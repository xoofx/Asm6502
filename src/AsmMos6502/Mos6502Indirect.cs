// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502.Expressions;

namespace AsmMos6502;

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect operation.
/// </summary>
/// <param name="Address">The 16-bit address.</param>
public readonly record struct Mos6502Indirect(ushort Address);

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect Indexed (X) operation.
/// </summary>
/// <remarks>This addressing mode uses the provided zero-page address as the base, adds the X register value, and
/// dereferences the resulting address to obtain the effective memory location.</remarks>
/// <param name="Address">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
public readonly record struct Mos6502IndirectX(byte Address)
{
    /// <summary>
    /// Explicit conversion from byte to <see cref="Mos6502IndirectX"/>.
    /// </summary>
    /// <param name="value">The byte value representing the zero-page address.</param>
    public static explicit operator Mos6502IndirectX(byte value) => new(value);
}

/// <summary>
/// Represents the addressing mode for the 6502 CPU's Indirect Indexed (Y) operation.
/// </summary>
/// <remarks>This addressing mode uses the provided zero-page address as the base, adds the Y register value, and
/// dereferences the resulting address to obtain the effective memory location.</remarks>
/// <param name="Address">The zero-page address (low byte) used as the base for the Indirect Indexed (Y) operation.</param>
public readonly record struct Mos6502IndirectY(byte Address)
{
    /// <summary>
    /// Explicit conversion from byte to <see cref="Mos6502IndirectY"/>.
    /// </summary>
    /// <param name="value">The byte value representing the zero-page address.</param>
    public static explicit operator Mos6502IndirectY(byte value) => new(value);
}

/// <summary>
/// A factory class for creating instances of <see cref="Mos6502Indirect"/> and <see cref="Mos6502IndirectX"/> addressing modes.
/// </summary>
public struct Mos6502IndirectFactory
{
    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502Indirect"/>.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>An indirect address</returns>
    public Mos6502Indirect this[ushort address] => new(address);
    
    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502Indirect"/>.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>An indirect address</returns>
    public Mos6502ExpressionIndirectU16 this[Mos6502Label address] => new(address);
    
    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502Indirect"/>.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>An indirect address</returns>
    public Mos6502ExpressionIndirectU16 this[Mos6502ExpressionU16 address] => new(address);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (X) address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
    /// <param name="x">The X register</param>
    /// <returns>An indirect ZeroPage address Indexed by (X)</returns>
    public Mos6502IndirectX this[byte zpAddress, Mos6502RegisterX x] => new(zpAddress);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (Y) address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (Y) operation.</param>
    /// <returns>An indirect ZeroPage address Indexed by (Y)</returns>
    public Mos6502IndirectY this[byte zpAddress] => new(zpAddress);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (X) address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (X) operation.</param>
    /// <param name="x">The X register</param>
    /// <returns>An indirect ZeroPage address Indexed by (X)</returns>
    public Mos6502ExpressionIndirectX this[Mos6502ExpressionU8 zpAddress, Mos6502RegisterX x] => new(zpAddress);

    /// <summary>
    /// Accessor for creating an instance of <see cref="Mos6502IndirectX"/> using a zero-page Indirect Indexed (Y) address.
    /// </summary>
    /// <param name="zpAddress">The zero-page address (low byte) used as the base for the Indirect Indexed (Y) operation.</param>
    /// <returns>An indirect ZeroPage address Indexed by (Y)</returns>
    public Mos6502ExpressionIndirectY this[Mos6502ExpressionU8 zpAddress] => new(zpAddress);
}