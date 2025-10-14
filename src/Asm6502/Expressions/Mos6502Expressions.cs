// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Expressions;

/// <summary>
/// Base class for all 6502 memory expressions.
/// </summary>
public abstract record Mos6502Expression()
{
    /// <summary>
    /// Collects all labels used in this expression.
    /// </summary>
    /// <param name="labels">The set to collect labels into.</param>
    public abstract void CollectLabels(HashSet<IMos6502Label> labels);
}

/// <summary>
/// Base class for all 6502 memory expressions that evaluate to a byte (8 bits).
/// </summary>
public abstract record Mos6502ExpressionU8 : Mos6502Expression
{
    /// <summary>
    /// Evaluates the expression and returns a byte value.
    /// </summary>
    /// <returns>The evaluated byte value.</returns>
    public abstract byte Evaluate();

    /// <summary>
    /// Converts a function that evaluates to a byte into a <see cref="Mos6502ExpressionU8"/>.
    /// </summary>
    /// <param name="evaluateFunc">A function that evaluates to a byte value.</param>
    public static implicit operator Mos6502ExpressionU8(Func<byte> evaluateFunc) => new Mos6502ExpressionFuncU8(evaluateFunc);

    /// <summary>
    /// Subtracts one 16-bit MOS 6502 expression from another.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit expression to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU8"/> representing the result of the subtraction.</returns>
    public static Mos6502ExpressionU8 operator -(Mos6502ExpressionU8 left, Mos6502ExpressionU8 right) => new Mos6502ExpressionSubtractU8(left, right);

    /// <summary>
    /// Subtracts one 16-bit MOS 6502 expression from a const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the subtraction.</returns>
    public static Mos6502ExpressionU8 operator -(Mos6502ExpressionU8 left, byte right) => new Mos6502ExpressionAddConstU8(left, (sbyte)-right);

    /// <summary>
    /// Adds one 16-bit MOS 6502 expression to const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the addition.</returns>
    public static Mos6502ExpressionU8 operator +(Mos6502ExpressionU8 left, byte right) => new Mos6502ExpressionAddConstU8(left, (sbyte)right);
}

/// <summary>
/// Base class for all 6502 memory expressions that evaluate to a 16 bits unsigned integer (16 bits).
/// </summary>
public abstract record Mos6502ExpressionU16 : Mos6502Expression
{
    /// <summary>
    /// Evaluates the expression and returns a 16 bits unsigned integer value.
    /// </summary>
    /// <returns>The evaluated 16 bits unsigned integer value.</returns>
    public abstract ushort Evaluate();

    /// <summary>
    /// Converts a function that evaluates to a 16 bits unsigned integer into a <see cref="Mos6502ExpressionU16"/>.
    /// </summary>
    /// <param name="evaluateFunc">A function that evaluates to a 16 bits unsigned integer value.</param>
    public static implicit operator Mos6502ExpressionU16(Func<ushort> evaluateFunc) => new Mos6502ExpressionFuncU16(evaluateFunc);

    /// <summary>
    /// Subtracts one 16-bit MOS 6502 expression from another.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit expression to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the subtraction.</returns>
    public static Mos6502ExpressionU16 operator -(Mos6502ExpressionU16 left, Mos6502ExpressionU16 right) => new Mos6502ExpressionSubtractU16(left, right);

    /// <summary>
    /// Subtracts one 16-bit MOS 6502 expression from a const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the subtraction.</returns>
    public static Mos6502ExpressionU16 operator -(Mos6502ExpressionU16 left, short right) => new Mos6502ExpressionAddConstU16(left, (short)-right);

    /// <summary>
    /// Adds one 16-bit MOS 6502 expression to const.
    /// </summary>
    /// <param name="left">The minuend, representing the 16-bit expression to subtract from.</param>
    /// <param name="right">The subtrahend, representing the 16-bit const value to subtract.</param>
    /// <returns>A new <see cref="Mos6502ExpressionU16"/> representing the result of the addition.</returns>
    public static Mos6502ExpressionU16 operator +(Mos6502ExpressionU16 left, short right) => new Mos6502ExpressionAddConstU16(left, (short)right);
}

/// <summary>
/// Represents the low byte of a 16-bit expression in the MOS 6502 architecture.
/// </summary>
/// <remarks>This type evaluates to the least significant byte (low byte) of the result of a 16-bit expression. It
/// is useful for operations where only the low byte of a 16-bit value is required.</remarks>
/// <param name="Expression">The 16-bit expression from which to derive the low byte.</param>
public record Mos6502ExpressionLowByte(Mos6502ExpressionU16 Expression) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate() => (byte)(Expression.Evaluate());

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Expression.CollectLabels(labels);
}

/// <summary>
/// Represents an 8-bit expression that evaluates to the high byte of a 16-bit expression.
/// </summary>
/// <remarks>This type is used to extract the most significant byte (high byte) from a 16-bit expression by
/// evaluating the provided 16-bit expression and shifting its result 8 bits to the right.</remarks>
/// <param name="Expression">The 16-bit expression from which to derive the high byte.</param>
public record Mos6502ExpressionHighByte(Mos6502ExpressionU16 Expression) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate() => (byte)(Expression.Evaluate() >> 8);

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Expression.CollectLabels(labels);
}

