// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

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
    /// The label needs to be bound to an address with <see cref="Mos6502Assembler.BindLabel"/> before it can be used in an instruction.
    /// </remarks>
    /// <param name="name">The name of the label</param>
    public Mos6502Label(string? name = null)
    {
        Name = name;
        IsBound = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="address"></param>
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
}