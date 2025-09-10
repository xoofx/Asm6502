// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502.Expressions;

namespace AsmMos6502;

/// <summary>
/// Represents a label in Mos6502.
/// </summary>
public record Mos6502Label : Mos6502ExpressionU16, IMos6502Label
{
    /// <summary>
    /// Creates an unbound label with the specified name.
    /// </summary>
    /// <remarks>
    /// The label needs to be bound to an address with <see cref="Mos6502AssemblerBase.Label(AsmMos6502.Mos6502Label,bool)"/> before it can be used in an instruction.
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
    public override ushort Evaluate()
    {
        if (!IsBound) throw new InvalidOperationException($"Label `{this}` is not bound");
        return Address;
    }

    /// <inheritdoc />
    public override void CollectLabels(HashSet<IMos6502Label> labels) => labels.Add(this);

    /// <inheritdoc />
    public override string ToString() => Name ?? (IsBound ? $"0x{Address:X4}" : $"0x????");

    /// <summary>
    /// This method tries to parse a simple C# expression to extract a label name.
    /// </summary>
    /// <param name="labelExpression">The label expression.</param>
    /// <returns>The label name if successfully parsed; otherwise, null.</returns>
    internal static string? ParseCSharpExpression(string? labelExpression)
    {
        // We support only the following C# expression
        // var variableName
        // variableName
        // Mos6502Label variableName
        //
        // We don't support complex expressions like:
        // array[0]
        // obj.Field
        // ...etc.

        // The following is a simple parser that checks if the expression is a valid C# identifier
        // it could be improved in the future to support more complex expressions if needed

        if (string.IsNullOrEmpty(labelExpression)) return null;

        var span = labelExpression.AsSpan();
        var indexOfSpace = span.IndexOf(' ');
        if (indexOfSpace >= 0)
        {
            span = span[(indexOfSpace + 1)..].Trim();
        }
        if (span.IsEmpty) return null;
        // labelName must be a valid C# identifier

        if (!char.IsLetter(span[0]) && span[0] != '_') return null;
        for (var i = 1; i < span.Length; i++)
        {
            var c = span[i];
            if (!char.IsLetterOrDigit(c) && c != '_') return null;
        }

        return span.ToString();
    }
}