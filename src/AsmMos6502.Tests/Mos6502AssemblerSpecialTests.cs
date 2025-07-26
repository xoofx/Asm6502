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
        //         .org $c000             ; Start address (for example, on C64 this is an available memory area)
        using var asm = new Mos6502Assembler(0xc000);
        //         ; Initialization
        // START:  LDX #$00               ; X = 0, index into buffer
        asm.Label("START", out var startLabel);
        asm.LDX(0x00);

        //         LDY #$10               ; Y = 16, number of bytes to process
        asm.LDY(0x10);

        // LOOP:   LDA $0200,X            ; Load byte at $0200 + X
        asm.Label("LOOP", out var loopLabel);
        asm.LDA(0x0200, X);

        //         CMP #$FF               ; Check if byte is already 0xFF
        asm.CMP(0xFF);

        //         BEQ SKIP               ; If so, skip incrementing
        var skipLabel = new Mos6502Label("SKIP");
        asm.BEQ(skipLabel);

        //         CLC                    ; Clear carry before addition
        asm.CLC();

        //         ADC #$01               ; Add 1
        asm.ADC(0x01);

        //         STA $0200,X            ; Store result back to memory
        asm.STA(0x0200, X);

        // SKIP:   INX                    ; X = X + 1
        asm.Label(skipLabel);
        asm.INX();

        //         DEY                    ; Y = Y - 1
        asm.DEY();

        //         BNE LOOP               ; Loop until Y == 0
        asm.BNE(loopLabel);

        //         ; Call subroutine to flash border color
        //         JSR FLASH_BORDER
        var flashBorderLabel = new Mos6502Label("FLASH_BORDER");
        asm.JSR(flashBorderLabel);

        //         ; Infinite loop
        // END:    JMP END
        asm.Label("END", out var endLabel);
        asm.JMP(endLabel);

        // ; ------------------------------
        // ; Subroutine: FLASH_BORDER
        // ; Cycles border color between 0–7
        // ; (Useful on C64, otherwise dummy)
        // ; ------------------------------
        asm.Label(flashBorderLabel);

        // FLASH_BORDER:
        //         LDX #$00
        asm.LDX(0x00);

        // FLASH_LOOP:
        asm.Label("FLASH_LOOP", out var flashLoopLabel);

        //         STX $D020              ; C64 border color register
        asm.STX(0xD020);

        //         INX
        asm.INX();

        //         CPX #$08
        asm.CPX(0x08);

        //         BNE FLASH_LOOP
        asm.BNE(flashLoopLabel);

        //         RTS
        asm.RTS();

        asm.End();

        await VerifyAsm(asm);
    }
}
