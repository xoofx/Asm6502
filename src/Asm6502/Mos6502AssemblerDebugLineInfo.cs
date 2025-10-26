// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// A record to hold debug information for a 6502 assembler instruction.
/// </summary>
/// <param name="Address">The address of the instruction.</param>
/// <param name="Kind">The kind of debug information.</param>
/// <param name="Name">The file name where the instruction is defined when the <paramref name="Kind"/> is <see cref="Mos6502AssemblerDebugInfoKind.LineInfo"/>. Otherwise, the name of the origin, code, or data section. Will be null for end event.</param>
/// <param name="LineNumber">The line number in the file where the instruction is defined.</param>
public record Mos6502AssemblerDebugInfo(ushort Address, Mos6502AssemblerDebugInfoKind Kind, string? Name = null, int? LineNumber = null)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Kind switch
        {
            Mos6502AssemblerDebugInfoKind.OriginBegin => $"${Address:x4} ORG BEGIN: {Name}",
            Mos6502AssemblerDebugInfoKind.LineInfo => $"${Address:x4} LINE: {Name}:{LineNumber}",
            Mos6502AssemblerDebugInfoKind.CodeSectionBegin => $"${Address:x4} CODE SECTION BEGIN: {Name}",
            Mos6502AssemblerDebugInfoKind.CodeSectionEnd => $"${Address:x4} CODE SECTION END",
            Mos6502AssemblerDebugInfoKind.DataSectionBegin => $"${Address:x4} DATA SECTION BEGIN: {Name}",
            Mos6502AssemblerDebugInfoKind.DataSectionEnd => $"${Address:x4} DATA SECTION END",
            Mos6502AssemblerDebugInfoKind.End => $"${Address:x4} END",
            _ => $"{Address:X4} UNKNOWN KIND"
        };
    }
}