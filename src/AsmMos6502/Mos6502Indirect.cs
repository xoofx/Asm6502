// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect ZeroPage operation.
/// </summary>
/// <param name="ZpAddress">The zero-page address (low byte) used as the base for the Indirect operation.</param>
public readonly record struct Mos6502Indirect(byte ZpAddress);

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect ZeroPage operation.
/// </summary>
/// <param name="ZpLabel">The zero-page address (low byte) used as the base for the Indirect operation.</param>
public readonly record struct Mos6502IndirectLabel(Mos6502Label ZpLabel);

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect Indexed (X) operation.
/// </summary>
/// <remarks>This addressing mode uses the provided zero-page address as the base, adds the X register value, and
/// dereferences the resulting address to obtain the effective memory location.</remarks>
/// <param name="ZpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
public readonly record struct Mos6502IndirectX(byte ZpAddress);

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect Indexed (X) operation.
/// </summary>
/// <remarks>This addressing mode uses the provided zero-page address as the base, adds the X register value, and
/// dereferences the resulting address to obtain the effective memory location.</remarks>
/// <param name="ZpLabel">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
public readonly record struct Mos6502IndirectLabelX(Mos6502Label ZpLabel);

/// <summary>
/// A factory class for creating instances of <see cref="Mos6502Indirect"/> and <see cref="Mos6502IndirectX"/> addressing modes.
/// </summary>
public struct Mos6502IndirectFactory
{
    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502Indirect"/> using a zero-page address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte).</param>
    /// <returns>An indirect ZeroPage address</returns>
    public Mos6502Indirect this[byte zpAddress] => new(zpAddress);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (X) address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
    /// <param name="x">The X register</param>
    /// <returns>An indirect ZeroPage address Indexed by (X)</returns>
    public Mos6502IndirectX this[byte zpAddress, Mos6502RegisterX x] => new(zpAddress);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502Indirect"/> using a zero-page address.
    /// </summary>
    /// <param name="zpLabel">The zero-page address (low byte).</param>
    /// <returns>An indirect ZeroPage address</returns>
    public Mos6502IndirectLabel this[Mos6502Label zpLabel] => new(zpLabel);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (X) address.
    /// </summary>
    /// <param name="zpLabel">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
    /// <param name="x">The X register</param>
    /// <returns>An indirect ZeroPage address Indexed by (X)</returns>
    public Mos6502IndirectLabelX this[Mos6502Label zpLabel, Mos6502RegisterX x] => new(zpLabel);
}