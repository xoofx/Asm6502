// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Represents a range of addresses in the zero-page (ZP) RAM address space (0x00-0xFF).
/// </summary>
public readonly record struct RamZpRange : IComparable<RamZpRange>, IComparable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RamZpRange"/> struct.
    /// </summary>
    /// <param name="start">The starting address of the range.</param>
    /// <param name="length">The length of the range in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range exceeds the 256-byte zero-page address space.</exception>
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
    
    /// <summary>
    /// Gets the starting address of the range.
    /// </summary>
    public byte Start { get; }

    /// <summary>
    /// Gets the length of the range in bytes.
    /// </summary>
    public byte Length { get; }

    /// <summary>
    /// Gets a value indicating whether this range is empty (length is 0).
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Gets the ending address of the range (inclusive).
    /// </summary>
    /// <returns>The last address in the range.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the range is empty.</exception>
    public byte GetEnd() => IsEmpty ? throw new InvalidOperationException("RamZpRange is empty") : (byte)(Start + Length - 1);

    /// <summary>
    /// Determines whether the specified address is contained within this range.
    /// </summary>
    /// <param name="addr">The address to check.</param>
    /// <returns><c>true</c> if the address is within the range; otherwise, <c>false</c>.</returns>
    public bool Contains(byte addr) => Length > 0 && addr >= Start && addr <= (Start + Length - 1);

    /// <summary>
    /// Returns a string representation of this range in hexadecimal format.
    /// </summary>
    /// <returns>A string in the format "$XX-$XX (length)" or "&lt;empty>" if the range is empty.</returns>
    public override string ToString() => IsEmpty ? "<empty>" : $"${Start:x2}-${GetEnd():x2} ({Length})";

    /// <summary>
    /// Compares this range to another <see cref="RamZpRange"/> instance.
    /// </summary>
    /// <param name="other">The range to compare with this instance.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// Less than zero if this instance precedes <paramref name="other"/>, zero if this instance has the same position as <paramref name="other"/>,
    /// greater than zero if this instance follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(RamZpRange other)
    {
        var startComparison = Start.CompareTo(other.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        return Length.CompareTo(other.Length);
    }

    /// <summary>
    /// Compares this range to another object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// Less than zero if this instance precedes <paramref name="obj"/>, zero if this instance has the same position as <paramref name="obj"/>,
    /// greater than zero if this instance follows <paramref name="obj"/> or <paramref name="obj"/> is null.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not of type <see cref="RamZpRange"/>.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        return obj is RamZpRange other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(RamZpRange)}");
    }

    /// <summary>
    /// Determines whether one range is less than another range.
    /// </summary>
    /// <param name="left">The first range to compare.</param>
    /// <param name="right">The second range to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <(RamZpRange left, RamZpRange right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one range is greater than another range.
    /// </summary>
    /// <param name="left">The first range to compare.</param>
    /// <param name="right">The second range to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >(RamZpRange left, RamZpRange right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one range is less than or equal to another range.
    /// </summary>
    /// <param name="left">The first range to compare.</param>
    /// <param name="right">The second range to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <=(RamZpRange left, RamZpRange right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one range is greater than or equal to another range.
    /// </summary>
    /// <param name="left">The first range to compare.</param>
    /// <param name="right">The second range to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >=(RamZpRange left, RamZpRange right) => left.CompareTo(right) >= 0;
}