/// <summary>
/// Represents an expression that performs a subtraction operation between an 8-bit expression and an 8-bit subtrahend,
/// resulting in a 8-bit value.
/// </summary>
/// <remarks>This expression evaluates to the result of subtracting the value of the <see cref="Right"/> from
/// the value of the <see cref="Left"/>. The result is computed as an unsigned 8-bit integer, and any overflow or
/// underflow will wrap around according to standard unsigned arithmetic rules.</remarks>
/// <param name="Left">The 8-bit expression that serves as the left operand (the value from which another value is subtracted).</param>
/// <param name="Right">The 8-bit expression that serves as the right operand (the value to be subtracted from the left operand).</param>
public record Mos6502ExpressionSubtractU8(Mos6502ExpressionU8 Left, Mos6502ExpressionU8 Right) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate()
    {
        var result = (byte)(Left.Evaluate() - Right.Evaluate());
        return result;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels)
    {
        Left.CollectLabels(labels);
        Right.CollectLabels(labels);
    }
}

/// <summary>
/// An 8-bit expression that adds a constant value to another 8-bit expression in the MOS 6502 architecture.
/// </summary>
/// <param name="Left">The left operand, which is an 8-bit expression.</param>
/// <param name="Right">The right operand, which is a constant 8-bit value to be added to the left operand.</param>
public record Mos6502ExpressionAddConstU8(Mos6502ExpressionU8 Left, sbyte Right) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate()
    {
        var result = (byte)(Left.Evaluate() + Right);
        return result;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Left.CollectLabels(labels);
}

/// <summary>
/// Represents an expression that performs a subtraction operation between a 16-bit expression and an 8-bit subtrahend,
/// resulting in a 16-bit value.
/// </summary>
/// <remarks>This expression evaluates to the result of subtracting the value of the <see cref="Right"/> from
/// the value of the <see cref="Left"/>. The result is computed as an unsigned 16-bit integer, and any overflow or
/// underflow will wrap around according to standard unsigned arithmetic rules.</remarks>
/// <param name="Left">The 16-bit expression that serves as the left operand (the value from which another value is subtracted).</param>
/// <param name="Right">The 8-bit expression that serves as the right operand (the value to be subtracted from the left operand).</param>
public record Mos6502ExpressionSubtractU16(Mos6502ExpressionU16 Left, Mos6502ExpressionU16 Right) : Mos6502ExpressionU16
{
    /// <inheritdoc />
    public override ushort Evaluate()
    {
        var result = (ushort)(Left.Evaluate() - Right.Evaluate());
        return result;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels)
    {
        Left.CollectLabels(labels);
        Right.CollectLabels(labels);
    }
}

/// <summary>
/// A 16-bit expression that adds a constant value to another 16-bit expression in the MOS 6502 architecture.
/// </summary>
/// <param name="Left">The left operand, which is a 16-bit expression.</param>
/// <param name="Right">The right operand, which is a constant 16-bit value to be added to the left operand.</param>
public record Mos6502ExpressionAddConstU16(Mos6502ExpressionU16 Left, short Right) : Mos6502ExpressionU16
{
    /// <inheritdoc />
    public override ushort Evaluate()
    {
        var result = (ushort)(Left.Evaluate() + Right);
        return result;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Left.CollectLabels(labels);
}

/// <summary>
/// Represents a 16-bit MOS 6502 expression that is derived from an 8-bit expression by zero-extending its value.
/// </summary>
/// <param name="Left">The 8-bit expression whose value is used as the lower byte of the resulting 16-bit expression.</param>
public record Mos6502ExpressionU8ToU16(Mos6502ExpressionU8 Left) : Mos6502ExpressionU16
{
    /// <inheritdoc />
    public override ushort Evaluate() => Left.Evaluate();

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Left.CollectLabels(labels);
}


/// <summary>
/// Represents a 16-bit expression that evaluates to an indirect address in the MOS 6502 architecture.
/// </summary>
/// <param name="Expression">The 16-bit expression that resolves to an indirect address.</param>
public record Mos6502ExpressionIndirectU16(Mos6502ExpressionU16 Expression) : Mos6502ExpressionU16
{
    /// <inheritdoc />
    public override ushort Evaluate() => Expression.Evaluate();

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Expression.CollectLabels(labels);
}

/// <summary>
/// Represents an 8-bit expression that evaluates to an indirect X address in the MOS 6502 architecture.
/// </summary>
/// <param name="Expression">The 8-bit expression that resolves to an indirect X address.</param>
public record Mos6502ExpressionIndirectX(Mos6502ExpressionU8 Expression) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate() => Expression.Evaluate();
    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Expression.CollectLabels(labels);
}

/// <summary>
/// Represents an 8-bit expression that evaluates to an indirect Y address in the MOS 6502 architecture.
/// </summary>
/// <param name="Expression">The 8-bit expression that resolves to an indirect Y address.</param>
public record Mos6502ExpressionIndirectY(Mos6502ExpressionU8 Expression) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate() => Expression.Evaluate();

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => Expression.CollectLabels(labels);
}

/// <summary>
/// Represents a 8-bit expression that evaluates to a value using a function.
/// </summary>
/// <param name="EvaluateFunc">The function that evaluates to a byte value.</param>
public record Mos6502ExpressionFuncU8(Func<byte> EvaluateFunc) : Mos6502ExpressionU8
{
    /// <inheritdoc />
    public override byte Evaluate() => EvaluateFunc();

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels)
    {
    }
}

/// <summary>
/// Represents a 16-bit expression that evaluates to a value using a function.
/// </summary>
/// <param name="EvaluateFunc">The function that evaluates to a 16-bit unsigned integer value.</param>
public record Mos6502ExpressionFuncU16(Func<ushort> EvaluateFunc) : Mos6502ExpressionU16
{
    /// <inheritdoc />
    public override ushort Evaluate() => EvaluateFunc();

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels)
    {
    }
}
