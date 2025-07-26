# AsmMos6502 [![ci](https://github.com/xoofx/AsmMos6502/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/AsmMos6502/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/AsmMos6502.svg)](https://www.nuget.org/packages/AsmMos6502/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/AsmMos6502/main/img/AsmMos6502.png">

AsmMos6502 is a lightweight and efficient C# library to assemble and disassemble 6502 assembly code. It provides a fluent API to create 6502 assembly code (e.g. a CPU powering the Commodore 64), and can be used to generate binary files or disassemble existing binaries into assembly code.

## ✨ Features

- **Full support** for all **core 6502 instructions
- Unique **strongly typed** assembler API
- **Easily disassemble** instructions and operand.
- **High performance** / **zero allocation** library for disassembling / assembling instructions.
- Compatible with `net8.0+` and NativeAOT.

## 📖 User Guide

Suppose that we want to write a simple program in C# to assemble and disassemble the equivalent of the following 6502 assembly code:

```
        .org $c000             ; Start address (for example, on C64 this is an available memory area)

        ; Initialization
START:  LDX #$00               ; X = 0, index into buffer
        LDY #$10               ; Y = 16, number of bytes to process

LOOP:   LDA $0200,X            ; Load byte at $0200 + X
        CMP #$FF               ; Check if byte is already 0xFF
        BEQ SKIP               ; If so, skip incrementing

        CLC                    ; Clear carry before addition
        ADC #$01               ; Add 1
        STA $0200,X            ; Store result back to memory

SKIP:   INX                    ; X = X + 1
        DEY                    ; Y = Y - 1
        BNE LOOP               ; Loop until Y == 0

        ; Call subroutine to flash border color
        JSR FLASH_BORDER

        ; Infinite loop
END:    JMP END

; ------------------------------
; Subroutine: FLASH_BORDER
; Cycles border color between 0–7
; (Useful on C64, otherwise dummy)
; ------------------------------
FLASH_BORDER:
        LDX #$00

FLASH_LOOP:
        STX $D020              ; C64 border color register
        INX
        CPX #$08
        BNE FLASH_LOOP

        RTS
```

The following C# assembly would assemble this code using the `AsmMos6502` library:
```csharp
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

var buffer = asm.Buffer; // Get the assembled buffer
```

For more details on how to use AsmMos6502, please visit the [user guide](https://github.com/xoofx/AsmMos6502/blob/main/doc/readme.md).

## 🪪 License

This software is released under the [BSD-2-Clause license](https://opensource.org/licenses/BSD-2-Clause). 

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
