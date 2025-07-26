// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

/// <summary>
/// Internal enumeration to represent the kind of address that can be patched in a 6502 instruction.
/// </summary>
internal enum Mos6502AddressKind
{
    /// <summary>
    /// No address to patch.
    /// </summary>
    None,
    /// <summary>
    /// A 16-bit absolute address.
    /// </summary>
    Absolute,
    /// <summary>
    /// An 8-bit signed offset from PC.
    /// </summary>
    Relative,
}