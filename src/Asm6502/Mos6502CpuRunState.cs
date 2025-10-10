// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Represents the current micro-cycle phase of the <see cref="Mos6502Cpu"/>.
/// </summary>
/// <remarks>
/// This simplified state machine drives the emulator execution loop:
/// - Fetch: read opcode byte at the program counter (PC).
/// - Load: read additional operand bytes and/or compute the effective address.
/// - Execute: perform the instruction operation and any writes, then advance to the next instruction.
/// </remarks>
public enum Mos6502CpuRunState
{
    /// <summary>
    /// Fetches the next opcode byte from memory at the program counter (PC) and advances the PC.
    /// </summary>
    Fetch,

    /// <summary>
    /// Loads operand byte(s) and/or calculates the effective address required by the instruction.
    /// </summary>
    Load,

    /// <summary>
    /// Executes the instruction, performing required reads/writes and updating CPU state.
    /// </summary>
    Execute
}