// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using static AsmMos6502.Mos6502Factory;

namespace AsmMos6502.Tests;

[TestClass]
public class Mos6502AssemblerSpecialTests : VerifyAsmBase
{
    [TestMethod]
    public void TestAppendBuffer()
    {
        using var asm = new Mos6502Assembler();
        // Append a buffer of 3 bytes
        asm.AppendBuffer([0x01, 0x02, 0x03]);
        // Check the size in bytes
        Assert.AreEqual(3, asm.SizeInBytes);
        // Check the content of the buffer
        var buffer = asm.Buffer;
        Assert.AreEqual(0x01, buffer[0]);
        Assert.AreEqual(0x02, buffer[1]);
        Assert.AreEqual(0x03, buffer[2]);
    }

    [TestMethod]
    public void TestAppendBytes()
    {
        using var asm = new Mos6502Assembler();
        // Append 5 bytes with value 0xFF
        asm.AppendBytes(5, 0xFF);
        // Check the size in bytes
        Assert.AreEqual(5, asm.SizeInBytes);
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
        using var asm = new Mos6502Assembler();

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
        using var asm = new Mos6502Assembler(0xc000);

        asm.Label("START", out var startLabel);
        asm.LDX(0x00);             // X = 0, index into buffer
        asm.LDY(0x10);             // Y = 16, number of bytes to process

        asm.Label("LOOP", out var loopLabel);
        asm.LDA(0x0200, X);        // Load byte at $0200 + X
        asm.CMP(0xFF);             // Check if byte is already 0xFF
        var skipLabel = new Mos6502Label("SKIP");
        asm.BEQ(skipLabel);        // If so, skip incrementing

        asm.CLC();                 // Clear carry before addition
        asm.ADC(0x01);             // Add 1
        asm.STA(0x0200, X);        // Store result back to memory

        asm.Label(skipLabel);      // X = X + 1
        asm.INX();
        asm.DEY();                 // Y = Y - 1
        asm.BNE(loopLabel);        // Loop until Y == 0

        // Call subroutine to flash border color
        var flashBorderLabel = new Mos6502Label("FLASH_BORDER");
        asm.JSR(flashBorderLabel);

        // Infinite loop
        asm.Label("END", out var endLabel);
        asm.JMP(endLabel);

        // ------------------------------
        // Subroutine: FLASH_BORDER
        // Cycles border color between 0–7
        // (Useful on C64, otherwise dummy)
        // ------------------------------
        asm.Label(flashBorderLabel);
        asm.LDX(0x00);

        asm.Label("FLASH_LOOP", out var flashLoopLabel);
        asm.STX(0xD020);           // C64 border color register
        asm.INX();
        asm.CPX(0x08);
        asm.BNE(flashLoopLabel);
        asm.RTS();

        asm.End();                 // Mark the end of the assembly (to resolve labels)

        await VerifyAsm(asm);
    }
}
