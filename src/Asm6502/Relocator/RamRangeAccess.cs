// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Represents a contiguous range of RAM addresses with associated read/write access flags.
/// </summary>
public readonly record struct RamRangeAccess : IComparable<RamRangeAccess>, IComparable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RamRangeAccess"/> struct.
    /// </summary>
    /// <param name="start">The starting address of the RAM range.</param>
    /// <param name="length">The length of the RAM range in bytes.</param>
    /// <param name="flags">The read/write access flags for this range. Defaults to <see cref="RamReadWriteFlags.ReadWrite"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range exceeds the 64KB address space.</exception>
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
    
    /// <summary>
    /// Gets the starting address of the RAM range.
    /// </summary>
    public ushort Start { get; }

    /// <summary>
    /// Gets the length of the RAM range in bytes.
    /// </summary>
    public ushort Length { get; }

    /// <summary>
    /// Gets the read/write access flags for this RAM range.
    /// </summary>
    public RamReadWriteFlags Flags { get; }

    /// <summary>
    /// Gets the end address of the RAM range (Start + Length).
    /// </summary>
    public ushort End => (ushort)(Start + Length);
    
    /// <summary>
    /// Determines whether the specified address is contained within this RAM range.
    /// </summary>
    /// <param name="addr">The address to check.</param>
    /// <returns><c>true</c> if the address is within the range; otherwise, <c>false</c>.</returns>
    public bool Contains(ushort addr) => addr >= Start && addr <= End;

    /// <summary>
    /// Compares this instance to another <see cref="RamRangeAccess"/> instance.
    /// Comparison is first by <see cref="Start"/>, then by <see cref="Length"/>.
    /// </summary>
    /// <param name="other">The other instance to compare to.</param>
    /// <returns>A value indicating the relative order of the objects being compared.</returns>
    public int CompareTo(RamRangeAccess other)
    {
        var delta = Start.CompareTo(other.Start);
        return delta != 0 ? delta : Length.CompareTo(other.Length);
    }

    /// <summary>
    /// Compares this instance to another object.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>A value indicating the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not of type <see cref="RamRangeAccess"/>.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        return obj is RamRangeAccess other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(RamRangeAccess)}");
    }

    /// <inheritdoc />
    public override string ToString() => $"[${Start:x4}-${End - 1:x4}] ({Length} bytes) {Flags}";

    /// <summary>
    /// Determines whether one <see cref="RamRangeAccess"/> is less than another.
    /// </summary>
    public static bool operator <(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one <see cref="RamRangeAccess"/> is greater than another.
    /// </summary>
    public static bool operator >(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one <see cref="RamRangeAccess"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one <see cref="RamRangeAccess"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(RamRangeAccess left, RamRangeAccess right) => left.CompareTo(right) >= 0;
}