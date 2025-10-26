// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Provides methods for tracking debug information during MOS 6502 assembly. Set via <see cref="Mos6502AssemblerBase.DebugMap"/>.
/// Implementations can log program start/end addresses and per-line debug info.
/// </summary>
public interface IMos6502AssemblerDebugMap
{
    /// <summary>
    /// Logs detailed debugging information for the assembler process.
    /// </summary>
    /// <remarks>Use this method to record diagnostic data that can assist in troubleshooting or analyzing the
    /// assembler's behavior. The information provided may include source mappings, symbol tables, or other relevant
    /// details depending on the implementation.</remarks>
    /// <param name="debugInfo">The debugging information to be logged. Must not be null.</param>
    void LogDebugInfo(Mos6502AssemblerDebugInfo debugInfo);
}