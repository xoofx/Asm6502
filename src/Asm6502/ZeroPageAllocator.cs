// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Asm6502;

/// <summary>
/// Helper class that provides allocation, reservation, and release of 6502 zero-page addresses and contiguous ranges.
/// </summary>
[DebuggerTypeProxy(typeof(CustomDebuggerTypeProxy))]
[DebuggerDisplay("Count = {AllocatedCount}, Free = {FreeCount}")]
public class ZeroPageAllocator
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ZeroPageAddressTable _table;
    // Reversed from 0xFF to 0x00
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ZeroPageAddressBitmap _bitmap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroPageAllocator"/> class.
    /// </summary>
    public ZeroPageAllocator()
    {
        Clear();
    }

    /// <summary>
    /// Gets the number of free addresses available for allocation.
    /// </summary>
    public int FreeCount => 256 - AllocatedCount;

    /// <summary>
    /// Gets the number of currently allocated addresses.
    /// </summary>
    public int AllocatedCount { get; private set; }

    /// <summary>
    /// Clears all allocations and reservations.
    /// </summary>
    public void Clear()
    {
        AllocatedCount = 0;
        for (int addr = 0; addr < 0x100; addr++)
        {
            if (!IsSystem((byte)addr))
            {
                _bitmap[GetBitmapIndex((byte)addr)] = false;
                _table[addr] = default;
            }
            else
            {
                AllocatedCount++;
            }
        }
    }

    /// <summary>
    /// Returns a list of all currently allocated zero-page addresses.
    /// </summary>
    /// <returns>A list of <see cref="ZeroPageAddress"/> objects representing the allocated zero-page addresses. The list will be
    /// empty if no addresses are allocated.</returns>
    public List<ZeroPageAddress> GetAllocatedAddresses()
    {
        var addresses = new List<ZeroPageAddress>(AllocatedCount);
        GetAllocatedAddresses(addresses);
        return addresses;
    }

    /// <summary>
    /// Adds all currently allocated zero-page addresses to the specified collection.
    /// </summary>
    /// <param name="addresses">The list to which allocated zero-page addresses will be added. Must not be null.</param>
    public void GetAllocatedAddresses(List<ZeroPageAddress> addresses)
    {
        for(int addr = 0; addr < 0x100; addr++)
        {
            if (TryGetAddress((byte)addr, out var label))
            {
                addresses.Add(label);
            }
        }
    }

    /// <summary>
    /// Determines whether the specified address represents a system address.
    /// </summary>
    /// <remarks>This method cana be derived to implement custom logic about system addresses.
    /// With the 6510, the address $00 and $01 are reserved for defining memory banks with ROMs.
    /// By returning true for these addresses, the allocator can avoid allocating them or clearing them when calling <see cref="Clear"/>.
    /// </remarks>
    /// <param name="address">The address to evaluate.</param>
    /// <returns>true if the specified address is recognized as a system address; otherwise, false.</returns>
    public virtual bool IsSystem(byte address) => false;

    /// <summary>
    /// Determines whether the specified zero-page address is currently allocated or reserved.
    /// </summary>
    /// <param name="address">The zero-page address to check.</param>
    /// <returns>True if the address is allocated; otherwise, false.</returns>
    public bool IsAllocated(byte address)
    {
        var index = GetBitmapIndex(address);
        return _bitmap[index];
    }

    /// <summary>
    /// Attempts to retrieve the <see cref="ZeroPageAddress"/> instance associated with the specified byte address.
    /// </summary>
    /// <param name="address">The zero-page address to look up.</param>
    /// <param name="zeroPageAddress">When this method returns, contains the corresponding <see cref="ZeroPageAddress"/> if found; otherwise, the default value.</param>
    /// <returns>True if the address is allocated and a value was returned; otherwise, false.</returns>
    public bool TryGetAddress(byte address, out ZeroPageAddress zeroPageAddress)
    {
        var index = GetBitmapIndex(address);
        if (_bitmap[index])
        {
            zeroPageAddress = _table[address];
            return true;
        }

        zeroPageAddress = default;
        return false;
    }

    /// <summary>
    /// Allocates a single zero-page address and assigns it to the specified out parameter.
    /// </summary>
    /// <param name="zp">When this method returns, contains the allocated <see cref="ZeroPageAddress"/>.</param>
    /// <param name="zpExpression">The C# expression for <paramref name="zp"/> captured by the compiler.</param>
    /// <returns>The current allocator instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no more zero-page addresses are available.</exception>
    public ZeroPageAllocator Allocate(out ZeroPageAddress zp, [CallerArgumentExpression(nameof(zp))] string? zpExpression = null)
    {
        zp = Allocate(Mos6502Label.ParseCSharpExpression(zpExpression));
        return this;
    }

    /// <summary>
    /// Allocates a contiguous range of zero-page addresses and assigns it to the specified out parameter.
    /// </summary>
    /// <param name="length">The number of addresses to allocate.</param>
    /// <param name="zpRange">When this method returns, contains the allocated <see cref="ZeroPageAddressRange"/>.</param>
    /// <param name="zpExpression">The C# expression for <paramref name="zpRange"/> captured by the compiler.</param>
    /// <returns>The current allocator instance for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown if there is no contiguous range available.</exception>
    public ZeroPageAllocator AllocateRange(byte length, out ZeroPageAddressRange zpRange, [CallerArgumentExpression(nameof(zpRange))] string? zpExpression = null)
    {
        zpRange = AllocateRange(length, Mos6502Label.ParseCSharpExpression(zpExpression));
        return this;
    }
    
    /// <summary>
    /// Allocates a single zero-page address.
    /// </summary>
    /// <param name="name">An optional symbolic name to associate with the address.</param>
    /// <returns>The allocated <see cref="ZeroPageAddress"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no more zero-page addresses are available.</exception>
    public ZeroPageAddress Allocate(string? name = null)
    {
        Span<bool> bitmap = _bitmap;

        var index = bitmap.IndexOf(false);
        if (index < 0) throw new InvalidOperationException("No more zero page address available");
        var address = 0xFF - index;

        var zeroPageAddress = new ZeroPageAddress(name, (byte)address, 0);
        bitmap[index] = true;
        _table[address] = zeroPageAddress;
        AllocatedCount++;
        return zeroPageAddress;
    }
    
    /// <summary>
    /// Allocates a contiguous range of zero-page addresses.
    /// </summary>
    /// <param name="length">The number of addresses to allocate.</param>
    /// <param name="name">An optional symbolic name to associate with the range.</param>
    /// <returns>The allocated <see cref="ZeroPageAddressRange"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown if there is no contiguous range available.</exception>
    public ZeroPageAddressRange AllocateRange(byte length, string? name = null)
    {
        if (length == 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero");

        Span<bool> bitmap = _bitmap;

        int index = 0;
        while (true)
        {
            var relativeIndex = bitmap[index..].IndexOf(false);
            if (relativeIndex < 0) throw new InvalidOperationException($"No consecutive zero page addresses available with the requested length {length}");
            index += relativeIndex;

            var address = 0xFF - (index + length - 1);
            if (address < 0) throw new InvalidOperationException($"No consecutive zero page addresses available with the requested length {length}");

            // Check if we have enough space
            var hasSpace = true;
            for (int i = 0; i < length; i++)
            {
                if (bitmap[index + i])
                {
                    hasSpace = false;
                    break;
                }
            }

            if (hasSpace)
            {
                // Mark as used
                var zeroPageAddress = new ZeroPageAddress(name, (byte)address, 0);
                for (int i = 0; i < length; i++)
                {
                    bitmap[index + i] = true;
                    _table[address + i] = zeroPageAddress + i;
                }
                AllocatedCount += length;
                return new ZeroPageAddressRange(name, (byte)address, (byte)(address + length - 1));
            }

            index++;
        }
    }

    /// <summary>
    /// Frees a previously allocated or reserved zero-page address.
    /// </summary>
    /// <param name="address">The address to free.</param>
    /// <exception cref="ArgumentException">Thrown if the address is not allocated.</exception>
    public void Free(byte address)
    {
        if (IsSystem(address)) throw new ArgumentException($"Address ${address:x2} is a system address and cannot be freed", nameof(address));

        Span<bool> bitmap = _bitmap;
        var index = GetBitmapIndex(address);
        if (!bitmap[index]) throw new ArgumentException($"Address ${address:x2} is not allocated", nameof(address));
        bitmap[index] = false;
        _table[address] = default;
        AllocatedCount--;
    }

    /// <summary>
    /// Frees a previously allocated or reserved <see cref="ZeroPageAddress"/>.
    /// </summary>
    /// <param name="address">The address to free.</param>
    /// <exception cref="ArgumentException">Thrown if the address is not allocated.</exception>
    public void Free(ZeroPageAddress address)
    {
        if (IsSystem(address.Address)) throw new ArgumentException($"Address ${address.Address:x2} is a system address and cannot be freed", nameof(address));

        Span<bool> bitmap = _bitmap;
        var index = GetBitmapIndex(address.Address);
        if (!bitmap[index]) throw new ArgumentException($"Address {address} is not allocated", nameof(address));
        bitmap[index] = false;
        _table[address.Address] = default;
        AllocatedCount--;
    }

    /// <summary>
    /// Frees a previously allocated contiguous range of addresses.
    /// </summary>
    /// <param name="range">The range to free.</param>
    /// <exception cref="ArgumentException">Thrown if any address in the range is not allocated.</exception>
    public void FreeRange(ZeroPageAddressRange range)
    {
        Span<bool> bitmap = _bitmap;
        // Check that range is allocated
        for (int i = 0; i < range.Length; i++)
        {
            var address = (byte)(range.BeginAddress + i);
            if (IsSystem(address)) throw new ArgumentException($"Address ${address:x2} from range {range} is a system address and cannot be freed", nameof(range));
            var index = GetBitmapIndex(address);
            if (!bitmap[index]) throw new ArgumentException($"Address ${address:x2} from range {range} is not allocated", nameof(range));
        }

        for (int i = 0; i < range.Length; i++)
        {
            var address = (byte)(range.BeginAddress + i);
            var index = GetBitmapIndex(address);
            bitmap[index] = false;
            _table[address] = default;
        }

        AllocatedCount -= range.Length;
    }

    /// <summary>
    /// Reserves a specific zero-page address without searching for a free slot.
    /// </summary>
    /// <param name="address">The address to reserve.</param>
    /// <param name="name">An optional symbolic name to associate with the address.</param>
    /// <returns>The reserved <see cref="ZeroPageAddress"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the address is already allocated.</exception>
    public ZeroPageAddress Reserve(byte address, string? name = null) => Reserve(new ZeroPageAddress(name, address, 0));

    /// <summary>
    /// Reserves a specific <see cref="ZeroPageAddress"/> without searching for a free slot.
    /// </summary>
    /// <param name="address">The address instance to reserve.</param>
    /// <returns>The reserved <see cref="ZeroPageAddress"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the address is already allocated.</exception>
    public ZeroPageAddress Reserve(ZeroPageAddress address)
    {
        Span<bool> bitmap = _bitmap;
        var index = GetBitmapIndex(address.Address);
        if (bitmap[index]) throw new ArgumentException($"Address {address} is already allocated", nameof(address));
        bitmap[index] = true;
        _table[address] = address;
        AllocatedCount++;
        return address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBitmapIndex(byte address) => 0xFF - address;

    [InlineArray(256)]
    private struct ZeroPageAddressTable
    {
        private ZeroPageAddress _element;
    }

    [InlineArray(256)]
    private struct ZeroPageAddressBitmap
    {
        private bool _element;
    }

    /// <summary>
    /// Internal class used to display allocated addresses in debugger.
    /// </summary>
    private class CustomDebuggerTypeProxy(ZeroPageAllocator allocator)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ZeroPageAddress[] Allocated
        {
            get
            {
                var result = new List<ZeroPageAddress>(allocator.AllocatedCount);
                Span<bool> bitmap = allocator._bitmap;
                Span<ZeroPageAddress> table = allocator._table;
                for (int i = 0xFF; i >= 0; i--)
                {
                    if (bitmap[i])
                    {
                        var address = (byte)(0xFF - i);
                        result.Add(table[address]);
                    }
                }
                return result.ToArray();
            }
        }
    }
}