// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using static AsmMos6502.Mos6502Factory;

namespace AsmMos6502.Tests;

[TestClass]
public class Mos6502AssemblerSpecialTests : VerifyAsmMos6502Base
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
        using var asm = CreateAsm();

        var forwardLabel = new Mos6502Label();

        asm
            .Begin()
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

        asm
            .Begin(0xc000)
            .Label("START", out var startLabel)
            .LDX_Imm(0x00)             // X = 0, index into buffer
            .LDY_Imm(0x10)             // Y = 16, number of bytes to process

            .Label("LOOP", out var loopLabel)
            .LDA(0x0200, X) // Load byte at $0200 + X
            .CMP_Imm(0xFF)             // Check if byte is already 0xFF

            .LabelForward("SKIP", out var skipLabel)
            .BEQ(skipLabel)        // If so, skip incrementing
            .CLC()                 // Clear carry before addition
            .ADC_Imm(0x01)             // Add 1
            .STA(0x0200, X) // Store result back to memory

            .Label(skipLabel)      // X = X + 1
            .INX()
            .DEY()                 // Y = Y - 1
            .BNE(loopLabel)        // Loop until Y == 0

            // Call subroutine to flash border color
            .LabelForward("FLASH_BORDER", out var flashBorderLabel)
            .JSR(flashBorderLabel)

            // Infinite loop
            .Label("END", out var endLabel)
            .JMP(endLabel)

            // ------------------------------
            // Subroutine: FLASH_BORDER
            // Cycles border color between 0–7
            // (Useful on C64, otherwise dummy)
            // -----------------------------
            .Label(flashBorderLabel)
            .LDX_Imm(0x00)

            .Label("FLASH_LOOP", out var flashLoopLabel)
            .STX(0xD020) // C64 border color register
            .INX()
            .CPX_Imm(0x08)
            .BNE(flashLoopLabel)
            .RTS()

            .End();                 // Mark the end of the assembly (to resolve labels)

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
}
