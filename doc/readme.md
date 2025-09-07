# AsmMos6502 User Guide

This document provides a small user guide for the AsmMos6502 library.

---

## Table of Contents

- [Table of Contents](#table-of-contents)
- [Quick Start](#quick-start)
  - [Assembling 6502 Code](#assembling-6502-code)
  - [Disassembling 6502 Code](#disassembling-6502-code)
- [Assembling 6502 Code](#assembling-6502-code-1)
  - [Example: Loop with Labels](#example-loop-with-labels)
  - [Appending Raw Bytes](#appending-raw-bytes)
- [Disassembling 6502 Code](#disassembling-6502-code-1)
  - [Basic Disassembly](#basic-disassembly)
  - [Customizing Output](#customizing-output)
- [Advanced Usage](#advanced-usage)
  - [Labels and Branches](#labels-and-branches)
  - [Expressions](#expressions)
  - [Customizing Disassembly Output](#customizing-disassembly-output)
  - [Assembler Debug Line Information](#assembler-debug-line-information)
- [Tips and Best Practices](#tips-and-best-practices)

---

## Quick Start

### Assembling 6502 Code

```csharp
using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

// Create an assembler 
using var asm = new Mos6502Assembler();

asm.Begin(0xC000);  // With a base address (e.g., $C000)

asm.LDX_Imm(0x00);      // LDX #$00
asm.LDY_Imm(0x10);      // LDY #$10
asm.LDA(0x0200, X); // LDA $0200,X
asm.STA(0x0200, X); // STA $0200,X
asm.RTS();          // RTS

asm.End(); // Finalize assembly (resolves labels)

// Get the assembled machine code
var buffer = asm.Buffer;
```

With a more fluent syntax:
```csharp
using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

// Create an assembler with a base address (e.g., $C000)
using var asm = new Mos6502Assembler(0xC000);

asm
    .Begin()        // Start assembling

    .LDX_Imm(0x00)      // LDX #$00
    .LDY_Imm(0x10)      // LDY #$10
    .LDA(0x0200, X) // LDA $0200,X
    .STA(0x0200, X) // STA $0200,X
    .RTS()          // RTS

    .End(); // Finalize assembly (resolves labels)

// Get the assembled machine code
var buffer = asm.Buffer;
```

### Disassembling 6502 Code

```csharp
using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

var dis = new Mos6502Disassembler(new Mos6502DisassemblerOptions {
    PrintLabelBeforeFirstInstruction = false,
    PrintAddress = true,
    PrintAssemblyBytes = true,
});

string asmText = dis.Disassemble(buffer);
Console.WriteLine(asmText);
```

---

## Assembling 6502 Code

The `Mos6502Assembler` class provides a fluent API for generating 6502 machine code. Each method corresponds to a 6502 instruction. Labels and branches are supported and resolved automatically.

### Example: Loop with Labels

```csharp
using var asm = new Mos6502Assembler();

asm
    .Begin(0xC000)         // Start assembling at address $C000
    .Label("START", out var startLabel)
    .LDX_Imm(0x00)         // X = 0
    .LDY_Imm(0x10)         // Y = 16
    .Label("LOOP", out var loopLabel)
    .LDA(0x0200, X)        // LDA $0200,X
    .CMP(0xFF)             // CMP #$FF
    .ForwardLabel("SKIP", out var skipLabel)
    .BEQ(skipLabel)        // BEQ SKIP
    .CLC()                 // CLC
    .ADC_Imm(0x01)         // ADC #$01
    .STA(0x0200, X)        // STA $0200,X
    .Label(skipLabel)
    .INX()                 // INX
    .DEY()                 // DEY
    .BNE(loopLabel)        // BNE LOOP
    .Label("END", out var endLabel)
    .JMP(endLabel)
    .End();

```

### Appending Raw Bytes

You can append arbitrary bytes or fill memory regions:

```csharp
asm
    .AppendBuffer([0x01, 0x02, 0x03])  // Appends 3 bytes
    .AppendBytes(5, 0xFF);             // Appends 5 bytes of value 0xFF
```

---

## Disassembling 6502 Code

The `Mos6502Disassembler` class converts machine code back to readable assembly. Output formatting is highly customizable via `Mos6502DisassemblerOptions`.

### Basic Disassembly

```csharp
var dis = new Mos6502Disassembler();
string asmText = dis.Disassemble(buffer);
Console.WriteLine(asmText);
```

### Customizing Output

Options include:
- Show addresses and bytes
- Label formatting
- Indentation and padding
- Custom comments

Example:

```csharp
var options = new Mos6502DisassemblerOptions {
    PrintAddress = true,
    PrintAssemblyBytes = true,
    IndentSize = 4,
    InstructionTextPaddingLength = 20,
    PrintLabelBeforeFirstInstruction = false,
};
var dis = new Mos6502Disassembler(options);
```

### Addressing Modes

The assembler supports all 6502 addressing modes. Below is a summary of the addressing modes with examples in both assembly and C# syntax:


| Addressing Mode | ASM Example       | C# Example              | Description                          |
|-----------------|-------------------|-------------------------|--------------------------------------|
| Immediate       | `LDA #$10`        | `asm.LDA_Imm(0x10)`     | Load immediate value into A          |
| Zero Page       | `LDA $10`         | `asm.LDA(0x10)`         | Load from zero page address          |
| Zero Page,X     | `LDA $10,X`       | `asm.LDA(0x10, X)`      | Load from zero page address + X      |
| Zero Page,Y     | `LDX $10,Y`       | `asm.LDX(0x10, Y)`      | Load from zero page address + Y      |
| Absolute        | `LDA $1234`       | `asm.LDA(0x1234)`       | Load from absolute address           |
| Absolute,X      | `LDA $1234,X`     | `asm.LDA(0x1234, X)`    | Load from absolute address + X       |
| Absolute,Y      | `LDA $1234,Y`     | `asm.LDA(0x1234, Y)`    | Load from absolute address + Y       |
| Indirect        | `JMP ($1234)`     | `asm.JMP(_[0x1234])`    | Jump to address stored at $1234      |
| (Indirect,X)    | `LDA ($10,X)`     | `asm.LDA(_[0x10, X])`   | Load from address at (zero page + X) |
| (Indirect),Y    | `LDA ($10),Y`     | `asm.LDA(_[0x10], Y)`   | Load from address at (zero page) + Y |

Notice that immediate values are suffixed with `_Imm`, and indirect addressing uses the `_[]` syntax. The registers `X` and `Y` are used directly in the method calls.

In order to access the X and Y registers as well as the indirect addressing modes, the following syntax is used in C#:

```csharp
using static AsmMos6502.Mos6502Factory;
```

---

## Advanced Usage

### Labels and Branches

Labels can be created and bound to addresses. Branch instructions (e.g., BEQ, BNE) can reference labels, even forward-declared ones. The assembler will resolve all label addresses when `End()` is called.

```csharp
asm
    .Label("LOOP", out var loopLabel)
    .BNE(loopLabel);
```

You can also create forward labels that are resolved later:
```csharp
asm
    .ForwardLabel("SKIP", out var skipLabel)
    .BEQ(skipLabel)    // Branch to SKIP if condition met
    .LDA_Imm(0xFF)     // Load accumulator with 0xFF
    .Label(skipLabel)  // Bind SKIP label later
```

### Expressions

It is possible to use expressions for memory addresses and immediate values, such as:

- Storing the low byte and high byte of an address
  ```csharp
  asm
      .Begin()
      .LabelForward(out var forwardLabel)
      .LDA_Imm(forwardLabel.LowByte())
      .STA(0x1000)
      .LDA_Imm(forwardLabel.HighByte())
      .STA(0x1001)
      .RTS()
      .Label(forwardLabel)
      .End();
  ```
- Storing the difference of value between two labels
  ```csharp
  asm
    .Begin()
    .Label("start", out var startLabel)
    .LabelForward("end", out var endLabel)
    .LDA_Imm((endLabel - startLabel).LowByte()) // Store the size of this code
    .STA(0x1000)
    .RTS()
    .Label(endLabel)
    .End();
  ```
- The address of label + a const value
  ```csharp
  asm
    .Begin()
    .LabelForward("end", out var endLabel)
    .LDA(endLabel + 1) // Load A with the address of endLabel + 1
    .RTS()
    .Label(endLabel)
    .End();
  ```
- Appending as raw data an address or the difference between two labels
  ```csharp
  asm
    .Begin()
    .Label("start", out var startLabel)
    .LabelForward("end", out var endLabel)
    .RTS()
    .Label(endLabel)
    .Append(startLabel) // The address of the label
    .Append((endLabel - startLabel).LowByte()) // The size of the code
    .End();
  ```

### Customizing Disassembly Output

The disassembler supports extensive customization:
- **Custom label formatting**: Provide a delegate to format labels.
- **Instruction comments**: Add comments per instruction.
- **Pre/Post instruction hooks**: Inject text before/after each instruction.

Example:

```csharp
var options = new Mos6502DisassemblerOptions {
    TryFormatLabel = (offset, span, out int charsWritten) => {
        // Custom label formatting logic
        charsWritten = $"LBL_{offset:X4}".AsSpan().CopyTo(span) ? 9 : 0;
        return charsWritten > 0;
    },
    TryFormatComment = (offset, inst, span, out int charsWritten) => {
        // Add a comment for each instruction
        charsWritten = $"; Offset {offset:X4}".AsSpan().CopyTo(span) ? 13 : 0;
        return charsWritten > 0;
    }
};
```

### Assembler Debug Line Information

The assembler can generate debug line information that includes C# source file names and line numbers. It simply requires to add a derived class from `IMos6502AssemblerDebugMap` or use the default `Mos6502AssemblerDebugMap` implementation.

```csharp
var debugMap = new Mos6502AssemblerDebugMap();
var asm = new Mos6502Assembler() 
{
    DebugMap = debugMap
};

var forwardLabel = new Mos6502Label();

asm
    .Begin(0xC000)
    .LDA(0x5) // Zero page load
    .STA(0x1000)
    .Label(out var label)
    .LDA(_[0x1, X])
    .LDA(_[0x2], Y)
    .BEQ(label)
    .BCC(forwardLabel)
    .RTS()
    .Label(forwardLabel)
    .End();

var toString = asm.DebugMap.ToString();
Console.WriteLine(toString);
```

will print something like:

```
Debug Info (Program: TestSimple)
- Program Start Address: 0xC000
- Program End Address: C00E
- Debug Line Count: 7

C000 {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:51
C002 {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:52
C005 {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:54
C007 {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:55
C009 {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:56
C00B {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:57
C00D {ProjectDirectory}Mos6502AssemblerSpecialTests.cs:58
```

### 6510 Support

The library also supports the 6510 CPU, which is a variant of the 6502 used in the Commodore 64. For simplicity, it contains all the 6502 instructions including the illegal opcodes. You can use the `Mos6510Assembler` and `Mos6510Disassembler` classes in the same way as the 6502 counterparts.

## Tips and Best Practices

- Always call `End()` after assembling to resolve labels and finalize the buffer.
- Instead of raw addresses you can use labels for all branch targets; the assembler will resolve them.
- Customize disassembly output for integration with tools or documentation.
- Use `AppendBuffer` and `AppendBytes` for embedding data or padding.
- Dispose the assembler (`using var asm = ...`) to release internal buffers.
