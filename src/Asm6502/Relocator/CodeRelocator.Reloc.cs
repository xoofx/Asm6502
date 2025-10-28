// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// The code in this file is based on code from the original project sidreloc 1.0 by Linus Akesson,
// which is licensed under the MIT license (see below).
// The original code has been largely adapted and integrated into Asm6502.
// https://www.linusakesson.net/software/sidreloc/index.php

/* Copyright (c) 2012 Linus Akesson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Asm6502.Relocator;

partial class CodeRelocator
{
    private readonly Stack<SolverStackFrame> _solverStackFramePool = new();
    private readonly Stack<RamByteFlags[]> _backtrackPool = new();
    private ConstraintList[] _allConstraints = [];

    /// <summary>
    /// Gets or sets a value indicating whether the application is in unit testing mode (used by CodeRelocatorTests).
    /// </summary>
    internal bool Testing { get; set; }
    
    /// <summary>
    /// Checks the indirect address and determines if it is relocatable.
    /// </summary>
    /// <remarks>This method resolves the address from the specified indirect address and checks if it falls
    /// within a relocatable range. If the resolved address is non-zero, it performs additional checks to verify its
    /// relocatability and track the source of relocations.
    ///
    /// It can be used to track vectors or jump tables sets during a prior pass over the code. And execute them later for emulation (e.g. NMI routines or IRQ routines).
    /// </remarks>
    /// <param name="indirectAddress">The indirect address to be checked, represented as a 16-bit unsigned integer.</param>
    /// <param name="address">When this method returns, contains the resolved address if the indirect address is relocatable; otherwise, it is
    /// set to zero.</param>
    /// <returns><see langword="true"/> if the indirect address is relocatable and the resolved address is non-zero; otherwise,
    /// <see langword="false"/>.</returns>
    public bool CheckIndirectAddressAndProbeIfRelocatable(ushort indirectAddress, out ushort address)
    {
        address = (ushort)((Ram[indirectAddress + 1] << 8) | Ram[indirectAddress]);
        if (address != 0)
        {
            var lsbSource = GetRamProgramSource(indirectAddress);
            var msbSource = GetRamProgramSource((ushort)(indirectAddress + 1));

            CheckRelocRange(address, lsbSource, null, msbSource);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Relocates the program code to the specified target address and applies any required zero-page relocations.
    /// </summary>
    /// <remarks>This method performs relocation only once per instance; subsequent calls reuse the analysis
    /// unless the relocation target changes. The returned byte array represents the program code as it would appear at
    /// the specified relocation address, with all applicable relocations applied. If zero-page relocation is enabled,
    /// the method uses the provided zero-page range to resolve zero-page references.</remarks>
    /// <param name="relocationTarget">The relocation target specifying the destination address and zero-page range for the relocated code. The address
    /// must be greater than or equal to 0x200, and the low byte must match the relocation analysis start address. If
    /// zero-page relocation is enabled, a non-empty zero-page range must be provided.</param>
    /// <returns>A byte array containing the relocated program code. Returns null if relocation is not possible.</returns>
    /// <exception cref="ArgumentException">Thrown if the relocation target address is less than 0x200, if the low byte of the address does not match the
    /// relocation analysis start address, if the address plus program length exceeds 64KB, or if zero-page relocation
    /// is enabled but no zero-page range is specified.</exception>
    /// <exception cref="CodeRelocationException">Thrown if a trivial inconsistency is detected during relocation analysis or if no valid relocation solution can
    /// be found.</exception>
    public byte[]? Relocate(CodeRelocationTarget relocationTarget)
    {
        _lastRelocationTarget = default;

        if (EnableZpReloc && relocationTarget.ZpRange.IsEmpty)
        {
            throw new ArgumentException("Zero-page relocation is enabled but no zero-page range was specified", nameof(relocationTarget));
        }

        if ((byte)relocationTarget.Address != (_programAddress & 0x00FF))
        {
            throw new ArgumentException($"Relocation target address ${relocationTarget.Address:x4} low byte must be match the program address low byte ${(byte)_programAddress:x2} instead of ${(byte)relocationTarget.Address:x2}", nameof(relocationTarget));
        }

        if (relocationTarget.Address < 0x200)
        {
            throw new ArgumentException("Relocation target address must be >= $200", nameof(relocationTarget));
        }

        if (relocationTarget.Address + _programBytes.Length > 0x10000)
        {
            throw new ArgumentException($"Relocation target address + program length must be within 64KB. Actual: ${relocationTarget.Address + _programBytes.Length:x4}", nameof(relocationTarget));
        }

        _lastRelocationTarget = relocationTarget;

        if (!_hasBeenAnalyzed)
        {
            ReportBadMemoryAccess(); // TODO: check result

            FinalizeConstraints();
            _hasBeenAnalyzed = true;

            if (CheckTrivialInconsistency())
            {
                throw new CodeRelocationException(CodeRelocationDiagnosticId.ERR_RelocationInconsistency, "Trivial inconsistency detected. Check Diagnostics.");
            }

            var clock = Stopwatch.StartNew();
            try
            {
                // Collect all constraints
                _allConstraints = _constraintsHashTable.Values.ToArray();

                //if (RecursiveSolver())
                if (HeapSolver())
                {
                    throw new CodeRelocationException(CodeRelocationDiagnosticId.ERR_NoSolutionFound, "No solution found. Check Diagnostics.");
                }
            }
            finally
            {
                clock.Stop();
                if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                {
                    Diagnostics.Info(CodeRelocationDiagnosticId.TRC_SolverTime, $"Relocation solving took {(Testing ? 0.0 : clock.Elapsed.TotalMilliseconds):0.00}ms");
                }
            }
        }

        ComputeZeroPageRelocations(relocationTarget.ZpRange);

        var newProgramBytes = (byte[])_programBytes.Clone();

        // Apply relocations
        var msb = (byte)(relocationTarget.Address >> 8);
        var msbOffset = msb - (_programAddress >> 8);
        for (int i = 0; i < newProgramBytes.Length; i++)
        {
            ref var pb = ref _programByteStates[i];
            if ((pb.Flags & RamByteFlags.Reloc) == 0)
            {
                continue;
            }

            ref var b = ref newProgramBytes[i];
            if ((pb.Flags & RamByteFlags.UsedInMsb) != 0)
            {
                b = (byte)(b + msbOffset);
            }
            else if (EnableZpReloc && (pb.Flags & RamByteFlags.UsedInZp) != 0)
            {
                int j;
                for(j = 2; j< 256; j++) {
                    if(pb.ZpAddr!.IsUsed((byte)j)) {
                        break;
                    }
                }
                if (j < 256)
                {
                    b = _zpRelocations[j].Reloc;
                }
            }
        }

        return newProgramBytes;
    }

    /// <summary>
    /// Retrieves the set of zero-page addresses relocated after running <see cref="Relocate"/>.
    /// </summary>
    /// <returns>An array of bytes containing the relocation addresses in the zero page. The array will be
    /// empty if no writable addresses are found.</returns>
    public byte[] GetZeroPageAddresses()
    {
        int countZp = 0;
        for (int i = 2; i < 256; i++)
        {
            if ((_accessMap[i] & RamReadWriteFlags.Write) != 0) countZp++;
        }
        var result = new byte[countZp];
        int index = 0;
        for (int i = 2; i < 256; i++)
        {
            if ((_accessMap[i] & RamReadWriteFlags.Write) != 0)
            {
                result[index++] = _zpRelocations[i].Reloc;
            }
        }
        return result;
    }
    
    /// <summary>
    /// Prints a formatted relocation map of the program to the specified text writer.
    /// </summary>
    /// <remarks>The output includes a visual map of program memory, counts of different relocation types, and
    /// lists of old and new zero-page addresses. This method does not modify program state.</remarks>
    /// <param name="writer">The <see cref="TextWriter"/> to which the relocation map will be written. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if relocation analysis has not been performed. Call <c>Relocate()</c> before invoking this method.</exception>
    public void PrintRelocationMap(TextWriter writer)
    {
        if (!_hasBeenAnalyzed)
        {
            throw new InvalidOperationException($"Relocation has not been analyzed yet. Call {nameof(Relocate)}() first.");
        }

        int nReloc = 0, nZp = 0, nDont = 0, nUnused = 0, nUnknown = 0;

        writer.Write("Program map:");
        ushort org = _programAddress;
        int relocOffset = _lastRelocationTarget.Address - _programAddress;

        // Align down and up to 64 bytes to display a full range
        for (int addr = org & 0xFFC0; addr <= ((org + _programByteStates.Length - 1) | 0x003F); addr++)
        {
            if ((addr & 0x3F) == 0)
            {
                writer.Write($"\n{addr:x4}, {(ushort)(addr + relocOffset) :x4}:  ");
            }

            if (addr < org || addr >= org + _programByteStates.Length)
            {
                writer.Write(" ");
            }
            else
            {
                int i = addr - org;
                ref var pb = ref _programByteStates[i];

                if ((pb.Flags & RamByteFlags.Reloc) != 0)
                {
                    if ((pb.Flags & RamByteFlags.UsedInMsb) != 0)
                    {
                        writer.Write("R");
                        nReloc++;
                    }
                    else if ((pb.Flags & RamByteFlags.UsedInZp) != 0)
                    {
                        writer.Write("Z");
                        nZp++;
                    }
                    else
                    {
                        writer.Write("e"); // internal error
                    }
                }
                else if ((pb.Flags & RamByteFlags.NoReloc) != 0)
                {
                    writer.Write("=");
                    nDont++;
                }
                else if (_accessMap[addr] == RamReadWriteFlags.None)
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
            if ((_accessMap[i] & RamReadWriteFlags.Write) != 0) writer.Write($" {i:x2}");
        }
        writer.WriteLine();

        writer.Write("New zero-page addresses:  ");
        for (int i = 2; i < 256; i++)
        {
            if ((_accessMap[i] & RamReadWriteFlags.Write) != 0) writer.Write($" {_zpRelocations[i].Reloc:x2}");
        }
        writer.WriteLine();
    }

    private void ComputeZeroPageRelocations(RamZpRange zpRange)
    {
        var zp = _zpRelocations;
        if (!EnableZpReloc)
        {
            for (int i = 0; i < 256; i++)
            {
                zp[i].Link = (byte)i;
                zp[i].Reloc = (byte)i;
                zp[i].IsFree = false;
            }
            return;
        }
        
        Debug.Assert(!zpRange.IsEmpty);

        var firstZp = zpRange.Start;
        var lastZp = zpRange.GetEnd();

        // Initialize zero-page relocation array
        for (int i = 0; i < 256; i++)
        {
            zp[i].Link = (byte)i;
            zp[i].IsFree = (i >= firstZp && i <= lastZp);
        }

        // Find program bytes that contribute to multiple zero-page addresses and link them
        for (int i = 0; i < _programByteStates.Length; i++)
        {
            ref var pb = ref _programByteStates[i];
            if ((pb.Flags & (RamByteFlags.Reloc | RamByteFlags.UsedInZp)) != (RamByteFlags.Reloc | RamByteFlags.UsedInZp))
            {
                continue;
            }

            int first = -1;
            for (int j = 0; j < 256; j++)
            {
                if (!pb.ZpAddr!.IsUsed((byte)j))
                {
                    continue;
                }

                if (first != -1)
                {
                    // One relocated program byte contributes to several zero-page addresses. Link them.
                    if (zp[j].Link > zp[first].Link)
                    {
                        zp[j].Link = zp[first].Link;
                    }
                    else
                    {
                        zp[first].Link = zp[j].Link;
                    }
                }
                else
                {
                    first = j;
                }
            }
        }

        // Flatten links (path compression)
        for (int i = 0; i < 256; i++)
        {
            if (zp[i].Link != zp[zp[i].Link].Link)
            {
                zp[i].Link = zp[zp[i].Link].Link;
            }
        }

        // Perform zero-page relocation if enabled
        for (int chunk = 2; chunk < 0x100; chunk++)
        {
            if ((GetAccessMap((ushort)chunk) & RamReadWriteFlags.Write) != 0 && zp[chunk].Link == chunk)
            {
                // We have a chunk to place somewhere
                int dest;
                for (dest = firstZp; dest <= lastZp; dest++)
                {
                    // Try to put it at dest
                    bool fits = true;
                    for (int i = chunk; i < 0x100; i++)
                    {
                        if (zp[i].Link == chunk)
                        {
                            if (!zp[(byte)(dest + i - chunk)].IsFree)
                            {
                                fits = false;
                                break;
                            }
                        }
                    }

                    if (fits)
                    {
                        // It fits!
                        for (int i = chunk; i < 0x100; i++)
                        {
                            if (zp[i].Link == chunk)
                            {
                                zp[i].Reloc = (byte)(dest + i - chunk);
                                zp[(byte)(dest + i - chunk)].IsFree = false;
                            }
                        }
                        break;
                    }
                }

                if (dest > lastZp)
                {
                    throw new InvalidOperationException($"Can't fit all zero-page addresses into specified range (${firstZp:x2}-${lastZp:x2}).");
                }
            }
        }
    }

    private bool ReportBadMemoryAccess()
    {
        bool outOfBounds = false;
        ushort oobStart = 0;
        ushort oobEnd = 0;

        var ramRanges = SafeRamRanges.ToArray().AsSpan();
        ramRanges.Sort();
        
        for (int i = 0; i < 65536; i++)
        {
            var addr = (ushort)i;
            if (addr >= _programAddress && addr < _programAddress + _programBytes.Length)
            {
                // Inside program
            }
            else if (addr >= 2 && addr < 0x100) // ZeroPage
            {
                // A zp address was read from without writing to it
                if (GetAccessMap(addr) == RamReadWriteFlags.Read)
                {
                    Diagnostics.Warning(CodeRelocationDiagnosticId.WRN_ZeroPageReadWithoutWriting, $"Warning: Zero-page address ${i:x2} read but never written.{(EnableZpReloc ? " Not relocating." : "")}");
                    for (int j = 0; j < _programBytes.Length; j++)
                    {
                        ref var pb = ref _programByteStates[j];
                        if ((pb.Flags & RamByteFlags.UsedInZp) != 0 && pb.ZpAddr!.IsUsed((byte)addr))
                        {
                            pb.ZpAddr!.ClearUsed((byte)addr);
                            if (pb.ZpAddr!.IsNotUsed())
                            {
                                pb.Flags &= ~(RamByteFlags.Reloc | RamByteFlags.UsedInZp);
                            }
                        }
                    }
                }
            }
            else if (addr >= 0x200)
            {
                bool isAllowed = false;
                var ramRwFlags = GetAccessMap(addr);
                foreach (var ramRange in ramRanges)
                {
                    if (ramRange.Contains(addr))
                    {
                        isAllowed = (ramRange.Flags & ramRwFlags) != 0;
                        break;
                    }
                }

                if (!isAllowed && (ramRwFlags & RamReadWriteFlags.Write) != 0)
                {
                    outOfBounds = true;
                    if (oobStart == 0)
                    {
                        oobStart = addr;
                    }
                    else if (oobEnd != addr - 1)
                    {
                        ReportMemoryAccessOutOfBounds(oobStart, oobEnd);
                        oobStart = 0;
                    }
                    oobEnd = addr;
                }
            }
        }

        if (oobStart != 0)
        {
            ReportMemoryAccessOutOfBounds(oobStart, oobEnd);
        }

        return outOfBounds;

        void ReportMemoryAccessOutOfBounds(ushort first, ushort last)
        {
            Diagnostics.Warning(CodeRelocationDiagnosticId.WRN_OutOfBounds,
                first == last
                    ? $"Warning: Write out of bounds at address ${first:x4}"
                    : $"Warning: Write out of bounds at address ${first:x4}-${last:x4}"
            );
        }
    }

    private void RelocAlike(byte v1, ProgramSource? sv1, byte v2, ProgramSource? sv2)
    {
        if (v1 >= (RelocationAnalysisStart >> 8) && v1 <= (RelocationAnalysisEnd >> 8) &&
            v2 >= (RelocationAnalysisStart >> 8) && v2 <= (RelocationAnalysisEnd >> 8))
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

            if (sv2 is not null)
            {
                constraint.ProgramOffsets2 ??= new();
                for (var s = sv2; s is not null; s = s.Next)
                {
                    constraint.ProgramOffsets2.Add(s.ProgramOffset);
                }
            }

            ConstraintList? list = null;
            AddOrMergeConstraint(ref list, constraint);
        }
    }

    private void RelocExactlyOne(ProgramSource src, byte zpAddress)
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
                    Diagnostics.Warning(CodeRelocationDiagnosticId.WRN_ByteMultipleContributeCannotBeRelocated, $"Byte at ${_programAddress + s.ProgramOffset:x4} contributes more than once to a sum and won't be relocated.");
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
            ref var programByte = ref _programByteStates[s.ProgramOffset];
            if ((programByte.Flags & RamByteFlags.NoReloc) != 0)
            {
                dontCount++;
            }
            else if ((programByte.Flags & RamByteFlags.Reloc) != 0)
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
                if ((_programByteStates[s.ProgramOffset].Flags & RamByteFlags.NoReloc) == 0)
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
                    var sb = new StringBuilder();
                    sb.Append("Inconsistency: Want to relocate one of {");
                    for (var s = src; s is not null; s = s.Next)
                    {
                        sb.Append($"${_programAddress + s.ProgramOffset:x4}{(s.Next != null ? ", " : "")}");
                    }

                    sb.Append("} but this would contradict other equations.");
                    Diagnostics.Error(CodeRelocationDiagnosticId.ERR_RelocationInconsistency, sb.ToString());
                    throw new CodeRelocationException(CodeRelocationDiagnosticId.ERR_RelocationInconsistency, "Constraint inconsistency. Check Diagnostics.");
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
                        if ((_programByteStates[s.ProgramOffset].Flags & (RamByteFlags.Reloc | RamByteFlags.NoReloc)) == 0)
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
        for(var it = constraints; it is not null; it = it.Next)
        {
            var existing = it.Constraint;
            if (existing is not null && existing.Equals(constraint))
            {
                // Duplicate, don't add
                return;
            }
        }

        if (Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Debug)
        {
            LogConstraint(constraint);
        }

        if (constraints.Constraint is null)
        {
            Debug.Assert(constraints.Next is null);
            constraints.Constraint = constraint;
        }
        else
        {
            var newNode = new ConstraintList
            {
                Constraint = constraint,
                Next = constraints
            };
            constraints = newNode;
        }
    }

    private void CheckRelocRange(ushort addr, ProgramSource? lsb1, ProgramSource? lsb2, ProgramSource? msb)
    {
        if (msb is null) return;

        for (var s = msb; s != null; s = s.Next)
        {
            _programByteStates[s.ProgramOffset].Flags |= RamByteFlags.UsedInMsb;
        }

        if (addr >= RelocationAnalysisStart && addr <= RelocationAnalysisEnd)
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
            _programByteStates[s.ProgramOffset].SetUsedInZp(zpAddr);

        for (var s = src2; s is not null; s = s.Next)
            _programByteStates[s.ProgramOffset].SetUsedInZp(zpAddr);

        if (EnableZpReloc && src1 is not null)
        {
            var list = src1;
            for (var s = src2; s is not null; s = s.Next) list = CreateSourceAtProgramByteOffset(s.ProgramOffset, list);
            RelocExactlyOne(list, zpAddr);
        }
    }

    [return: NotNullIfNotNull(nameof(next))]
    private ProgramSource? CreateSourceAtProgramByteOffset(ushort offset, ProgramSource? next)
    {
        // Don't track an offset that is marked as NoReloc
        if ((_programByteStates[offset].Flags & RamByteFlags.NoReloc) != 0)
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
                if ((_accessMap[i] & RamReadWriteFlags.Write) != 0)
                {
                    Debug.Assert(constraints is not null);
                    for (var it = constraints; it is not null; it = it.Next)
                    {
                        var constraint = it.Constraint!;
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
            for(var it = constraints; it is not null; it = it.Next)
            {
                var constraint = it.Constraint!;
                AddConstraintToProgramBytes(constraint, constraint.ProgramOffsets1);
                if (constraint.ProgramOffsets2 is not null)
                {
                    AddConstraintToProgramBytes(constraint, constraint.ProgramOffsets2);
                }
            }
        }
    }

    private void AddConstraintToProgramBytes(Constraint constraint, List<ushort> offsets)
    {
        var prog = _programByteStates;
        foreach (var offset in CollectionsMarshal.AsSpan(offsets))
        {
            ref var previousConstraint = ref prog[offset].ConstraintChain;
            previousConstraint = new ConstraintList()
            {
                Constraint = constraint,
                Next = previousConstraint
            };
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
                ref var pb = ref _programByteStates[offset];

                if ((pb.Flags & RamByteFlags.Reloc) != 0)
                {
                    relocCount++;
                    lastRelocIndex = i;
                }
                else if ((pb.Flags & RamByteFlags.NoReloc) != 0)
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
            var pgs = _programByteStates;
            foreach (var offset in constraint.ProgramOffsets1)
            {
                if ((pgs[offset].Flags & RamByteFlags.Reloc) != 0)
                {
                    n1Do++;
                }
            }

            if (constraint.ProgramOffsets2 is not null)
            {
                foreach (var offset in constraint.ProgramOffsets2)
                {
                    if ((pgs[offset].Flags & RamByteFlags.Reloc) != 0)
                    {
                        n2Do++;
                    }
                }
            }

            if (n1Do > 1 || n2Do > 1) return true;
            return n1Do != n2Do;
        }

        return true;
    }

    private bool CheckTrivialInconsistency()
    {
        for (ushort i = 0; i < _programByteStates.Length; i++)
        {
            ref var pb = ref _programByteStates[i];

            // If a byte contributes to both a zero-page address and an msb, it cannot be relocatable
            if ((pb.Flags & RamByteFlags.UsedInZp) != 0 && (pb.Flags & RamByteFlags.UsedInMsb) != 0)
            {
                pb.Flags |= RamByteFlags.NoReloc;
            }

            if ((pb.Flags & RamByteFlags.Reloc) != 0 && (pb.Flags & RamByteFlags.NoReloc) != 0)
            {
                Diagnostics.Error(CodeRelocationDiagnosticId.ERR_RelocationInconsistency, $"Inconsistency detected! Byte at ${_programAddress + i:x4} can't be both relocated and not relocated at the same time.");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recursive backtracking solver for relocation constraints.
    ///
    /// THIS RESOLVED IS NOT USED BUT IS KEPT FOR REFERENCE. See <see cref="HeapSolver"/> for the current solver.
    /// </summary>
    private bool RecursiveSolver()
    {
        bool done;
        do
        {
            done = true;
            foreach (var constraints in _allConstraints)
            {
                for (var it = constraints; it is not null; it = it.Next)
                {
                    var constraint = it.Constraint!;
                    if (constraint.CheckNeeded)
                    {
                        if (TryPropagateConstraint(constraint))
                        {
                            return true;
                        }
                        done = false;
                    }
                }
            }
        } while (!done);


        foreach (var constraints in _allConstraints)
        {
            for (var it = constraints; it is not null; it = it.Next)
            {
                var constraint = it.Constraint!;
                var prog = _programByteStates;
                if (constraint.TryFindUnresolvedOffset(prog, out var offset))
                {
                    var backtrack = CreateBackTrackArray();
                    if (EnforceNoReloc(offset) || RecursiveSolver())
                    {
                        // Restore state
                        for (int i = 0; i < prog.Length; i++)
                        {
                            prog[i].Flags = backtrack![i];
                        }

                        _backtrackPool.Push(backtrack);

                        return EnforceReloc(offset) || RecursiveSolver();
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Heap version of the <see cref="RecursiveSolver"/> to avoid stack overflows on large programs.
    /// </summary>
    /// <returns><c>true</c> if inconsistency detected, <c>false</c> on success.</returns>
    private bool HeapSolver()
    {
        // Clear it as it depends on the program size
        _backtrackPool.Clear();

        var stack = new Stack<SolverStackFrame>();
        var initFrame = CreateFrame();
        initFrame.State = SolverState.PropagateConstraints;
        stack.Push(initFrame);

        while (stack.Count > 0)
        {
            //Console.WriteLine($"StackSize {stack.Count}");
            //Console.WriteLine($"HeapResolver Stack: {stack.Count}");
            var frame = stack.Pop();

            switch (frame.State)
            {
                case SolverState.PropagateConstraints:
                {
                    // Propagate constraints
                    bool inconsistency = false;
                    while (true)
                    {
                        var done = true;
                        foreach (var constraints in _allConstraints)
                        {
                            for(var it = constraints; it is not null; it = it.Next)
                            {
                                var constraint = it.Constraint!;
                                if (constraint.CheckNeeded)
                                {
                                    if (TryPropagateConstraint(constraint))
                                    {
                                        inconsistency = true;
                                        break;
                                    }

                                    done = false;
                                }
                            }

                            if (inconsistency) break;
                        }

                        if (inconsistency || done) break;
                    }

                    if (inconsistency)
                    {
                        // Return false (inconsistency) - continue with parent frame
                        if (frame.ParentFrame != null)
                        {
                            frame.ParentFrame.ChildReturnedInconsistency = true;
                            stack.Push(frame.ParentFrame);
                        }
                        continue;
                    }

                    // Search for an offset to decide
                    bool foundOffset = false;
                    ushort offset = 0;
                    var prog = _programByteStates;
                    foreach (var constraints in _allConstraints)
                    {
                        for(var it = constraints; it is not null; it = it.Next)
                        {
                            var constraint = it.Constraint!;
                            if (constraint.TryFindUnresolvedOffset(prog, out offset))
                            {
                                foundOffset = true;
                                goto FoundOffset;
                            }
                        }
                    }
                    FoundOffset:

                    if (foundOffset)
                    {
                        // Found an undecided offset - create backtracking point
                        var backtrack = CreateBackTrackArray();
                        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                        {
                            Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverShouldNotBeRelocated, $"Guessing that ${_programAddress + offset:x4} should not be relocated.");
                        }

                        // Create a frame to handle the result of trying NoReloc first
                        var afterNoRelocFrame = CreateFrame();
                        afterNoRelocFrame.State = SolverState.AfterNoRelocAttempt;
                        afterNoRelocFrame.Offset = offset;
                        afterNoRelocFrame.Backtrack = backtrack;
                        afterNoRelocFrame.ParentFrame = frame.ParentFrame;

                        stack.Push(afterNoRelocFrame);

                        // Try NoReloc path
                        if (EnforceNoReloc(offset))
                        {
                            // Immediate inconsistency
                            afterNoRelocFrame.ChildReturnedInconsistency = true;
                        }
                        else
                        {
                            // Push a new propagation frame
                            var propagationFrame = CreateFrame();
                            propagationFrame.State = SolverState.PropagateConstraints;
                            propagationFrame.ParentFrame = afterNoRelocFrame;
                            stack.Push(propagationFrame);
                        }
                    }
                    else
                    {
                        // No more variables to decide - success (return false)
                        if (frame.ParentFrame != null)
                        {
                            frame.ParentFrame.ChildReturnedSuccess = true;
                            stack.Push(frame.ParentFrame);
                        }
                    }

                    break;
                }

                case SolverState.AfterNoRelocAttempt:
                {
                    if (frame.ChildReturnedInconsistency)
                    {
                        // NoReloc path failed, try Reloc path
                        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                        {
                            Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverBackTracking, "Backtracking.");
                        }

                        // Restore state
                        var pgs = _programByteStates;
                        var backtrack = frame.Backtrack!;
                        for (ushort i = 0; i < pgs.Length; i++)
                        {
                            pgs[i].Flags = backtrack[i];
                        }

                        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                        {
                            Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverShouldBeRelocated, $"Assuming that ${_programAddress + frame.Offset:x4} should be relocated.");
                        }

                        // Try Reloc path
                        if (EnforceReloc(frame.Offset))
                        {
                            // Immediate inconsistency - propagate to parent
                            if (frame.ParentFrame != null)
                            {
                                frame.ParentFrame.ChildReturnedInconsistency = true;
                                stack.Push(frame.ParentFrame);
                            }
                        }
                        else
                        {
                            // Push a new propagation frame
                            var propagationFrame = CreateFrame();
                            propagationFrame.State = SolverState.PropagateConstraints;
                            propagationFrame.ParentFrame = frame.ParentFrame;
                            stack.Push(propagationFrame);
                        }
                    }
                    else if (frame.ChildReturnedSuccess)
                    {
                        // NoReloc path succeeded - return success (false)
                        if (frame.ParentFrame != null)
                        {
                            frame.ParentFrame.ChildReturnedSuccess = true;
                            stack.Push(frame.ParentFrame);
                        }
                    }
                    else
                    {
                        // This shouldn't happen in normal flow
                        if (frame.ParentFrame != null)
                        {
                            frame.ParentFrame.ChildReturnedInconsistency = true;
                            stack.Push(frame.ParentFrame);
                        }
                    }
                    break;
                }
            }

            ReleaseFrame(frame);
        }

        return false; // Success
    }

    private enum SolverState
    {
        PropagateConstraints,
        AfterNoRelocAttempt
    }

    private RamByteFlags[] CreateBackTrackArray()
    {
        var pg = _programByteStates;
        var backtrack = _backtrackPool.Count > 0 ? _backtrackPool.Pop() : GC.AllocateUninitializedArray<RamByteFlags>(pg.Length);
        for (ushort i = 0; i < pg.Length; i++)
        {
            backtrack[i] = pg[i].Flags;
        }
        return backtrack;
    }

    private SolverStackFrame CreateFrame()
    {
        return _solverStackFramePool.Count > 0 ? _solverStackFramePool.Pop() : new SolverStackFrame(); 
    }

    private void ReleaseFrame(SolverStackFrame frame)
    {
        if (frame.Backtrack is not null)
            _backtrackPool.Push(frame.Backtrack);

        frame.ParentFrame = null;
        frame.Backtrack = null;
        _solverStackFramePool.Push(frame);
    }
    
    private class SolverStackFrame
    {
        public SolverStackFrame()
        {
        }

        public SolverState State;
        public ushort Offset;
        public RamByteFlags[]? Backtrack;
        public SolverStackFrame? ParentFrame;
        public bool ChildReturnedInconsistency;
        public bool ChildReturnedSuccess;
    }


    private bool EnforceNoReloc(ushort offset)
    {
        ref var pb = ref _programByteStates[offset];
        var flags = pb.Flags;
        if ((flags & RamByteFlags.Reloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((flags & RamByteFlags.NoReloc) == 0)
        {
            pb.Flags = flags | RamByteFlags.NoReloc;

            for(var it = pb.ConstraintChain; it is not null; it = it.Next)
            {
                it.Constraint!.CheckNeeded = true;
            }
        }

        return false;
    }

    private bool EnforceReloc(ushort offset)
    {
        ref var pb = ref _programByteStates[offset];
        var flags = pb.Flags;
        if ((flags & RamByteFlags.NoReloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((flags & RamByteFlags.Reloc) == 0)
        {
            pb.Flags = flags | RamByteFlags.Reloc;

            for (var it = pb.ConstraintChain; it is not null; it = it.Next)
            {
                it.Constraint!.CheckNeeded = true;
            }
        }

        return false;
    }

    private void LogConstraint(Constraint constraint)
    {
        var sb = new StringBuilder();
        sb.Append("Adding constraint: ");
        if (constraint.Kind == ConstraintKind.ExactlyOne)
        {
            sb.Append("Exactly one reloc: {");
            for (int i = 0; i < constraint.ProgramOffsets1.Count; i++)
            {
                sb.Append($"{(i > 0 ? ", " : "")}${_programAddress + constraint.ProgramOffsets1[i]:x4}");
            }
            sb.Append("}");
        }
        else if (constraint.Kind == ConstraintKind.Alike)
        {
            sb.Append("Reloc alike: {");
            for (int i = 0; i < constraint.ProgramOffsets1.Count; i++)
            {
                sb.Append($"{(i > 0 ? ", " : "")}${_programAddress + constraint.ProgramOffsets1[i]:x4}");
            }
            sb.Append("}, {");
            if (constraint.ProgramOffsets2 is not null)
            {
                for (int i = 0; i < constraint.ProgramOffsets2.Count; i++)
                {
                    sb.Append($"{(i > 0 ? ", " : "")}${_programAddress + constraint.ProgramOffsets2[i]:x4}");
                }
            }
            sb.Append("}");
        }
        else
        {
            sb.Append("Unknown constraint!");
        }
        Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverConstraint, sb.ToString());
    }

    private void NoRelocAt(ushort offset) => _programByteStates[offset].Flags |= RamByteFlags.NoReloc;

    private void RelocAt(ushort offset) => _programByteStates[offset].Flags |= RamByteFlags.Reloc;

    private void NoReloc(ProgramSource? src)
    {
        while (src != null)
        {
            NoRelocAt(src.ProgramOffset);
            src = src.Next;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref ProgramSource? GetRamProgramSource(ushort addr) => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_ramProgramSources), addr);

    private class ProgramSource
    {
        public ProgramSource? Next;

        public ushort ProgramOffset;
    }

    private struct ProgramByteState()
    {
        public RamByteFlags Flags;

        public ConstraintList? ConstraintChain;

        public ZpBitmap? ZpAddr;

        public void SetUsedInZp(byte zpAddr)
        {
            Flags |= RamByteFlags.UsedInZp;
            ZpAddr ??= new ZpBitmap();
            ZpAddr.SetUsed(zpAddr);
        }
    }

    [DebuggerTypeProxy(typeof(ZpBitmapDebugView))]
    private class ZpBitmap
    {
        private ZpBitmapArray _data;

        public bool IsUsed(byte zpAddr) => (_data[zpAddr >> 3] & (1 << (zpAddr & 7))) != 0;

        public void SetUsed(byte zpAddr) => _data[zpAddr >> 3] |= (byte)(1 << (zpAddr & 7));

        public void ClearUsed(byte zpAddr) => _data[zpAddr >> 3] &= (byte)~(1 << (zpAddr & 7));

        public bool IsNotUsed()
        {
            Span<byte> span = _data;
            return span.IndexOfAnyExcept((byte)0) < 0;
        }

        public void Clear()
        {
            Span<byte> span = _data;
            span.Clear();
        }
        
        [InlineArray(32)]
        private struct ZpBitmapArray
        {
            private byte _e;
        }

        private class ZpBitmapDebugView
        {
            private readonly ZpBitmap _bitmap;
            public ZpBitmapDebugView(ZpBitmap bitmap)
            {
                _bitmap = bitmap;
            }
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public byte[] UsedZpAddresses
            {
                get
                {
                    var list = new List<byte>();
                    for (int i = 0; i < 256; i++)
                    {
                        if (_bitmap.IsUsed((byte)i))
                        {
                            list.Add((byte)i);
                        }
                    }
                    return list.ToArray();
                }
            }
        }


    }

    private class ConstraintList
    {
        public Constraint? Constraint;
        public ConstraintList? Next;
    }

    [DebuggerDisplay("Check = {CheckNeeded}, Kind = {Kind}, Offsets1 = {ProgramOffsets1.Count}, Offsets1 = {ProgramOffsets2.Count}")]
    private class Constraint : IEquatable<Constraint>
    {
        public bool CheckNeeded { get; set; }

        public ConstraintKind Kind { get; set; }

        public readonly List<ushort> ProgramOffsets1 = new();

        public List<ushort>? ProgramOffsets2 { get; set; }

        public void SortOffsets()
        {
            if (ProgramOffsets1.Count > 1) CollectionsMarshal.AsSpan(ProgramOffsets1).Sort();
            if (ProgramOffsets2 is not null && ProgramOffsets2.Count > 1) CollectionsMarshal.AsSpan(ProgramOffsets2).Sort();
        }

        public bool TryFindUnresolvedOffset(ProgramByteState[] prog, out ushort offset)
        {
            foreach(var localOffset in CollectionsMarshal.AsSpan(ProgramOffsets1))
            {
                if ((prog[localOffset].Flags & (RamByteFlags.Reloc | RamByteFlags.NoReloc)) == 0)
                {
                    offset = localOffset;
                    return true;
                }
            }

            if (ProgramOffsets2 is not null)
            {
                foreach (var localOffset in CollectionsMarshal.AsSpan(ProgramOffsets2))
                {
                    if ((prog[localOffset].Flags & (RamByteFlags.Reloc | RamByteFlags.NoReloc)) == 0)
                    {
                        offset = localOffset;
                        return true;
                    }
                }
            }

            offset = 0;
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var offset in CollectionsMarshal.AsSpan(ProgramOffsets1))
            {
                hash.Add(offset);
            }

            if (ProgramOffsets2 is not null)
            {
                foreach (var offset in CollectionsMarshal.AsSpan(ProgramOffsets2))
                {
                    hash.Add(offset);
                }
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

        public static bool operator ==(Constraint? left, Constraint? right) => Equals(left, right);

        public static bool operator !=(Constraint? left, Constraint? right) => !Equals(left, right);
    }

    private enum ConstraintKind
    {
        None,
        ExactlyOne,
        Alike,
    }

    private record struct ZeroPageReloc(byte Link, bool IsFree)
    {
        public byte Reloc { get; set; }
    }
}