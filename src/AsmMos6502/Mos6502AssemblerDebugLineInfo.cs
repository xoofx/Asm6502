// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

/// <summary>
/// A record to hold debug information for a 6502 assembler instruction.
/// </summary>
/// <param name="Address">The address of the instruction.</param>
/// <param name="FileName">The file name where the instruction is defined.</param>
/// <param name="LineNumber">The line number in the file where the instruction is defined.</param>
public record Mos6502AssemblerDebugLineInfo(ushort Address, string FileName, int LineNumber)
{
    /// <inheritdoc />
    public override string ToString() => $"{Address:X4} {FileName}:{LineNumber}";
}