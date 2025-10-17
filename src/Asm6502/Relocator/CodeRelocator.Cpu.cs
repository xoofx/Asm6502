// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Asm6502.Relocator;

partial class CodeRelocator
{
    private uint _runTotalCycleCount;

    /// <summary>
    /// Gets the total number of cycles completed during the current run via <see cref="RunSubroutineAt(ushort,uint,bool,bool,bool)"/>.
    /// </summary>
    public uint RunTotalCycleCount => _runTotalCycleCount;

    /// <summary>
    /// Gets the combined flags indicating how a byte at the specified RAM address has been accessed and used.
    /// </summary>
    /// <param name="address">The RAM address to query.</param>
    /// <returns>A <see cref="RamByteFlags"/> value combining access flags (Read/Write) and relocation flags.</returns>
    public RamByteFlags GetRamByteFlagsAt(ushort address)
    {
        var rwFlags = GetAccessMap(address);
        var offset = address - _programAddress;
        var flags = (uint)offset < (uint)_programByteStates.Length ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_programByteStates), offset).Flags : RamByteFlags.None;
        flags |= (RamByteFlags)rwFlags; // They share the same bit pattern for the flags Read and Write
        return flags;
    }
    
    /// <summary>
    /// Clears a region of RAM by setting all bytes to zero.
    /// </summary>
    /// <param name="targetAddress">The starting address of the region to clear.</param>
    /// <param name="length">The number of bytes to clear.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the target address and length exceed 64KB address space.</exception>
    public void ClearRamRegion(ushort targetAddress, ushort length)
    {
        if (length == 0) return;
        if (targetAddress + length > 0x10000)
            throw new ArgumentOutOfRangeException(nameof(targetAddress), "Target address and length exceed 64KB");
        _ram.AsSpan(targetAddress, length).Clear();
    }

    /// <summary>
    /// Copies data from a buffer into a region of RAM.
    /// </summary>
    /// <param name="targetAddress">The starting address where data will be written.</param>
    /// <param name="buffer">The data to copy into RAM.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the target address and buffer length exceed 64KB address space.</exception>
    public void SetRamRegion(ushort targetAddress, ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0) return;
        if (targetAddress + buffer.Length > 0x10000)
            throw new ArgumentOutOfRangeException(nameof(targetAddress), "Target address and length exceed 64KB");
        buffer.CopyTo(_ram.AsSpan(targetAddress, buffer.Length));
    }

    /// <summary>
    /// Executes a subroutine at the specified address, simulating a JSR call and running until RTS returns (or RTI if specified).
    /// </summary>
    /// <param name="address">The address of the subroutine to execute.</param>
    /// <param name="maxCycles">The maximum number of cycles to execute, or 0 for unlimited. Default is 0.</param>
    /// <param name="enableAnalysis">Whether to enable source tracking and relocation analysis during execution.</param>
    /// <param name="expectRtiInsteadOfRts">Whether to expect an RTI instruction instead of RTS for returning from the subroutine.</param>
    /// <param name="useCycleByCycle">Whether to execute the CPU in cycle-by-cycle mode. Default is false.</param>
    /// <returns>True if the maximum number of cycles was reached before returning; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the CPU halts or jams during execution.</exception>
    public bool RunSubroutineAt(ushort address, uint maxCycles = 0, bool enableAnalysis = true, bool expectRtiInsteadOfRts = false, bool useCycleByCycle = false)
    {
        _hasBeenAnalyzed = false; // Reset analysis state
        _srcA = null;
        _srcX = null;
        _srcY = null;
        _eaSrcMsb = null;

        _cpu.PC = address;
        var expectedOpcode = expectRtiInsteadOfRts ? Mos6502OpCode.RTI_Implied : Mos6502OpCode.RTS_Implied;

        // Simulate the return address on the stack:
        // - RTS: 2 bytes on the stack (when a JSR was done)
        // - RTI: 3 bytes on the stack (when an interrupt was done)
        _cpu.S = (byte)(0xFF - (expectRtiInsteadOfRts ? 3 : 2));

        // Clear stack
        ClearRamRegion(0x100, 0x100);

        _enableTrackSource = enableAnalysis; // Enable tracking during execution

        _runTotalCycleCount = 0;
        while (true)
        {
            if (useCycleByCycle)
            {
                _cpu.Cycle();
            }
            else
            {
                _cpu.FastStep();
            }

            if (_cpu.IsHalted || _cpu.IsJammed)
            {
                throw new InvalidOperationException($"CPU was halted or jammed at ${_cpu.PCAtOpcode:X4}");
            }

            if (Cpu.RunState == Mos6502CpuRunState.Fetch)
            {
                // Only run the post process after the full instruction has been executed
                PostProcessInstructionForRegisters();

                if (_cpu.CurrentOpCode == expectedOpcode && Cpu.S == 0xFF)
                {
                    // RTS with empty stack, we are done
                    break;
                }
            }

            _runTotalCycleCount += useCycleByCycle ? 1 : _cpu.InstructionCycles;
            if (maxCycles > 0 && _runTotalCycleCount >= maxCycles)
            {
                return true;
            }

            _eaSrcMsb = null;
        }

        _enableTrackSource = false;
        return false;
    }

    // Implementation of IMos6502CpuMemoryBus
    void IMos6502CpuMemoryBus.Trace(Mos6502MemoryBusAccessKind kind) => _kind = kind;

    byte IMos6502CpuMemoryBus.Read(ushort address)
    {
        var value = GetRam(address);
        if (_enableTrackSource)
        {
            GetAccessMap(address) |= RamReadWriteFlags.Read;
            HandleMemoryAccess(address, value);
        }
        return value;
    }

    void IMos6502CpuMemoryBus.Write(ushort address, byte value)
    {
        GetRam(address) = value;
        if (_enableTrackSource)
        {
            GetAccessMap(address) |= RamReadWriteFlags.Write;
            HandleMemoryAccess(address, value);
        }
    }
    
    private void PostProcessInstructionForRegisters()
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
    
    private void HandleMemoryAccess(ushort address, byte value)
    {
        // Don't track dummy accesses
        if (IsDummyBusAccess(_kind)) return;

        ref var source = ref GetRamProgramSource(address);
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
                var ea = (ushort)((value << 8) | GetRam((ushort)(address - 1)));
                CheckRelocRange(ea, GetRamProgramSource((ushort)(address - 1)), null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandBranchOffset:
                _eaSrcMsb = source;
                NoReloc(source);
                break;

            case Mos6502MemoryBusAccessKind.OperandIndirectHigh:
            {
                var indirectAddr = (ushort)((value << 8) | GetRam((ushort)(address - 1)));
                CheckRelocRange(indirectAddr, GetRamProgramSource((ushort)(address - 1)), null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandIndirectResolveHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)((value << 8) | GetRam((ushort)(address - 1)));
                CheckRelocRange(ea, GetRamProgramSource((ushort)(address - 1)), null, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandAbsoluteXHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)(((value << 8) | GetRam((ushort)(address - 1))) + _cpu.X);
                CheckRelocRange(ea, GetRamProgramSource((ushort)(address - 1)), _srcX, source);
            }
                break;
            case Mos6502MemoryBusAccessKind.OperandAbsoluteYHigh:
            {
                _eaSrcMsb = source;
                var ea = (ushort)(((value << 8) | GetRam((ushort)(address - 1))) + _cpu.Y);
                CheckRelocRange(ea, GetRamProgramSource((ushort)(address - 1)), _srcY, source);
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
                var ea = (ushort)((value << 8) | GetRam((ushort)(address - 1)));
                CheckRelocRange(ea, GetRamProgramSource((ushort)(address - 1)), null, source);
            }
                break;

            case Mos6502MemoryBusAccessKind.OperandIndirectY:
            {
                var tmp = value;
                UsedForZpAddr(tmp, source, _srcY);
                UsedForZpAddr((byte)(tmp + 1), source, _srcY);

                _eaSrcMsb = GetRamProgramSource((ushort)(tmp + 1));
                var ea = (ushort)(((GetRam((ushort)(tmp + 1)) << 8) | GetRam(tmp)) + _cpu.Y);
                CheckRelocRange(ea, GetRamProgramSource(tmp), _srcY, _eaSrcMsb);
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
                source = GetRamProgramSource((ushort)(_cpu.PC + 1));
                break;
            case Mos6502MemoryBusAccessKind.PopRtiLow:
                _eaSrcMsb = source;
                break;
            case Mos6502MemoryBusAccessKind.PopRtiHigh:
                //CheckRelocRange((ushort)((value << 8) | _cpu.PC), _eaSrcMsb, null, source);
                break;
            case Mos6502MemoryBusAccessKind.PopRtsLow:
                _eaSrcMsb = source;
                break;
            case Mos6502MemoryBusAccessKind.PopRtsHigh:
                //CheckRelocRange((ushort)((value << 8) | _cpu.PC), _eaSrcMsb, null, source);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref byte GetRam(ushort addr) => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_ram), addr);

    private ref RamReadWriteFlags GetAccessMap(ushort addr) => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_accessMap), addr);

    private static bool IsDummyBusAccess(Mos6502MemoryBusAccessKind kind) =>
        kind switch
        {
            Mos6502MemoryBusAccessKind.OperandDummyRead or Mos6502MemoryBusAccessKind.ExecuteDummyRead or Mos6502MemoryBusAccessKind.ExecuteDummyWrite => true,
            _ => false
        };
}