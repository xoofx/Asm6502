// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Asm6502.Relocator;

public class CodeRelocator : IMos6502CpuMemoryBus
{
    private readonly byte[] _ram = new byte[65536];
    private readonly ProgramSource?[] _ramProgramSources = new ProgramSource?[65536];
    private readonly bool[] _readMap = new bool[65536];
    private readonly bool[] _writeMap = new bool[65536];
    private readonly ushort _programAddress;
    private readonly byte[] _programBytes;
    private readonly ProgramByteInfo[] _programByteInfos;
    private readonly Mos6502Cpu _cpu;
    private readonly Stack<ProgramSource> _sourcePool = new();
    private bool _enableTrackSource;
    private readonly ConstraintList?[] _zpConstraints = new ConstraintList?[0x100];
    private readonly Dictionary<int, ConstraintList> _constraintsHashTable = new();

    private ProgramSource? _eaSrcMsb;
    private ProgramSource? _srcX;
    private ProgramSource? _srcY;
    private ProgramSource? _srcA;

    private Mos6502MemoryBusAccessKind _kind;

    public CodeRelocator(ushort programAddress, byte[] programBytes)
    {
        _programAddress = (ushort)programAddress;
        _programBytes = programBytes;
        _programByteInfos = new ProgramByteInfo[programBytes.Length];
        EnableZpReloc = true;
        for (ushort i = 0; i < programBytes.Length; i++)
        {
            ref var pgByte = ref _programByteInfos[i];
            pgByte = new ProgramByteInfo();
        }

        RelocationStart = programAddress;
        RelocationEnd = (ushort)(programAddress + programBytes.Length - 1);

        _cpu = new Mos6510Cpu(this);
        Reset();
    }

    public Mos6502Cpu Cpu => _cpu;

    public ushort RelocationStart { get; set; }

    public ushort RelocationEnd { get; set; }

    public ushort TargetRelocationStart { get; set; }

    public bool EnableZpReloc { get; set; }

    public int VerboseLevel { get; set; }

    public TextWriter? Log { get; set; }

    public void Reset()
    {
        var ram = _ram.AsSpan();
        ram.Clear();
        _readMap.AsSpan().Clear();
        _writeMap.AsSpan().Clear();

        for (var i = 0; i < _ramProgramSources.Length; i++)
        {
            ref var src = ref _ramProgramSources[i];
            ReleaseSource(src);
            src = null;
        }

        _programBytes.AsSpan().CopyTo(ram[_programAddress..]);

        for (ushort i = 0; i < _programByteInfos.Length; i++)
        {
            var addr = (ushort)(_programAddress + i);
            _ramProgramSources[addr] = CreateSourceAtProgramByteOffset(i, null);
            _programByteInfos[i].Flags = ProgramByteFlags.None;
        }

        _enableTrackSource = false;
        _cpu.Reset();
    }

    public void RunSubroutineAt(ushort address, int maxCycles)
    {
        // Check RelocationStart/End correctness
        if (RelocationStart < 0x200) throw new InvalidOperationException("RelocationStart must be greater than 0x200");

        int relocationEnd = RelocationStart + _programByteInfos.Length;
        if (relocationEnd >= 0x10000)
            throw new InvalidOperationException($"RelocationEnd (0x{relocationEnd:X}) must be less than 0x10000 (64KB)");

        if (RelocationStart >= RelocationEnd)
            throw new InvalidOperationException($"RelocationStart (0x{RelocationStart:X4}) must be less than RelocationEnd (0x{RelocationEnd:X4})");
        
        _srcA = null;
        _srcX = null;
        _srcY = null;
        _eaSrcMsb = null;

        _cpu.PC = address;
        _cpu.S = 0xFD; // Simulate that we came from a JSR (with a return address on the stack)
        _ram[0x1FE] = default; // Clear the return address on the stack
        _ram[0x1FF] = default;

        _enableTrackSource = true; // Enable tracking during execution

        int cycleCount = 0;
        while (true)
        {
            _cpu.Cycle();

            if (_cpu.IsHalted || _cpu.IsJammed)
            {
                throw new InvalidOperationException($"CPU was halted or jammed at 0x{_cpu.PCAtOpcode:X4}");
            }

            if (_cpu.RunState == Mos6502CpuRunState.Fetch && _cpu.CurrentOpCode == Mos6502OpCode.RTS_Implied)
            {
                break;
            }

            ProcessPostInstruction();

            if (maxCycles > 0 && cycleCount++ >= maxCycles)
            {
                break;
            }

            _eaSrcMsb = null;
        }

        _enableTrackSource = false;
    }

