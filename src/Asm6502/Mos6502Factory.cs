// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Factory class for creating 6502 CPU instructions.
/// </summary>
public static class Mos6502Factory
{
    /// <summary>
    /// Represents a factory for creating instances of <see cref="Mos6502Indirect"/> and <see cref="Mos6502IndirectX"/> addressing modes.
    /// </summary>
    public static Mos6502IndirectFactory _ => new();
    
    /// <summary>
    /// Gets the 6502 CPU Accumulator register.
    /// </summary>
    public const Mos6502RegisterA A = Mos6502RegisterA.A;

    /// <summary>
    /// Gets the 6502 CPU X index register.
    /// </summary>
    public const Mos6502RegisterX X = Mos6502RegisterX.X;

    /// <summary>
    /// Gets the 6502 CPU Y index register.
    /// </summary>
    public const Mos6502RegisterY Y = Mos6502RegisterY.Y;
}