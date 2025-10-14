// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public readonly record struct RamZpRange
{
    public RamZpRange(byte start, byte length)
    {
        var end = start + length;
        if (end > 0x100)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The length exceeds the 256B zero-page address space.");
        }
        
        Start = start;
        Length = length;
    }
    
    public byte Start { get; }

    public byte Length { get; }

    public bool IsEmpty => Length == 0;

    public byte GetEnd() => IsEmpty ? throw new InvalidOperationException("RamZpRange is empty") : (byte)(Start + Length - 1);

    public bool Contains(byte addr) => Length > 0 && addr >= Start && addr <= (Start + Length - 1);
}