    public void Solve()
    {
        FinalizeConstraints();

        if (CheckTrivialInconsistency())
        {
            throw new InvalidOperationException("Trivial inconsistency detected");
        }

        if (RecursiveSolver())
        {
            throw new InvalidOperationException("No solution found");
        }
    }

    public void PrintRelocationMap(TextWriter writer)
    {
        int nReloc = 0, nZp = 0, nDont = 0, nUnused = 0, nUnknown = 0;

        writer.Write("Program map:");
        ushort org = _programAddress;
        ushort targetOrg = TargetRelocationStart;

        bool isFirst = true;
        for (int addr = org & 0xFFC0, taddr = targetOrg & 0xFFC0; addr <= ((org + _programByteInfos.Length - 1) | 0x003F); addr++, taddr++)
        {
            if ((addr & 0x3F) == 0)
            {
                writer.Write($"\n{addr:x4}, {taddr:x4}:  ");
            }

            if (addr < org || addr >= org + _programByteInfos.Length)
            {
                writer.Write(" ");
            }
            else
            {
                int i = addr - org;
                ref var pb = ref _programByteInfos[i];

                if ((pb.Flags & ProgramByteFlags.Reloc) != 0)
                {
                    if ((pb.Flags & ProgramByteFlags.UsedInMsb) != 0)
                    {
                        writer.Write("R");
                        nReloc++;
                    }
                    else if ((pb.Flags & ProgramByteFlags.UsedInZp) != 0)
                    {
                        writer.Write("Z");
                        nZp++;
                    }
                    else
                    {
                        writer.Write("e"); // internal error
                    }
                }
                else if ((pb.Flags & ProgramByteFlags.NoReloc) != 0)
                {
                    writer.Write("=");
                    nDont++;
                }
                else if (!(_readMap[addr] || _writeMap[addr]))
                {
                    writer.Write(".");
                    nUnused++;
                }
                else
                {
                    writer.Write("?");
                    nUnknown++;
                }
            }
        }

        writer.WriteLine();
        writer.WriteLine($"MSB relocations       (R): {nReloc}");
        writer.WriteLine($"Zero-page relocations (Z): {nZp}");
        writer.WriteLine($"Static bytes          (=): {nDont}");
        writer.WriteLine($"Status undetermined   (?): {nUnknown}");
        writer.WriteLine($"Unused bytes          (.): {nUnused}");

        writer.Write("Old zero-page addresses:  ");

        for(int i = 2; i < 256; i++ ){
            if (_writeMap[i]) writer.Write($" {i:x2}");
        }
        writer.WriteLine();
    }

    private void ProcessPostInstruction()
    {
        var mnemonic = ((Mos6510OpCode)_cpu.CurrentOpCode).ToMnemonic();
        switch (mnemonic)
        {
            case Mos6510Mnemonic.EOR:
            case Mos6510Mnemonic.AND:
            case Mos6510Mnemonic.ORA:
            case Mos6510Mnemonic.SLO:
            case Mos6510Mnemonic.ALR:
            case Mos6510Mnemonic.ANC:
            case Mos6510Mnemonic.ANE:
            case Mos6510Mnemonic.ARR:
                _srcA = null;
                break;

            case Mos6510Mnemonic.TAX:
                _srcX = _srcA;
                break;
            case Mos6510Mnemonic.TAY:
                _srcY = _srcA;
                break;
            case Mos6510Mnemonic.TXA:
                _srcA = _srcX;
                break;
            case Mos6510Mnemonic.TYA:
                _srcA = _srcY;
                break;
            case Mos6510Mnemonic.TXS:
                NoReloc(_srcX);
                break;

            case Mos6510Mnemonic.SBX:
                _srcX = null; // TODO: check if this is correct
                break;
            case Mos6510Mnemonic.LAX:
                _srcA = _eaSrcMsb;
                _srcX = _eaSrcMsb;
                break;
            case Mos6510Mnemonic.LXA:
            case Mos6510Mnemonic.LAS:
                _srcA = null;
                _srcX = null;
                break;
        }
    }


