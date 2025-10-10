// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

#pragma warning disable CS1591
namespace Asm6502;

/// <summary>
/// Bit flags of the MOS 6502 CPU status register (P) used by the <see cref="Mos6502Cpu"/> class.
/// </summary>
/// <remarks>
/// These values map to the individual bits of the 8-bit status register and can be combined as a bitmask.
/// </remarks>
[Flags]
public enum Mos6502CpuFlags : byte
{
    /// <summary>
    /// Carry flag (C). Set when an addition produces a carry out, or a subtraction requires a borrow; also affected by shifts/rotates.
    /// </summary>
    C = 1 << 0,

    /// <summary>
    /// Zero flag (Z). Set when the result of an operation is zero.
    /// </summary>
    Z = 1 << 1,

    /// <summary>
    /// Interrupt Disable flag (I). When set, masks IRQ interrupts (NMI is not affected).
    /// </summary>
    I = 1 << 2,

    /// <summary>
    /// Decimal Mode flag (D). When set, ADC/SBC use BCD arithmetic on 6502 variants that support it.
    /// </summary>
    D = 1 << 3,

    /// <summary>
    /// Break flag (B). Appears set when BRK or PHP pushes the status to the stack; not stored internally as a persistent flag.
    /// </summary>
    B = 1 << 4,

    /// <summary>
    /// Unused/reserved (U). Typically forced to 1 when the status is pushed to the stack.
    /// </summary>
    U = 1 << 5,

    /// <summary>
    /// Overflow flag (V). Set when a signed overflow occurs in arithmetic operations.
    /// </summary>
    V = 1 << 6,

    /// <summary>
    /// Negative flag (N). Reflects bit 7 of the result (sign bit).
    /// </summary>
    N = 1 << 7,
}