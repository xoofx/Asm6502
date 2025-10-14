// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public readonly record struct RamRangeAccess : IComparable<RamRangeAccess>, IComparable
{
    public RamRangeAccess(ushort start, ushort length, RamReadWriteFlags flags = RamReadWriteFlags.ReadWrite)
    {
        var end = start + length;
        if (end > 0x10000)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The length exceeds the 64KB address space.");
        }
        
        Start = start;
        Length = length;
        Flags = flags;
    }
    
    public ushort Start { get; }

    public ushort Length { get; }

    public RamReadWriteFlags Flags { get; }

    public ushort End => (ushort)(Start + Length);
    
    public bool Contains(ushort addr) => addr >= Start && addr <= End;

    public int CompareTo(RamRangeAccess other)
    {
        var delta = Start.CompareTo(other.Start);
        return delta != 0 ? delta : Length.CompareTo(other.Length);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        return obj is RamRangeAccess other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(RamRangeAccess)}");
    }

    public static bool operator <(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) < 0;

    public static bool operator >(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) > 0;

    public static bool operator <=(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) <= 0;

    public static bool operator >=(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) >= 0;
}