    private void HandleMemoryAccess(ushort address, byte value, ref ProgramSource? source, bool isRead)
    {
        // Don't track dummy accesses
        if (IsDummy(_kind)) return;

        if (isRead)
            _readMap[address] = true;
        else
            _writeMap[address] = true;

        var mnemonic = ((Mos6510OpCode)_cpu.CurrentOpCode).ToMnemonic();

        switch (_kind)
        {
            case Mos6502MemoryBusAccessKind.OpCode:
                NoReloc(source);
                break;
            case Mos6502MemoryBusAccessKind.OperandImmediate:
            case Mos6502MemoryBusAccessKind.ExecuteRead:
                _eaSrcMsb = source;

                switch (mnemonic)
                {
                    case Mos6510Mnemonic.ADC:
                        if ((_cpu.SR & Mos6502CpuFlags.D) == 0)
                        {
                            for (var s = source; s is not null; s = s.Next)
                                _srcA = CreateSourceAtProgramByteOffset(s.ProgramOffset, _srcA);
                        }
                        else
                        {
                            _srcA = null;
                        }
                        break;
                    case Mos6510Mnemonic.SBC:
                        if ((_cpu.SR & Mos6502CpuFlags.D) != 0)
                        {
                            _srcA = null;
                        }
                        break;
                    case Mos6510Mnemonic.CMP:
                        RelocAlike(value, source, _cpu.A, _srcA);
                        break;
                    case Mos6510Mnemonic.CPX:
                        RelocAlike(value, source, _cpu.X, _srcX);
                        break;
                    case Mos6510Mnemonic.CPY:
                        RelocAlike(value, source, _cpu.Y, _srcY);
                        break;
                    case Mos6510Mnemonic.EOR:
                        RelocAlike(value, source, _cpu.A, _srcA);
                        _srcA = null;
                        break;
                    case Mos6510Mnemonic.LDA:
                        _srcA = _eaSrcMsb;
                        break;
                    case Mos6510Mnemonic.LDX:
                        _srcX = _eaSrcMsb;
                        break;
                    case Mos6510Mnemonic.LDY:
                        _srcY = _eaSrcMsb;
                        break;

                    case Mos6510Mnemonic.ASL:
                    case Mos6510Mnemonic.LSR:
                    case Mos6510Mnemonic.ROL:
                    case Mos6510Mnemonic.ROR:
                        _eaSrcMsb = null;
                        break;
                }

                break;
            case Mos6502MemoryBusAccessKind.OperandAbsoluteHigh:
            case Mos6502MemoryBusAccessKind.OperandJsrAbsoluteHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)((value << 8) | _ram[address - 1]);
                CheckRelocRange(ea, _ramProgramSources[address - 1], null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandBranchOffset:
                _eaSrcMsb = source;
                NoReloc(source);
                break;

            case Mos6502MemoryBusAccessKind.OperandIndirectHigh:
            {
                var indirectAddr = (ushort)((value << 8) | _ram[address - 1]);
                CheckRelocRange(indirectAddr, _ramProgramSources[address - 1], null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandIndirectResolveHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)((value << 8) | _ram[address - 1]);
                CheckRelocRange(ea, _ramProgramSources[address - 1], null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandAbsoluteXHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)(((value << 8) | _ram[address - 1]) + _cpu.X);
                CheckRelocRange(ea, _ramProgramSources[address - 1], _srcX, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandAbsoluteYHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)(((value << 8) | _ram[address - 1]) + _cpu.Y);
                CheckRelocRange(ea, _ramProgramSources[address - 1], _srcY, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandZeroPage:
            {
                _eaSrcMsb = null;
                var ea = value;
                UsedForZpAddr(ea, source, null);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandZeroPageX:
            {
                _eaSrcMsb = null;
                var ea = (byte)(value + _cpu.X);
                UsedForZpAddr(ea, source, _srcX);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandZeroPageY:
            {
                _eaSrcMsb = null;
                var ea = (byte)(value + _cpu.Y);
                UsedForZpAddr(ea, source, _srcY);
            }
                break;

            case Mos6502MemoryBusAccessKind.OperandIndirectX:
            {
                var tmp = (byte)(value + _cpu.X);
                UsedForZpAddr(tmp, source, _srcX);
                UsedForZpAddr((byte)(tmp + 1), source, _srcX);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandIndirectXResolveHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)((value << 8) | _ram[address - 1]);
                CheckRelocRange(ea, _ramProgramSources[address - 1], null, source);
            }
                break;

            case Mos6502MemoryBusAccessKind.OperandIndirectY:
            {
                var tmp = value;
                UsedForZpAddr(tmp, source, _srcY);
                UsedForZpAddr((byte)(tmp + 1), source, _srcY);

                _eaSrcMsb = _ramProgramSources[tmp + 1];
                var ea = (ushort)(((_ram[tmp + 1] << 8) | _ram[tmp]) + _cpu.Y);
                CheckRelocRange(ea, _ramProgramSources[tmp], _srcY, _eaSrcMsb);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandIndirectYResolveHigh:
                // Handled by OperandIndirectY
                break;

            case Mos6502MemoryBusAccessKind.ExecuteWrite:

                switch (mnemonic)
                {
                    case Mos6510Mnemonic.STA:
                        source = _srcA;
                        break;
                    case Mos6510Mnemonic.STX:
                        source = _srcX;
                        break;
                    case Mos6510Mnemonic.STY:
                        source = _srcY;
                        break;

                    default:
                        source = _eaSrcMsb;
                        break;
                }



                break;
            case Mos6502MemoryBusAccessKind.VectorInterruptLow:
                break;
            case Mos6502MemoryBusAccessKind.VectorInterruptHigh:
                break;
            case Mos6502MemoryBusAccessKind.PushSR:
                source = null;
                break;
            case Mos6502MemoryBusAccessKind.PopSR:
                NoReloc(source);
                break;
            case Mos6502MemoryBusAccessKind.PushRegisterA:
                source = _srcA;
                break;
            case Mos6502MemoryBusAccessKind.PopRegisterA:
                _srcA = source;
                break;
            case Mos6502MemoryBusAccessKind.PushInterruptReturnAddressHigh:
                break;
            case Mos6502MemoryBusAccessKind.PushInterruptReturnAddressLow:
                break;
            case Mos6502MemoryBusAccessKind.PushInterruptSR:
                break;
            case Mos6502MemoryBusAccessKind.PushJsrTargetLow:
                source = null;
                break;
            case Mos6502MemoryBusAccessKind.PushJsrTargetHigh:
                source = _ramProgramSources[_cpu.PC + 1];
                break;
            case Mos6502MemoryBusAccessKind.PopRtiLow:
                // TODO
                break;
            case Mos6502MemoryBusAccessKind.PopRtiHigh:
                // TODO
                source = null;
                break;
            case Mos6502MemoryBusAccessKind.PopRtsLow:
                _eaSrcMsb = source;
                break;
            case Mos6502MemoryBusAccessKind.PopRtsHigh:
                // TODO: add abck
                //CheckRelocRange((ushort)((value << 8) | _cpu.PC), _eaSrcMsb, null, source);
                break;
        }

    }

    private void NoRelocAt(ushort offset)
    {
        _programByteInfos[offset].Flags |= ProgramByteFlags.NoReloc;
    }

    private void RelocAt(ushort offset)
    {
        _programByteInfos[offset].Flags |= ProgramByteFlags.Reloc;
    }

    private void NoReloc(ProgramSource? src)
    {
        while (src != null)
        {
            NoRelocAt(src.ProgramOffset);
            src = src.Next;
        }
    }

    private void RelocAlike(byte v1, ProgramSource? sv1, byte v2, ProgramSource? sv2)
    {
        if (v1 >= (RelocationStart >> 8) && v1 <= (RelocationEnd >> 8) &&
            v2 >= (RelocationStart >> 8) && v2 <= (RelocationEnd >> 8))
        {
            var constraint = new Constraint
            {
                CheckNeeded = true,
                Kind = ConstraintKind.Alike,
            };

            for (var s = sv1; s is not null; s = s.Next)
            {
                constraint.ProgramOffsets1.Add(s.ProgramOffset);
            }

            for (var s = sv2; s is not null; s = s.Next)
            {
                constraint.ProgramOffsets2.Add(s.ProgramOffset);
            }

            ConstraintList? list = null;
            AddOrMergeConstraint(ref list, constraint);
        }
    }

    private void RelocExactlyOne(ProgramSource? src, byte zpAddress)
    {
        // Mark duplicates as non-relocatable
        for (var s = src; s is not null; s = s.Next)
        {
            for (var s2 = s.Next; s2 is not null; s2 = s2.Next)
            {
                // The same progbyte contributes more than
                // once. It cannot be relocated.
                if (s.ProgramOffset == s2.ProgramOffset)
                {
                    Log?.WriteLine($"Byte at 0x{_programAddress + s.ProgramOffset:X4} contributes more than once to a sum and won't be relocated.");
                    NoRelocAt(s.ProgramOffset);
                }
            }
        }

        int unknownCount = 0;
        int dontCount = 0;
        int doCount = 0;
        ushort lastDoOffset = 0;
        ushort lastUnknownOffset = 0;

        for (var s = src; s is not null; s = s.Next)
        {
            ref var programByte = ref _programByteInfos[s.ProgramOffset];
            if ((programByte.Flags & ProgramByteFlags.NoReloc) != 0)
            {
                dontCount++;
            }
            else if ((programByte.Flags & ProgramByteFlags.Reloc) != 0)
            {
                doCount++;
                lastDoOffset = s.ProgramOffset;
            }
            else
            {
                unknownCount++;
                lastUnknownOffset = s.ProgramOffset;
            }
        }

        if (zpAddress != 0)
        {
            // For zero-page constraints, collect all non-dont offsets
            var constraint = new Constraint
            {
                CheckNeeded = true,
                Kind = ConstraintKind.ExactlyOne,
            };
            
            for (var s = src; s is not null; s = s.Next)
            {
                if ((_programByteInfos[s.ProgramOffset].Flags & ProgramByteFlags.NoReloc) == 0)
                {
                    constraint.ProgramOffsets1.Add(s.ProgramOffset);
                }
            }

            ref var constraints = ref _zpConstraints[zpAddress];
            constraints ??= new ConstraintList();
            AddOrMergeConstraint(ref constraints, constraint);
        }
        else
        {
            if (doCount != 0)
            {
                // Mark all others as don't reloc
                for (var s = src; s is not null; s = s.Next)
                {
                    if (s.ProgramOffset != lastDoOffset)
                    {
                        NoRelocAt(s.ProgramOffset);
                    }
                }
            }
            else
            {
                // nDo is 0
                if (unknownCount == 1)
                {
                    RelocAt(lastUnknownOffset);
                }
                else if (unknownCount == 0)
                {
                    Log?.Write("Inconsistency: Want to relocate one of {");
                    for (var s = src; s is not null; s = s.Next)
                    {
                        Log?.Write($"0x{_programAddress + s.ProgramOffset:X4}{(s.Next != null ? ", " : "")}");
                    }
                    Log?.WriteLine("} but this would contradict other equations.");
                    throw new InvalidOperationException("Constraint inconsistency");
                }
                else
                {
                    // Create constraint
                    var constraint = new Constraint
                    {
                        CheckNeeded = true,
                        Kind = ConstraintKind.ExactlyOne,
                    };

                    for (var s = src; s is not null; s = s.Next)
                    {
                        if ((_programByteInfos[s.ProgramOffset].Flags & (ProgramByteFlags.Reloc | ProgramByteFlags.NoReloc)) == 0)
                        {
                            constraint.ProgramOffsets1.Add(s.ProgramOffset);
                        }
                    }

                    ConstraintList? list = null;
                    AddOrMergeConstraint(ref list, constraint);
                }
            }
        }
    }

    private void AddOrMergeConstraint(ref ConstraintList? constraints, Constraint constraint)
    {
        // Sort offsets for comparison
        constraint.SortOffsets();

        if (constraints is null)
        {
            var hash = constraint.GetHashCode();
            if (!_constraintsHashTable.TryGetValue(hash, out constraints))
            {
                constraints = new ConstraintList();
                _constraintsHashTable[hash] = constraints;
            }
        }
        
        // Try to find existing constraint
        foreach(var existing in CollectionsMarshal.AsSpan(constraints))
        {
            if (existing.Equals(constraint))
            {
                // Duplicate, don't add
                return;
            }
        }

        constraints.Add(constraint);
    }

    private void CheckRelocRange(ushort addr, ProgramSource? lsb1, ProgramSource? lsb2, ProgramSource? msb)
    {
        Debug.Assert(msb is not null);

        for (var s = msb; s != null; s = s.Next)
        {
            _programByteInfos[s.ProgramOffset].Flags |= ProgramByteFlags.UsedInMsb;
        }

        if (addr >= RelocationStart && addr <= RelocationEnd)
        {
            NoReloc(lsb1);
            if (lsb2 is not null) NoReloc(lsb2);
            RelocExactlyOne(msb, 0);
        }
        else if (addr < 0x100)
        {
            NoReloc(msb);
            UsedForZpAddr((byte)addr, lsb1, lsb2);
        }
        else
        {
            NoReloc(msb);
            NoReloc(lsb1);
            if (lsb2 is not null) NoReloc(lsb2);
        }
    }

    private void UsedForZpAddr(byte zpAddr, ProgramSource? src1, ProgramSource? src2)
    {
        for (var s = src1; s is not null; s = s.Next)
            _programByteInfos[s.ProgramOffset].SetUsedInZp(zpAddr);

        for (var s = src2; s is not null; s = s.Next)
            _programByteInfos[s.ProgramOffset].SetUsedInZp(zpAddr);

        if (EnableZpReloc)
        {
            var list = src1;
            for (var s = src2; s is not null; s = s.Next) list = CreateSourceAtProgramByteOffset(s.ProgramOffset, list);
            RelocExactlyOne(list, zpAddr);
        }
    }
    
    private ProgramSource? CreateSourceAtProgramByteOffset(ushort offset, ProgramSource? next)
    {
        // Don't track an offset that is marked as NoReloc
        if ((_programByteInfos[offset].Flags & ProgramByteFlags.NoReloc) != 0)
        {
            return next;
        }

        // No need to add the same program byte more
        // than twice to a list.
        for (var s = next; s is not null; s = s.Next)
        {
            if (s.ProgramOffset == offset)
            {
                for (s = s.Next; s is not null; s = s.Next)
                {
                    if (s.ProgramOffset == offset)
                    {
                        return next;
                    }
                }

                break;
            }
        }

        var newSource = GetOrCreateSource();
        newSource.ProgramOffset = offset;
        newSource.Next = next;
        return newSource;
    }

    private ProgramSource GetOrCreateSource()
    {
        if (_sourcePool.Count > 0)
        {
            var src = _sourcePool.Pop();
            src.Next = null;
            return src;
        }
        return new ProgramSource();
    }

    private void ReleaseSource(ProgramSource? src)
    {
        while (src != null)
        {
            var next = src.Next;
            _sourcePool.Push(src);
            src.Next = null;
            src = next;
        }
    }

    private void FinalizeConstraints()
    {
        // Process zero-page constraints
        if (EnableZpReloc)
        {
            for (int i = 0; i < _zpConstraints.Length; i++)
            {
                var constraints = _zpConstraints[i];
                if (_writeMap[i])
                {
                    Debug.Assert(constraints is not null);
                    foreach (var constraint in constraints!)
                    {
                        ConstraintList? list = null;
                        AddOrMergeConstraint(ref list, constraint);
                    }
                }
                else
                {
                    // TODO: recycle  constraints
                    _zpConstraints[i] = null;
                }
            }
        }
        
        foreach (var constraints in _constraintsHashTable.Values)
        {
            foreach (var constraint in constraints)
            {
                AddConstraintToProgramBytes(constraint, constraint.ProgramOffsets1);
                AddConstraintToProgramBytes(constraint, constraint.ProgramOffsets2);
            }
        }
    }

    private void AddConstraintToProgramBytes(Constraint constraint, List<ushort> offsets)
    {
        foreach (var offset in CollectionsMarshal.AsSpan(offsets))
        {
            _programByteInfos[offset].Constraints.Add(constraint);
        }
    }

    private bool TryPropagateConstraint(Constraint constraint)
    {
        constraint.CheckNeeded = false;

        if (constraint.Kind == ConstraintKind.ExactlyOne)
        {
            int relocCount = 0, noRelocCount = 0, unknownRelocCount = 0;
            ushort lastRelocIndex = 0, lastUnknownIndex = 0;

            for (ushort i = 0; i < constraint.ProgramOffsets1.Count; i++)
            {
                var offset = constraint.ProgramOffsets1[i];
                ref var pb = ref _programByteInfos[offset];

                if ((pb.Flags & ProgramByteFlags.Reloc) != 0)
                {
                    relocCount++;
                    lastRelocIndex = i;
                }
                else if ((pb.Flags & ProgramByteFlags.NoReloc) != 0)
                {
                    noRelocCount++;
                }
                else
                {
                    unknownRelocCount++;
                    lastUnknownIndex = i;
                }
            }

            if (relocCount == 1)
            {
                // Exactly one is already set to reloc, set all others to don't reloc
                for (var i = 0; i < constraint.ProgramOffsets1.Count; i++)
                {
                    if (i != lastRelocIndex)
                    {
                        if (EnforceNoReloc(constraint.ProgramOffsets1[i]))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            else if (relocCount > 1)
            {
                return true; // Inconsistency
            }
            else
            {
                return unknownRelocCount switch
                {
                    // nDo == 0, exactly one of the unknown vars must be relocated
                    0 => true,
                    1 => EnforceReloc(constraint.ProgramOffsets1[lastUnknownIndex]),
                    // We cannot propagate; leave the rest to search.
                    _ => false
                };
            }
        }
        else if (constraint.Kind == ConstraintKind.Alike)
        {
            int n1Do = 0, n2Do = 0;

            foreach (var offset in constraint.ProgramOffsets1)
            {
                if ((_programByteInfos[offset].Flags & ProgramByteFlags.Reloc) != 0)
                {
                    n1Do++;
                }
            }

            foreach (var offset in constraint.ProgramOffsets2)
            {
                if ((_programByteInfos[offset].Flags & ProgramByteFlags.Reloc) != 0)
                {
                    n2Do++;
                }
            }

            if (n1Do > 1 || n2Do > 1) return true;
            return n1Do != n2Do;
        }

        return true;
    }

    private bool CheckTrivialInconsistency()
    {
        for (ushort i = 0; i < _programByteInfos.Length; i++)
        {
            ref var pb = ref _programByteInfos[i];

            // If a byte contributes to both a zero-page address and an msb, it cannot be relocatable
            if ((pb.Flags & ProgramByteFlags.UsedInZp) != 0 && (pb.Flags & ProgramByteFlags.UsedInMsb) != 0)
            {
                pb.Flags |= ProgramByteFlags.NoReloc;
            }

            if ((pb.Flags & ProgramByteFlags.Reloc) != 0 && (pb.Flags & ProgramByteFlags.NoReloc) != 0)
            {
                Log?.WriteLine($"Inconsistency detected! Byte at 0x{_programAddress + i:X4} can't be both relocated and not relocated at the same time.");
                return true;
            }
        }

        return false;
    }

    private bool RecursiveSolver()
    {
        // Propagate constraints
        while (true)
        {
            var done = true;
            foreach (var constraints in _constraintsHashTable.Values)
            {
                foreach (var constraint in constraints)
                {
                    if (constraint.CheckNeeded)
                    {
                        if (TryPropagateConstraint(constraint))
                        {
                            return false; // Inconsistency
                        }

                        done = false;
                    }
                }
            }
            if (done) break;
        }

        // Search for an offset to decide
        foreach(var constraints in _constraintsHashTable.Values)
        {
            foreach (var constraint in constraints)
            {
                foreach (var offset in constraint.GetAllOffsets())
                {
                    if ((_programByteInfos[offset].Flags & (ProgramByteFlags.Reloc | ProgramByteFlags.NoReloc)) != 0)
                    {
                        continue;
                    }

                    // Backtrack: save state
                    var backtrack = new ProgramByteFlags[_programByteInfos.Length];
                    for (ushort i = 0; i < _programByteInfos.Length; i++)
                    {
                        backtrack[i] = _programByteInfos[i].Flags;
                    }

                    if (VerboseLevel >= 2)
                    {
                        Log?.WriteLine($"Guessing that 0x{_programAddress + offset:X4} should not be relocated.");
                    }

                    if (EnforceNoReloc(offset) || RecursiveSolver())
                    {
                        if (VerboseLevel >= 2)
                        {
                            Log?.WriteLine("Backtracking.");
                        }
                            
                        // Restore state
                        for (ushort i = 0; i < _programByteInfos.Length; i++)
                        {
                            _programByteInfos[i].Flags = backtrack[i];
                        }

                        if (VerboseLevel >= 2)
                        {
                            Log?.WriteLine($"Assuming that 0x{_programAddress + offset:X4} should be relocated.");
                        }

                        return EnforceReloc(offset) || RecursiveSolver();
                    }

                    return false;
                }
            }
        }

        return false; // No more variables to decide - success
    }


    private bool EnforceNoReloc(ushort offset)
    {
        ref var pb = ref _programByteInfos[offset];

        if ((pb.Flags & ProgramByteFlags.Reloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((pb.Flags & ProgramByteFlags.NoReloc) == 0)
        {
            pb.Flags |= ProgramByteFlags.NoReloc;

            foreach (var constraint in pb.Constraints)
            {
                constraint.CheckNeeded = true;
            }
        }

        return false;
    }

    private bool EnforceReloc(ushort offset)
    {
        ref var pb = ref _programByteInfos[offset];

        if ((pb.Flags & ProgramByteFlags.NoReloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((pb.Flags & ProgramByteFlags.Reloc) == 0)
        {
            pb.Flags |= ProgramByteFlags.Reloc;

            foreach (var constraint in pb.Constraints)
            {
                constraint.CheckNeeded = true;
            }
        }

        return false;
    }
    

    void IMos6502CpuMemoryBus.Trace(Mos6502MemoryBusAccessKind kind) => _kind = kind;

    byte IMos6502CpuMemoryBus.Read(ushort address)
    {
        var value = _ram[address];
        if (_enableTrackSource)
        {
            HandleMemoryAccess(address, value, ref _ramProgramSources[address], true);
        }
        return value;
    }

    void IMos6502CpuMemoryBus.Write(ushort address, byte value)
    {
        _ram[address] = value;
        if (_enableTrackSource)
        {
            HandleMemoryAccess(address, value, ref _ramProgramSources[address], false);
        }
    }

    private static bool IsDummy(Mos6502MemoryBusAccessKind kind) =>
        kind switch
        {
            Mos6502MemoryBusAccessKind.OperandDummyRead or Mos6502MemoryBusAccessKind.ExecuteDummyRead or Mos6502MemoryBusAccessKind.ExecuteDummyWrite => true,
            _ => false
        };

    private class ProgramSource
    {
        public ProgramSource? Next;

        public ushort ProgramOffset;
    }

    private struct ProgramByteInfo()
    {
        public ProgramByteFlags Flags;

        public readonly ConstraintList Constraints = new();

        public ZpBitmap ZpAddr;

        public void SetUsedInZp(byte zpAddr)
        {
            Flags |= ProgramByteFlags.UsedInZp;
            ZpAddr.SetUsed(zpAddr);
        }
    }

    [InlineArray(32)]
    private struct ZpBitmap
    {
        private byte _e;

        public bool IsUsed(byte zpAddr) => (this[zpAddr >> 3] & (1 << (zpAddr & 7))) != 0;

        public void SetUsed(byte zpAddr) => this[zpAddr >> 3] |= (byte)(1 << (zpAddr & 7));

        public void Clear()
        {
            Span<byte> span = this;
            span.Clear();
        }
    }

    [Flags]
    private enum ProgramByteFlags
    {
        None = 0,
        NoReloc = 1 << 0,
        Reloc = 1 << 1,
        UsedInZp = 1 << 2,
        UsedInMsb = 1 << 3,
    }

    private class ConstraintList : List<Constraint>;

    private class Constraint : IEquatable<Constraint>
    {
        public bool CheckNeeded { get; set; }

        public ConstraintKind Kind { get; set; }

        public readonly List<ushort> ProgramOffsets1 = new();

        public readonly List<ushort> ProgramOffsets2 = new();

        public void SortOffsets()
        {
            CollectionsMarshal.AsSpan(ProgramOffsets1).Sort();
            CollectionsMarshal.AsSpan(ProgramOffsets2).Sort();
        }

        public IEnumerable<ushort> GetAllOffsets()
        {
            foreach (var offset in ProgramOffsets1)
            {
                yield return offset;
            }

            foreach (var offset in ProgramOffsets2)
            {
                yield return offset;
            }
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var offset in CollectionsMarshal.AsSpan(ProgramOffsets1))
            {
                hash.Add(offset);
            }
            foreach (var offset in CollectionsMarshal.AsSpan(ProgramOffsets2))
            {
                hash.Add(offset);
            }

            return hash.ToHashCode();
        }

        public bool Equals(Constraint? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Kind == other.Kind &&
                   CollectionsMarshal.AsSpan(ProgramOffsets1).SequenceEqual(CollectionsMarshal.AsSpan(other.ProgramOffsets1)) &&
                   CollectionsMarshal.AsSpan(ProgramOffsets2).SequenceEqual(CollectionsMarshal.AsSpan(other.ProgramOffsets2));
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((Constraint)obj);
        }

        public static bool operator ==(Constraint? left, Constraint? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Constraint? left, Constraint? right)
        {
            return !Equals(left, right);
        }
    }

    private enum ConstraintKind
    {
        None,
        ExactlyOne,
        Alike,
    }
}