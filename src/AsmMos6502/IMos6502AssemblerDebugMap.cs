// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

/// <summary>
/// Provides methods for tracking debug information during MOS 6502 assembly. Set via <see cref="Mos6502AssemblerBase.DebugMap"/>.
/// Implementations can log program start/end addresses and per-line debug info.
/// </summary>
public interface IMos6502AssemblerDebugMap
{
    /// <summary>
    /// Signals the start of a program at the specified address.
    /// </summary>
    /// <param name="address">The starting address of the program.</param>
    void BeginProgram(ushort address);

    /// <summary>
    /// Logs debug information for a single source line.
    /// </summary>
    /// <param name="debugLineInfo">The debug line information to log.</param>
    void LogDebugLineInfo(Mos6502AssemblerDebugLineInfo debugLineInfo);

    /// <summary>
    /// Signals the end of a program at the specified address.
    /// </summary>
    /// <param name="address">The ending address of the program.</param>
    void EndProgram(ushort address);
}