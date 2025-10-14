// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public partial class CodeRelocator : IMos6502CpuMemoryBus
{
    private readonly byte[] _ram = new byte[65536];
    private readonly ProgramSource?[] _ramProgramSources = new ProgramSource?[65536];
    private readonly RamReadWriteFlags[] _accessMap = new RamReadWriteFlags[65536];
    private readonly ushort _programAddress;
    private readonly byte[] _programBytes;
    private readonly Mos6502Cpu _cpu;
    private readonly ProgramByteState[] _programByteInfos;
    private readonly Stack<ProgramSource> _sourcePool = new();
    private bool _enableTrackSource;
    private readonly ConstraintList?[] _zpConstraints = new ConstraintList?[0x100];
    private readonly Dictionary<int, ConstraintList> _constraintsHashTable = new();

    // Track of the program source for the current effective address and all registers
    private ProgramSource? _eaSrcMsb;
    private ProgramSource? _srcX;
    private ProgramSource? _srcY;
    private ProgramSource? _srcA;

    private Mos6502MemoryBusAccessKind _kind; // Property to track the current access kind from the CPU
    private readonly ZeroPageReloc[] _zpRelocations;
    private bool _hasBeenAnalyzed;
    private CodeRelocationTarget _lastRelocationTarget;

    public CodeRelocator(ushort programAddress, byte[] programBytes, ushort? relocationStart = null, ushort? relocationEnd = null)
    {
        RelocationStart = relocationStart ?? programAddress;
        RelocationEnd = relocationEnd ?? (ushort)(programAddress + programBytes.Length - 1);

        // Check RelocationStart/End correctness
        if (RelocationStart < 0x200) throw new InvalidOperationException("RelocationStart must be greater than 0x200");
        if (RelocationStart >= RelocationEnd)
            throw new InvalidOperationException($"RelocationStart (0x{RelocationStart:X4}) must be less than RelocationEnd (0x{RelocationEnd:X4})");

        _programAddress = (ushort)programAddress;
        _programBytes = programBytes;
        _programByteInfos = new ProgramByteState[programBytes.Length];
        EnableZpReloc = true;
        for (ushort i = 0; i < programBytes.Length; i++)
        {
            _programByteInfos[i] = new ProgramByteState();
        }

        _zpRelocations = new ZeroPageReloc[256];

        _cpu = new Mos6510Cpu(this);
        Reset();
    }

    public Mos6502Cpu Cpu => _cpu;

    public ReadOnlySpan<byte> Ram => _ram;

    public ushort RelocationStart { get; }

    public ushort RelocationEnd { get; }

    public List<RamRangeAccess> SafeRamRanges { get; } = new();

    public bool EnableZpReloc { get; init; }

    public CodeRelocationDiagnosticBag Diagnostics { get; } = new();

    public void Reset()
    {
        Diagnostics.Clear();

        var ram = _ram.AsSpan();
        ram.Clear();
        _accessMap.AsSpan().Clear();

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
            _programByteInfos[i].Flags = RamByteFlags.None;
        }

        _enableTrackSource = false;
        _cpu.Reset();
    }
}