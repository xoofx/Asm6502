// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using static Asm6502.Mos6502Factory;
// ReSharper disable InconsistentNaming

namespace Asm6502.Tests;

[TestClass]
public class Mos6502AssemblerSpecialTests : VerifyMos6502Base
{
    [TestMethod]
    public void TestAppendBuffer()
    {
        using var asm = new Mos6502Assembler();
        // Append a buffer of 3 bytes
        asm.Append([0x01, 0x02, 0x03]);
        // Check the size in bytes
        Assert.AreEqual(3, asm.SizeInBytes);
        Assert.AreEqual(3, asm.CurrentOffset);
        // Check the content of the buffer
        var buffer = asm.Buffer;
        Assert.AreEqual(0x01, buffer[0]);
        Assert.AreEqual(0x02, buffer[1]);
        Assert.AreEqual(0x03, buffer[2]);

        asm.Org(0xE000);
        // Append another buffer of 2 bytes at new origin
        asm.Append([0xAA, 0xBB]);
        Assert.AreEqual(5, asm.SizeInBytes);
        Assert.AreEqual(2, asm.CurrentOffset);
    }

    [TestMethod]
    public void TestAppendBytes()
    {
        using var asm = new Mos6502Assembler();
        // Append 5 bytes with value 0xFF
        asm.AppendBytes(5, 0xFF);
        // Check the size in bytes
        Assert.AreEqual(5, asm.SizeInBytes);
        Assert.AreEqual(5, asm.CurrentOffset);
        // Check the content of the buffer
        var buffer = asm.Buffer;
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(0xFF, buffer[i]);
        }
    }

    [TestMethod]
    public async Task TestSimple()
    {
        using var asm = CreateAsm();

        var forwardLabel = new Mos6502Label();

        asm
            .LDA(0x5)
            .STA(0x1000)
            .Label(out var label)
            .LDA(_[0x1, X])
            .LDA(_[0x2], Y)
            .BEQ(label)
            .BCC(forwardLabel)
            .RTS()
            .Label(forwardLabel)
            .End();

        await VerifyAsm(asm);
    }

    [TestMethod]
    public async Task TestComplex()
    {
        // Start address (for example, on C64 this is an available memory area)
        using var asm = CreateAsm();

        asm.BeginCodeSection("HelloWorld");

        // Initialization
        asm.Label(out var start)
            .LDX_Imm(0x00)     // X = 0, index into buffer
            .LDY_Imm(0x10);    // Y = 16, number of bytes to process

        asm.Label(out var loop)
            .LDA(0x0200, X)    // Load byte at $0200 + X
            .CMP_Imm(0xFF)     // Check if byte is already 0xFF
            .BEQ(out var skip) // If so, skip incrementing (forward label)

            .CLC()             // Clear carry before addition
            .ADC_Imm(0x01)     // Add 1
            .STA(0x0200, X);   // Store result back to memory

        asm.Label(skip)        // X = X + 1
            .INX()
            .DEY()             // Y = Y - 1
            .BNE(loop)         // Loop until Y == 0

            // Call subroutine to flash border color
            .JSR(out var flash_border); // Declare a forward label

        // Infinite loop
        asm.Label(out var end)
            .JMP(end);

        // ------------------------------
        // Subroutine: FLASH_BORDER
        // Cycles border color between 0–7
        // (Useful on C64, otherwise dummy)
        // -----------------------------
        asm.Label(flash_border)
            .LDX_Imm(0x00);

        asm.Label(out var flash_loop)
            .STX(0xD020)       // C64 border color register
            .INX()
            .CPX_Imm(0x08)
            .BNE(flash_loop)
            .RTS()

            .EndCodeSection()
            .End();            // Resolve labels
        

        await VerifyAsm(asm);
    }

    [TestMethod]
    public async Task TestAddressingModes()
    {
        using var asm = CreateAsm();
        asm
            .Begin()
            .LDA_Imm(0x10)   // Immediate
            .LDA(0x10)        // Zero Page
            .LDA(0x10, X)     // Zero Page,X
            .LDX(0x10, Y)     // Zero Page,Y
            .LDA(0x1234)       // Absolute
            .LDA(0x1234, X)    // Absolute,X
            .LDA(0x1234, Y)    // Absolute,Y
            .JMP(_[0x1234])           // Indirect
            .LDA(_[0x10, X])          // Indirect,X
            .LDA(_[0x10], Y)          // Indirect,Y
            .End();
        await VerifyAsm(asm);
    }

    [TestMethod]
    public void TestOrg()
    {
        using var asm = new Mos6502Assembler();
        asm.Org(0xE000);
        Assert.AreEqual(0xE000, asm.CurrentAddress);
        Assert.AreEqual(0, asm.CurrentOffset);
        Assert.AreEqual(0, asm.SizeInBytes);
        asm.LDA_Imm(0x10);
        Assert.AreEqual(0xE002, asm.CurrentAddress);
        Assert.AreEqual(2, asm.CurrentOffset);
        Assert.AreEqual(2, asm.SizeInBytes);
        asm.Org(0xC000);
        Assert.AreEqual(0xC000, asm.CurrentAddress);
        Assert.AreEqual(0, asm.CurrentOffset);
        Assert.AreEqual(2, asm.SizeInBytes);
        asm.LDA_Imm(0x20);
        Assert.AreEqual(0xC002, asm.CurrentAddress);
        Assert.AreEqual(2, asm.CurrentOffset);
        Assert.AreEqual(4, asm.SizeInBytes);
    }


    [TestMethod]
    public async Task TestOrgWithLabels()
    {
        using var asm = new Mos6502Assembler()
        {
            DebugMap = new Mos6502AssemblerDebugMap()
        };
        asm.Begin(0xC000, TestContext.TestName);
        asm.BeginCodeSection("Code1");
        asm.LDA_Imm(0);
        asm.Label("LABEL1", out var label1);
        asm.LabelForward("LABEL2", out var label2);
        asm.JMP(label2);
        asm.EndCodeSection();

        asm.BeginDataSection("Data1");

        // Fill with NOPs to reach address 0xC010
        asm.AppendBytes(0x10 - asm.SizeInBytes, (byte)Mos6502OpCode.NOP_Implied);

        asm.EndDataSection();

        asm.Org(0xC010);
        asm.BeginCodeSection("Code2");
        asm.LDA_Imm(1);
        asm.Label(label2);
        asm.JMP(label1);
        asm.EndCodeSection();

        asm.End();
        
        await VerifyAsm(asm);
    }
}
