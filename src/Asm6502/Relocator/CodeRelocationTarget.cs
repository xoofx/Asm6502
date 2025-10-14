// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Represents a target location for code relocation, specifying the destination address and zero-page RAM range.
/// </summary>
public readonly record struct CodeRelocationTarget
{
    /// <summary>
    /// Gets the target address where the code will be relocated.
    /// </summary>
    public required ushort Address { get; init; }

    /// <summary>
    /// Gets the zero-page RAM range available for use by the relocated code.
    /// </summary>
    public required RamZpRange ZpRange { get; init; }

    /// <summary>
    /// Returns a string representation of this relocation target.
    /// </summary>
    /// <returns>A string in the format "Address: $XXXX, ZP: ..." where XXXX is the hexadecimal address.</returns>
    public override string ToString() => $"Address: ${Address:x4}, ZP: {ZpRange}";
}