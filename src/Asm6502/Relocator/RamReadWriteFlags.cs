// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Flags indicating how RAM memory is accessed (read and/or write operations).
/// </summary>
/// <remarks>
/// This is used by <see cref="RamRangeAccess"/> to indicate the type of access for a given memory range
/// that can be added to the <see cref="CodeRelocator.SafeRamRanges"/>.
/// </remarks>
[Flags]
public enum RamReadWriteFlags : byte
{
    /// <summary>
    /// No read or write access.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Memory is read.
    /// </summary>
    Read = 1 << 0,
    
    /// <summary>
    /// Memory is written to.
    /// </summary>
    Write = 1 << 1,
    
    /// <summary>
    /// Memory is both read from and written to.
    /// </summary>
    ReadWrite = Read | Write,
}