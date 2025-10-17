// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Flags that describe the usage and relocation flags of a byte
/// returned by <see cref="CodeRelocator.GetRamByteFlagsAt"/>.
/// </summary>
[Flags]
public enum RamByteFlags : byte
{
    /// <summary>
    /// No flags are set.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Indicates the byte is read by the code.
    /// </summary>
    Read = 1 << 0,
    
    /// <summary>
    /// Indicates the byte is written to by the code.
    /// </summary>
    Write = 1 << 1,
    
    /// <summary>
    /// Indicates the byte should not be relocated.
    /// </summary>
    NoReloc = 1 << 2,
    
    /// <summary>
    /// Indicates the byte should be relocated.
    /// </summary>
    Reloc = 1 << 3,
    
    /// <summary>
    /// Indicates the byte is used in zero page addressing mode.
    /// </summary>
    UsedInZp = 1 << 4,
    
    /// <summary>
    /// Indicates the byte is used as the most significant byte (MSB) of an address.
    /// </summary>
    UsedInMsb = 1 << 5,
}