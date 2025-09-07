// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502.Expressions;

namespace AsmMos6502;

/// <summary>
/// Represents a label in the ZeroPage address range (byte, 0x00 - 0xFF) in Mos6502.
/// </summary>
public record Mos6502LabelZp : Mos6502ExpressionU8, IMos6502Label
{
    private byte _address;

    /// <summary>
    /// Creates an unbound label with the specified name.
    /// </summary>
    /// <remarks>
    /// The label needs to be bound to an address with <see cref="Mos6502AssemblerBase.Label(AsmMos6502.Mos6502Label,bool)"/> before it can be used in an instruction.
    /// </remarks>
    /// <param name="name">The name of the label</param>
    public Mos6502LabelZp(string? name = null)
    {
        Name = name;
        IsBound = false;
    }

    /// <summary>
    /// Creates a bound label with the specified name and address.
    /// </summary>
    /// <param name="name">The name of the label</param>
    /// <param name="address">The address of the label</param>
    public Mos6502LabelZp(string? name, byte address)
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
    public byte Address
    {
        get => _address;
        set
        {
            IsBound = true;
            _address = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the label is bound.
    /// </summary>
    public bool IsBound { get; internal set; }

    /// <summary>
    /// Resets the label to an unbound state.
    /// </summary>
    public void Reset()
    {
        IsBound = false;
        _address = 0;
    }

    /// <inheritdoc />
    public override byte Evaluate()
    {
        if (!IsBound) throw new InvalidOperationException($"ZeroPage Label `{this}` is not bound");
        return Address;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => labels.Add(this);


    /// <inheritdoc />
    public override string ToString() => Name ?? (IsBound ? $"0x{Address:X2}" : $"0x??");
}