// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace Asm6502;

/// <summary>
/// Represents a 6502 zero-page effective address with an optional symbolic name.
/// The effective address is computed as <see cref="BaseAddress"/> + <see cref="Offset"/> and must be within 0x00..0xFF.
/// </summary>
public readonly struct ZeroPageAddress : IEquatable<ZeroPageAddress>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroPageAddress"/> struct.
    /// </summary>
    /// <param name="name">An optional symbolic name associated with the address.</param>
    /// <param name="baseAddress">The base address (0x00..0xFF) in zero page.</param>
    /// <param name="offset">The offset relative to <paramref name="baseAddress"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the resulting address is outside the 0x00..0xFF range.</exception>
    public ZeroPageAddress(string? name, byte baseAddress, int offset)
    {
        Name = name;
        var address = baseAddress + offset;
        if (address < 0 || address > 0xFF) throw new ArgumentOutOfRangeException(nameof(baseAddress), $"Resulting address {address} is out of range (0x00..0xff)");
        BaseAddress = baseAddress;
        Offset = offset;
    }

    /// <summary>
    /// Gets the optional symbolic name associated with this address.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the base address used to compute the effective address.
    /// </summary>
    public byte BaseAddress { get; }

    /// <summary>
    /// Gets the effective address as <see cref="BaseAddress"/> + <see cref="Offset"/>.
    /// </summary>
    public byte Address => (byte)(BaseAddress + Offset);

    /// <summary>
    /// Gets the offset from the <see cref="BaseAddress"/>.
    /// </summary>
    public int Offset { get; }
    
    /// <summary>
    /// Indicates whether the current address is equal to another address by comparing their effective addresses.
    /// </summary>
    /// <param name="other">The other zero-page address to compare with.</param>
    /// <returns>True if both effective addresses are equal; otherwise, false.</returns>
    public bool Equals(ZeroPageAddress other) => Address == other.Address;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ZeroPageAddress other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => BaseAddress.GetHashCode();

    /// <inheritdoc />
    public override string ToString()
    {
        // name(+offset): 0x00
        var sb = new StringBuilder();
        sb.Append(Name ?? "<unknown>");
        if (Offset != 0)
        {
            if (Offset > 0)
            {
                sb.Append('+');
            }
            sb.Append(Offset);
        }
        sb.Append(": 0x");
        sb.Append(Address.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Returns a new <see cref="ZeroPageAddress"/> advanced by the specified <paramref name="offset"/>.
    /// </summary>
    /// <param name="addr">The base address.</param>
    /// <param name="offset">The offset to add.</param>
    /// <returns>A new <see cref="ZeroPageAddress"/> representing the advanced address.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the resulting address is outside the 0x00..0xFF range.</exception>
    public static ZeroPageAddress operator +(ZeroPageAddress addr, int offset)
    {
        var newAddress = addr.BaseAddress + offset;
        if (newAddress < 0 || newAddress > 0xFF) throw new ArgumentOutOfRangeException(nameof(offset), $"Resulting address {newAddress} is out of range (0x00..0xff)");
        return new ZeroPageAddress(addr.Name, (byte)addr.Address, addr.Offset + offset);
    }

    /// <summary>
    /// Returns a new <see cref="ZeroPageAddress"/> moved back by the specified <paramref name="offset"/>.
    /// </summary>
    /// <param name="addr">The base address.</param>
    /// <param name="offset">The offset to subtract.</param>
    /// <returns>A new <see cref="ZeroPageAddress"/> representing the moved address.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the resulting address is outside the 0x00..0xFF range.</exception>
    public static ZeroPageAddress operator -(ZeroPageAddress addr, int offset)
    {
        var newAddress = addr.BaseAddress - offset;
        if (newAddress < 0 || newAddress > 0xFF) throw new ArgumentOutOfRangeException(nameof(offset), $"Resulting address {newAddress} is out of range (0x00..0xff)");
        return new ZeroPageAddress(addr.Name, (byte)addr.Address, addr.Offset - offset);
    }

    /// <summary>
    /// Implicitly converts a <see cref="ZeroPageAddress"/> to its effective byte address.
    /// </summary>
    /// <param name="zp">The zero-page address.</param>
    /// <returns>The effective address as a byte.</returns>
    public static implicit operator byte(ZeroPageAddress zp) => zp.Address;

    /// <summary>
    /// Computes the signed distance in bytes between two zero-page addresses.
    /// </summary>
    /// <param name="left">The left address.</param>
    /// <param name="right">The right address.</param>
    /// <returns>The difference <c>left.Address - right.Address</c>.</returns>
    public static int operator -(ZeroPageAddress left, ZeroPageAddress right) => left.Address - right.Address;

    /// <summary>
    /// Determines whether two zero-page addresses refer to the same effective address.
    /// </summary>
    /// <param name="left">The left address.</param>
    /// <param name="right">The right address.</param>
    /// <returns>True if the effective addresses are equal; otherwise, false.</returns>
    public static bool operator ==(ZeroPageAddress left, ZeroPageAddress right) => left.Equals(right);

    /// <summary>
    /// Determines whether two zero-page addresses refer to different effective addresses.
    /// </summary>
    /// <param name="left">The left address.</param>
    /// <param name="right">The right address.</param>
    /// <returns>True if the effective addresses are not equal; otherwise, false.</returns>
    public static bool operator !=(ZeroPageAddress left, ZeroPageAddress right) => !left.Equals(right);
}