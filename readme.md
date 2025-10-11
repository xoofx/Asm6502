# Asm6502 [![ci](https://github.com/xoofx/Asm6502/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/Asm6502/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/Asm6502.svg)](https://www.nuget.org/packages/Asm6502/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/Asm6502/main/img/Asm6502.png">

Asm6502 is a lightweight C# library for the 6502/6510 that combines a fluent, strongly typed **assembler**/**disassembler** with a cycle-accurate **CPU emulator** (pluggable 64 KiB memory bus). Use it to generate binaries, disassemble existing code, and run or step programs with precise timing.

## ✨ Features

- Assembler/disassembler with **full support** for all core 6502 instructions and 6510 instructions (6502 + undocumented opcodes)
- New: **cycle-accurate 6502/6510 CPU emulator** (use `Mos6510Cpu` for full opcode coverage; `Mos6502Cpu` for documented opcodes)
  - Accurate cycle timing and passes known 6502 timing from [Thomas Harte's 2,560,000 tests for the 6502](https://github.com/SingleStepTests/65x02/tree/main/6502)
  - **Pluggable 64 KiB memory bus** via `IMos6502CpuMemoryBus`
- Unique **strongly typed** and fluent assembler API
- Support producing **debug information** (C# file and line numbers) for each instruction
- **Easily disassemble** instructions and operand.
- **High performance** / **zero allocation** library for disassembling / assembling instructions.
- Compatible with `net8.0+` and NativeAOT.
- Integrated assembler API documentation via API XML comments.
    ![Integrated API documentation](https://raw.githubusercontent.com/xoofx/Asm6502/main/img/asm6502_xml_api_example.png)

## 📖 User Guide

For more details on how to use Asm6502, please visit the [user guide](https://github.com/xoofx/Asm6502/blob/main/doc/readme.md).

## 🧠 6502 CPU Emulator

Asm6502 includes a cycle-accurate CPU emulator with two variants:

- `Mos6502Cpu`: documented 6502 instruction set
- `Mos6510Cpu`: full instruction set including all undocumented opcodes (recommended)

Both reuse the same decode tables as the assembler/disassembler and expose a simple, pluggable 64 KiB memory bus.

Quick start:

```csharp
using Asm6502;

// Minimal 64 KiB RAM bus
public sealed class RamBus : IMos6502CpuMemoryBus
{
    private readonly byte[] _ram;
    public RamBus(byte[] ram) => _ram = ram;
    public byte Read(ushort address) => _ram[address];
    public void Write(ushort address, byte value) => _ram[address] = value;
}

// Create memory and a tiny program at $C000: LDA #$01; ADC #$01;
var mem = new byte[65536];
mem[0xC000] = 0xA9;
mem[0xC001] = 0x01; // LDA #$01
mem[0xC002] = 0x69;
mem[0xC003] = 0x01; // ADC #$01

// Set Reset vector to $C000
mem[0xFFFC] = 0x00;
mem[0xFFFD] = 0xC0;

var cpu = new Mos6510Cpu(new RamBus(mem));
cpu.Reset(); // fetch reset vector and begin executing
cpu.Steps(2); // Run 2 instructions (LDA and ADC)
var a = cpu.A; // 2
// cpu.PC is at 0xC004 (next instruction)
```

Notes:
- RMW instructions perform read + two writes; your bus should tolerate that pattern.
- Use `cpu.Step()`/`cpu.Steps(n)` for instruction stepping, `cpu.Cycle()`/`cpu.Cycles(n)` for cycle stepping.
- `cpu.Nmi()`, `cpu.Irq()`, and `cpu.Reset()` helpers are provided; `RaiseNmi/Irq/Reset` schedule the interrupt on the next cycle.
- `InstructionCycles` reports cycles for the last completed instruction; `TimestampCounter` is a monotonic cycle counter.
- The implementation targets accurate cycle timing and passes known 6502 timing test suites.

## 🧪 6502 Assembler/Disassembler Example

Suppose the following 6502 assembly code:

```
       .org $c000     ; Start address
       
       ; Initialization
start: LDX #$00       ; X = 0, index into buffer
       LDY #$10       ; Y = 16, number of bytes to process
       
loop:  LDA $0200,X    ; Load byte at $0200 + X
       CMP #$FF       ; Check if byte is already 0xFF
       BEQ skip       ; If so, skip incrementing
       
       CLC            ; Clear carry before addition
       ADC #$01       ; Add 1
       STA $0200,X    ; Store result back to memory
       
skip:  INX            ; X = X + 1
       DEY            ; Y = Y - 1
       BNE loop       ; Loop until Y == 0
       
       ; Call subroutine to flash border color
       JSR flash_border
       
       ; Infinite loop
end:   JMP end

; ------------------------------
; Subroutine: flash_border
; Cycles border color between 0–7
; (Useful on C64, otherwise dummy)
; ------------------------------
flash_border:
        LDX #$00

flash_loop:
        STX $D020     ; C64 border color register
        INX
        CPX #$08
        BNE flash_loop

        RTS
```


And the equivalent in C# using `Asm6502` library:

```csharp
using var asm = new Mos6510Assembler();
asm.Org(0xc000);

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

    .End();            // Resolve labels

// Get the assembled buffer
var buffer = asm.Buffer;
```


Disassembling the same code can be done using the `Mos6502Disassembler` class:
```csharp
var dis = new Mos6510Disassembler(new Mos6502DisassemblerOptions()
{
    PrintLabelBeforeFirstInstruction = false,
    PrintAddress = true,
    PrintAssemblyBytes = true,
});

var asmText = dis.Disassemble(asm.Buffer);
Console.WriteLine(asmText);
```

Will generate the following disassembled code:

```
C000  A2 00      LDX #$00
C002  A0 10      LDY #$10

LL_02:
C004  BD 00 02   LDA $0200,X
C007  C9 FF      CMP #$FF
C009  F0 06      BEQ LL_01

C00B  18         CLC
C00C  69 01      ADC #$01
C00E  9D 00 02   STA $0200,X

LL_01:
C011  E8         INX
C012  88         DEY
C013  D0 EF      BNE LL_02

C015  20 1B C0   JSR LL_03

LL_04:
C018  4C 18 C0   JMP LL_04

LL_03:
C01B  A2 00      LDX #$00

LL_05:
C01D  8E 20 D0   STX $D020
C020  E8         INX
C021  E0 08      CPX #$08
C023  D0 F8      BNE LL_05

C025  60         RTS
```

## 🪪 License

This software is released under the [BSD-2-Clause license](https://opensource.org/licenses/BSD-2-Clause). 

## 🌟 Credits

Thanks to Norbert Landsteiner for providing the [6502 Instruction Set](https://www.masswerk.at/6502/6502_instruction_set.html).

Thanks to Jacob Paul for providing a C++ version cycle accurate version of the [6502 CPU](https://github.com/ericssonpaul/O2). See [Mos6502Cpu.cs](https://github.com/xoofx/Asm6502/blob/main/src/Asm6502/Mos6502Cpu.cs#L5-L35) for more details about the improvements made in Asm6502.

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
