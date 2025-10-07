// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Tests;

[TestClass]
public class ZeroPageAllocatorTests
{
    [TestMethod]
    public void TestSimpleAllocation()
    {
        var zpAlloc = new ZeroPageAllocator();
        Assert.AreEqual(256, zpAlloc.FreeCount);
        Assert.AreEqual(0, zpAlloc.AllocatedCount);

        zpAlloc.Allocate(out var zp1);
        Assert.AreEqual(255, zpAlloc.FreeCount);
        Assert.AreEqual(1, zpAlloc.AllocatedCount);
        Assert.AreEqual(0xFF, zp1.Address);
        Assert.AreEqual("zp1", zp1.Name);

        zpAlloc.Allocate(out var zp2);
        Assert.AreEqual(254, zpAlloc.FreeCount);
        Assert.AreEqual(2, zpAlloc.AllocatedCount);
        Assert.AreEqual(0xFE, zp2.Address);
        Assert.AreEqual("zp2", zp2.Name);

        zpAlloc.Free(zp1);
        Assert.AreEqual(255, zpAlloc.FreeCount);
        Assert.AreEqual(1, zpAlloc.AllocatedCount);
        zpAlloc.Free(zp2);
        Assert.AreEqual(256, zpAlloc.FreeCount);
        Assert.AreEqual(0, zpAlloc.AllocatedCount);
    }

    [TestMethod]
    public void TestToString()
    {
        var zp = new ZeroPageAddress("hello", 0x1f, 0);
        Assert.AreEqual("hello: 0x1f", zp.ToString());
        Assert.AreEqual("hello+1: 0x20", (zp + 1).ToString());
        Assert.AreEqual("hello-1: 0x1e", (zp - 1).ToString());

        var zpRange = new ZeroPageAddressRange("range", 0x1f, 0x30);
        Assert.AreEqual("range", zpRange.Name);
        Assert.AreEqual(0x1f, zpRange.BeginAddress);
        Assert.AreEqual(0x30, zpRange.EndAddress);
        Assert.AreEqual(0x30 - 0x1f + 1, zpRange.Length);
        Assert.AreEqual("range: 0x1f-0x30", zpRange.ToString());
    }

    [TestMethod]
    public void TestRange()
    {
        var zpRange = new ZeroPageAddressRange("range", 0x1f, 0x30);
        Assert.IsTrue(zpRange.Contains(0x1f));
        Assert.IsTrue(zpRange.Contains(0x20));
        Assert.IsTrue(zpRange.Contains(0x30));
        Assert.IsFalse(zpRange.Contains(0x1e));

        var zp0 = zpRange[0];
        Assert.AreEqual(0x1f, zp0.Address);
        var zp1 = zpRange[1];
        Assert.AreEqual(zpRange.BeginAddress, zp1.BaseAddress);
        Assert.AreEqual(0x20, zp1.Address);
        Assert.AreEqual(1, zp1.Offset);
        Assert.AreEqual("range+1: 0x20", zp1.ToString());
    }

    [TestMethod]
    public void TestRangeAllocation()
    {
        var zpAlloc = new ZeroPageAllocator();
        Assert.AreEqual(256, zpAlloc.FreeCount);
        Assert.AreEqual(0, zpAlloc.AllocatedCount);

        zpAlloc.AllocateRange(10, out var range);
        Assert.AreEqual(246, zpAlloc.FreeCount);
        Assert.AreEqual(10, zpAlloc.AllocatedCount);
        Assert.AreEqual(0xF6, range.BeginAddress);
        Assert.AreEqual(0xFF, range.EndAddress);
        Assert.AreEqual(10, range.Length);
        Assert.AreEqual("range", range.Name);

        zpAlloc.AllocateRange(2, out var range2);
        Assert.AreEqual(244, zpAlloc.FreeCount);
        Assert.AreEqual(12, zpAlloc.AllocatedCount);
        Assert.AreEqual(0xF4, range2.BeginAddress);
        Assert.AreEqual(0xF5, range2.EndAddress);
        Assert.AreEqual(2, range2.Length);
        Assert.AreEqual("range2", range2.Name);
        
        for (int i = 0; i < range.Length; i++)
        {
            var addr = range[i];
            Assert.AreEqual((byte)(0xF6 + i), addr.Address);
            Assert.AreEqual("range", addr.Name);
            Assert.AreEqual($"range{(addr.Offset != 0 ? $"+{addr.Offset}":"")}: 0x{addr.Address:x2}", addr.ToString());
        }
    }

    [TestMethod]
    public void TestRangeAllocation2()
    {
        var zpAlloc = new ZeroPageAllocator();
        for (byte i = 0; i < 246; i += 2)
        {
            zpAlloc.Reserve((byte)(255 - i), $"reserved{i}");
        }

        zpAlloc.AllocateRange(11, out var testRange);
        Assert.AreEqual(11, testRange.Length);
        Assert.AreEqual((byte)0, testRange.BeginAddress);
        Assert.AreEqual((byte)10, testRange.EndAddress);

        Assert.Throws<InvalidOperationException>(() => zpAlloc.AllocateRange(2, out var failed));

        var freeCount = zpAlloc.FreeCount;
        for (byte i = 0; i < freeCount; i++)
        {
            zpAlloc.Allocate($"zpFinal{i}");
        }

        Assert.AreEqual(0, zpAlloc.FreeCount);

        Assert.Throws<InvalidOperationException>(() => zpAlloc.Allocate("zpFailed"));
    }
}