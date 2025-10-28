// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Represents a label <see cref="Mos6502Label"/> in the MOS 6502 assembly context.
/// Provides the name of the label and indicates whether it is bound to an address.
/// </summary>
public interface IMos6502Label
{
    /// <summary>
    /// Gets the name of the label, or null if unnamed.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets a value indicating whether the label is bound to an address.
    /// </summary>
    public bool IsBound { get; }
}