// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Analyzes and relocates MOS 6502/6510 machine code by emulating it, tracking memory accesses, identifying zero-page usage,
/// and determining safe relocation addresses while maintaining code functionality.
/// </summary>
/// <remarks>
/// This class emulates a MOS 6502/6510 CPU to trace program execution and analyze memory access patterns.
/// It identifies which memory locations and zero-page addresses are used by the code, allowing for safe
/// relocation of the program to different memory addresses.
/// </remarks>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeRelocator"/> class with the specified program data and optional relocation boundaries.
    /// </summary>
    /// <param name="programAddress">The original address in memory where the program is loaded.</param>
    /// <param name="programBytes">The byte array containing the machine code to be analyzed and relocated.</param>
    /// <param name="relocationAnalysisStart">The starting address for the relocation analysis range. If null, defaults to <paramref name="programAddress"/>.</param>
    /// <param name="relocationAnalysisEnd">The ending address for the relocation analysis  range. If null, defaults to the end of the program.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="relocationAnalysisStart"/> is less than 0x200, or when <paramref name="relocationAnalysisStart"/> is greater than or equal to <paramref name="relocationAnalysisEnd"/>.
    /// </exception>
    public CodeRelocator(ushort programAddress, byte[] programBytes, ushort? relocationAnalysisStart = null, ushort? relocationAnalysisEnd = null)
    {
        ArgumentNullException.ThrowIfNull(programBytes);

        RelocationAnalysisStart = relocationAnalysisStart ?? programAddress;
        RelocationAnalysisEnd = relocationAnalysisEnd ?? (ushort)(programAddress + programBytes.Length - 1);

        // Check RelocationStart/End correctness
        if (RelocationAnalysisStart < 0x200) throw new InvalidOperationException("RelocationStart must be greater than $0200");
        if (RelocationAnalysisStart >= RelocationAnalysisEnd)
            throw new InvalidOperationException($"RelocationStart (${RelocationAnalysisStart:x4}) must be less than RelocationEnd (${RelocationAnalysisEnd:x4})");

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

    /// <summary>
    /// Gets the MOS 6502/6510 CPU instance used for emulating and analyzing the program code.
    /// </summary>
    public Mos6502Cpu Cpu => _cpu;

    /// <summary>
    /// Gets a read-only view of the 64KB RAM emulated by this relocator.
    /// </summary>
    /// <remarks>
    /// In order to modify the ram, use the <see cref="Reset"/> method to restore the original program bytes,
    /// Then you can use <see cref="ClearRamRegion"/> and <see cref="SetRamRegion"/> to modify the RAM.
    /// </remarks>
    public ReadOnlySpan<byte> Ram => _ram;
    
    /// <summary>
    /// Gets the address of the program in memory.
    /// </summary>
    public ushort ProgramAddress => _programAddress;

    /// <summary>
    /// Gets a read-only view of the original program bytes.
    /// </summary>
    public ReadOnlySpan<byte> ProgramBytes => _programBytes;

    /// <summary>
    /// Gets the starting address of the relocation analysis range. By default, it is the <see cref="ProgramAddress"/>
    /// </summary>
    public ushort RelocationAnalysisStart { get; }

    /// <summary>
    /// Gets the ending address of the relocation analysis range. By default, it is <see cref="ProgramAddress"/> + <see cref="ProgramBytes"/> length - 1.
    /// </summary>
    public ushort RelocationAnalysisEnd { get; }

    /// <summary>
    /// Gets the list of RAM address ranges that are safe to use during relocation, identified through program analysis.
    /// </summary>
    public List<RamRangeAccess> SafeRamRanges { get; } = new();

    /// <summary>
    /// Gets a value indicating whether zero-page relocation is enabled for this relocator. Default is true.
    /// </summary>
    public bool EnableZpReloc { get; init; }

    /// <summary>
    /// Gets the diagnostic bag containing warnings and errors generated during code analysis and relocation.
    /// </summary>
    public CodeRelocationDiagnosticBag Diagnostics { get; } = new();

    /// <summary>
    /// Resets the relocator to its initial state, clearing all analysis data, diagnostics, and restoring the original program in memory.
    /// </summary>
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