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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Asm6502.Relocator;

partial class CodeRelocator
{
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

        if (relocationTarget.Address < 0x200)
        {
            throw new ArgumentException("Relocation target address must be >= 0x200", nameof(relocationTarget));
        }

        // In practice, it should always be 0x00 (but we allow other values, as long as they are the same)
        if ((byte)relocationTarget.Address != (byte)RelocationAnalysisStart)
        {
            throw new ArgumentException($"Relocation target address low byte must be equal to RelocationStart low byte (${RelocationAnalysisStart:x4}). Actual: ${relocationTarget.Address:x4}", nameof(relocationTarget));
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

            if (RecursiveSolver())
            {
                throw new CodeRelocationException(CodeRelocationDiagnosticId.ERR_NoSolutionFound, "No solution found. Check Diagnostics.");
            }
        }

        ComputeZeroPageRelocations(relocationTarget.ZpRange);

        var newProgramBytes = new byte[_programBytes.Length];
        _programBytes.AsSpan().CopyTo(newProgramBytes);

        // Apply relocations
        var msb = (byte)(relocationTarget.Address >> 8);
        for (int i = 0; i < newProgramBytes.Length; i++)
        {
            ref var b = ref newProgramBytes[i];
            ref var pb = ref _programByteInfos[i];
            if ((pb.Flags & RamByteFlags.Reloc) == 0)
            {
                continue;
            }

            if((pb.Flags & RamByteFlags.UsedInMsb) != 0)
            {
                b = msb;
            }
            else if (EnableZpReloc && (pb.Flags & RamByteFlags.UsedInZp) != 0)
            {
                int j;
                for(j = 2; j< 256; j++) {
                    if(pb.ZpAddr.IsUsed((byte)j)) {
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
        ushort targetOrg = _lastRelocationTarget.Address;

        // Align down and up to 64 bytes to display a full range
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
        for (int i = 0; i < _programByteInfos.Length; i++)
        {
            ref var pb = ref _programByteInfos[i];
            if ((pb.Flags & (RamByteFlags.Reloc | RamByteFlags.UsedInZp)) != (RamByteFlags.Reloc | RamByteFlags.UsedInZp))
            {
                continue;
            }

            int first = -1;
            for (int j = 0; j < 256; j++)
            {
                if (!pb.ZpAddr.IsUsed((byte)j))
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
            else if (addr >= 0xd400 && addr <= 0xd41f) // SID registers: TODO make it configurable
            {
                // Ignore
            }
            else if (addr >= 2 && addr < 0x100) // ZeroPage
            {
                // A zp address was read from without writing to it
                if (GetAccessMap(addr) == RamReadWriteFlags.Read)
                {
                    // TODO: Log read only from zero-page without writing
                    for (int j = 0; j < _programBytes.Length; j++)
                    {
                        ref var pb = ref _programByteInfos[j];
                        if ((pb.Flags & RamByteFlags.UsedInZp) != 0 && pb.ZpAddr.IsUsed((byte)addr))
                        {
                            pb.ZpAddr.ClearUsed((byte)addr);
                            if (pb.ZpAddr.IsNotUsed())
                            {
                                pb.Flags &= ~(RamByteFlags.Reloc | RamByteFlags.UsedInZp);
                            }
                        }
                    }
                }
            }
            else if (addr >= 0x200) // Ignore stack (TODO: should we ignore also vectors?)
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
            if (first == last)
            {
                Diagnostics.Warning(CodeRelocationDiagnosticId.WRN_OutOfBounds, $"Warning: Write out of bounds at address ${first:x4}");
            }
            else
            {
                Diagnostics.Warning(CodeRelocationDiagnosticId.WRN_OutOfBounds, $"Warning: Write out of bounds at address ${first:x4}-${last:x4}");
            }
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
            ref var programByte = ref _programByteInfos[s.ProgramOffset];
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
                if ((_programByteInfos[s.ProgramOffset].Flags & RamByteFlags.NoReloc) == 0)
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
                        if ((_programByteInfos[s.ProgramOffset].Flags & (RamByteFlags.Reloc | RamByteFlags.NoReloc)) == 0)
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

        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
        {
            LogConstraints(constraint);
        }

        constraints.Add(constraint);
    }

    private void CheckRelocRange(ushort addr, ProgramSource? lsb1, ProgramSource? lsb2, ProgramSource? msb)
    {
        Debug.Assert(msb is not null);

        for (var s = msb; s != null; s = s.Next)
        {
            _programByteInfos[s.ProgramOffset].Flags |= RamByteFlags.UsedInMsb;
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
        if ((_programByteInfos[offset].Flags & RamByteFlags.NoReloc) != 0)
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

            foreach (var offset in constraint.ProgramOffsets1)
            {
                if ((_programByteInfos[offset].Flags & RamByteFlags.Reloc) != 0)
                {
                    n1Do++;
                }
            }

            foreach (var offset in constraint.ProgramOffsets2)
            {
                if ((_programByteInfos[offset].Flags & RamByteFlags.Reloc) != 0)
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
                    if ((_programByteInfos[offset].Flags & (RamByteFlags.Reloc | RamByteFlags.NoReloc)) != 0)
                    {
                        continue;
                    }

                    // Backtrack: save state
                    var backtrack = new RamByteFlags[_programByteInfos.Length];
                    for (ushort i = 0; i < _programByteInfos.Length; i++)
                    {
                        backtrack[i] = _programByteInfos[i].Flags;
                    }

                    if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                    {
                        Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverShouldNotBeRelocated, $"Guessing that ${_programAddress + offset:x4} should not be relocated.");
                    }

                    if (EnforceNoReloc(offset) || RecursiveSolver())
                    {
                        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                        {
                            Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverBackTracking, "Backtracking.");
                        }
                            
                        // Restore state
                        for (ushort i = 0; i < _programByteInfos.Length; i++)
                        {
                            _programByteInfos[i].Flags = backtrack[i];
                        }

                        if (Diagnostics.LogLevel == CodeRelocationDiagnosticKind.Trace)
                        {
                            Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverShouldBeRelocated, $"Assuming that ${_programAddress + offset:X4} should be relocated.");
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

        if ((pb.Flags & RamByteFlags.Reloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((pb.Flags & RamByteFlags.NoReloc) == 0)
        {
            pb.Flags |= RamByteFlags.NoReloc;

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

        if ((pb.Flags & RamByteFlags.NoReloc) != 0)
        {
            return true; // Inconsistency
        }
        else if ((pb.Flags & RamByteFlags.Reloc) == 0)
        {
            pb.Flags |= RamByteFlags.Reloc;

            foreach (var constraint in pb.Constraints)
            {
                constraint.CheckNeeded = true;
            }
        }

        return false;
    }

    private void LogConstraints(Constraint constraint)
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
            for (int i = 0; i < constraint.ProgramOffsets2.Count; i++)
            {
                sb.Append($"{(i > 0 ? ", " : "")}${_programAddress + constraint.ProgramOffsets2[i]:x4}");
            }
            sb.Append("}");
        }
        else
        {
            sb.Append("Unknown constraint!");
        }
        Diagnostics.Trace(CodeRelocationDiagnosticId.TRC_SolverConstraint, sb.ToString());
    }

    private void NoRelocAt(ushort offset) => _programByteInfos[offset].Flags |= RamByteFlags.NoReloc;

    private void RelocAt(ushort offset) => _programByteInfos[offset].Flags |= RamByteFlags.Reloc;

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

        public readonly ConstraintList Constraints = new();

        public ZpBitmap ZpAddr;

        public void SetUsedInZp(byte zpAddr)
        {
            Flags |= RamByteFlags.UsedInZp;
            ZpAddr.SetUsed(zpAddr);
        }
    }

    [InlineArray(32)]
    private struct ZpBitmap
    {
        private byte _e;

        public bool IsUsed(byte zpAddr) => (this[zpAddr >> 3] & (1 << (zpAddr & 7))) != 0;

        public void SetUsed(byte zpAddr) => this[zpAddr >> 3] |= (byte)(1 << (zpAddr & 7));

        public void ClearUsed(byte zpAddr) => this[zpAddr >> 3] &= (byte)~(1 << (zpAddr & 7));

        public bool IsNotUsed()
        {
            Span<byte> span = this;
            return span.IndexOfAnyExcept((byte)0) < 0;
        }

        public void Clear()
        {
            Span<byte> span = this;
            span.Clear();
        }
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

    private record struct ZeroPageReloc(byte Link, bool IsFree)
    {
        public byte Reloc { get; set; }
    }
}