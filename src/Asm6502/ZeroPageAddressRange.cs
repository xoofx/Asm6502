// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace Asm6502;

/// <summary>
/// Represents a contiguous inclusive range of zero-page addresses (0x00..0xFF).
/// </summary>
public readonly struct ZeroPageAddressRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroPageAddressRange"/> struct.
    /// </summary>
    /// <param name="name">An optional symbolic name for the range.</param>
    /// <param name="beginAddress">The first address of the range (inclusive).</param>
    /// <param name="endAddress">The last address of the range (inclusive).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="beginAddress"/> is greater than <paramref name="endAddress"/>.</exception>
    public ZeroPageAddressRange(string? name, byte beginAddress, byte endAddress)
    {
        Name = name;
        if (beginAddress > endAddress) throw new ArgumentOutOfRangeException(nameof(beginAddress), $"Begin address 0x{beginAddress:x2} must be less than or equal to end address 0x{endAddress:x2}");
        BeginAddress = beginAddress;
        EndAddress = endAddress;
    }

    /// <summary>
    /// Gets the optional symbolic name associated with this range.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the first address of the range (inclusive).
    /// </summary>
    public byte BeginAddress { get; }

    /// <summary>
    /// Gets the last address of the range (inclusive).
    /// </summary>
    public byte EndAddress { get; }

    /// <summary>
    /// Gets the total number of addresses in the range.
    /// </summary>
    public byte Length => (byte)(EndAddress - BeginAddress + 1);

    /// <summary>
    /// Gets the <see cref="ZeroPageAddress"/> at the specified index within the range.
    /// </summary>
    /// <param name="index">An index from 0 to <c>Length - 1</c>.</param>
    /// <returns>A <see cref="ZeroPageAddress"/> at the specified offset from <see cref="BeginAddress"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is outside the valid range.</exception>
    public ZeroPageAddress this[int index]
    {
        get
        {
            if (index < 0 || index >= Length) throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range (0..{Length - 1})");
            return new ZeroPageAddress(Name, BeginAddress, (byte)index);
        }
    }

    /// <summary>
    /// Determines whether the specified address is contained within this range (inclusive).
    /// </summary>
    /// <param name="address">The address to test.</param>
    /// <returns>True if the address is within the range; otherwise, false.</returns>
    public bool Contains(byte address) => address >= BeginAddress && address <= EndAddress;

    /// <inheritdoc />
    public override string ToString()
    {
        // name: 0x00-0xff
        var sb = new StringBuilder();
        sb.Append(Name ?? "<unknown>");
        sb.Append(": 0x");
        sb.Append(BeginAddress.ToString("x2"));
        sb.Append("-0x");
        sb.Append(EndAddress.ToString("x2"));
        return sb.ToString();
    }
}