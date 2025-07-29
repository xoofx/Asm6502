// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502.Expressions;

namespace AsmMos6502;

/// <summary>
/// Represents a label in Mos6502.
/// </summary>
public class Mos6502Label
{
    /// <summary>
    /// Creates an unbound label with the specified name.
    /// </summary>
    /// <remarks>
    /// The label needs to be bound to an address with <see cref="Mos6502Assembler.Label(AsmMos6502.Mos6502Label,bool)"/> before it can be used in an instruction.
    /// </remarks>
    /// <param name="name">The name of the label</param>
    public Mos6502Label(string? name = null)
    {
        Name = name;
        IsBound = false;
    }

    /// <summary>
    /// Creates a bound label with the specified name and address.
    /// </summary>
    /// <param name="name">The name of the label</param>
    /// <param name="address">The address of the label</param>
    public Mos6502Label(string? name, ushort address)
    {
        Name = name;
        Address = address;
        IsBound = true;
    }

    /// <summary>
    /// Gets the name of the label.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the address of the label.
    /// </summary>
    public ushort Address { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the label is bound.
    /// </summary>
    public bool IsBound { get; internal set; }
    
    /// <inheritdoc />
    public override string ToString() => Name ?? (IsBound ? $"0x{Address:X4}" : $"0x????");

    /// <summary>
    /// Subtracts one <see cref="Mos6502Label"/> from another and returns the resulting 16-bit expression.
    /// </summary>
    /// <param name="left">The label to subtract from.</param>
    /// <param name="right">The label to subtract.</param>
    /// <returns>A <see cref="Mos6502ExpressionU16"/> representing the difference between the two labels.</returns>
    public static Mos6502ExpressionU16 operator -(Mos6502Label left, Mos6502Label right) => left.ToExpression() - right.ToExpression();
    
    /// <summary>
    /// Subtracts one 16-bit MOS 6502 expression from a const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the subtraction.</returns>
    public static Mos6502ExpressionU16 operator -(Mos6502Label left, int right) => new Mos6502ExpressionAddConstU16(left.ToExpression(), (short)-right);

    /// <summary>
    /// Adds one 16-bit MOS 6502 expression to const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the addition.</returns>
    public static Mos6502ExpressionU16 operator +(Mos6502Label left, int right) => new Mos6502ExpressionAddConstU16(left.ToExpression(), (short)right);
}