# Asm6502 User Guide

This document provides a small user guide for the Asm6502 library.

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
  - [Addressing Modes](#addressing-modes)
- [Advanced Usage](#advanced-usage)
  - [Labels and Branches](#labels-and-branches)
  - [Expressions](#expressions)
  - [Customizing Disassembly Output](#customizing-disassembly-output)
  - [Assembler Debug Line Information](#assembler-debug-line-information)
  - [Org directive](#org-directive)
  - [6510 Support](#6510-support)
- [Tips and Best Practices](#tips-and-best-practices)
- [Supported instructions](#supported-instructions)
  - [6502 Instructions](#6502-instructions)
  - [6510 Additional Illegal Instructions](#6510-additional-illegal-instructions)

---

## Quick Start

### Assembling 6502 Code

```csharp
using Asm6502;
using static Asm6502.Mos6502Factory;

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
using Asm6502;
using static Asm6502.Mos6502Factory;

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
using Asm6502;
using static Asm6502.Mos6502Factory;

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
    .Label(out var startLabel)
    .LDX_Imm(0x00)         // X = 0
    .LDY_Imm(0x10)         // Y = 16
    .Label(out var loopLabel)
    .LDA(0x0200, X)        // LDA $0200,X
    .CMP(0xFF)             // CMP #$FF
    .ForwardLabel(out var skipLabel)
    .BEQ(skipLabel)        // BEQ SKIP
    .CLC()                 // CLC
    .ADC_Imm(0x01)         // ADC #$01
    .STA(0x0200, X)        // STA $0200,X
    .Label(skipLabel)
    .INX()                 // INX
    .DEY()                 // DEY
    .BNE(loopLabel)        // BNE LOOP
    .Label(out var endLabel)
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
| [Immediate](https://www.masswerk.at/6502/6502_instruction_set.html#modes_immediate)       | `LDA #$10`        | `asm.LDA_Imm(0x10)`     | Load immediate value into A          |
| [Zero Page](https://www.masswerk.at/6502/6502_instruction_set.html#modes_zeropage)       | `LDA $10`         | `asm.LDA(0x10)`         | Load from zero page address          |
| [Zero Page,X](https://www.masswerk.at/6502/6502_instruction_set.html#modes_zeropage_indexed)     | `LDA $10,X`       | `asm.LDA(0x10, X)`      | Load from zero page address + X      |
| [Zero Page,Y](https://www.masswerk.at/6502/6502_instruction_set.html#modes_zeropage_indexed)     | `LDX $10,Y`       | `asm.LDX(0x10, Y)`      | Load from zero page address + Y      |
| [Absolute](https://www.masswerk.at/6502/6502_instruction_set.html#modes_absolute)        | `LDA $1234`       | `asm.LDA(0x1234)`       | Load from absolute address           |
| [Absolute,X](https://www.masswerk.at/6502/6502_instruction_set.html#modes_indexed)      | `LDA $1234,X`     | `asm.LDA(0x1234, X)`    | Load from absolute address + X       |
| [Absolute,Y](https://www.masswerk.at/6502/6502_instruction_set.html#modes_indexed)      | `LDA $1234,Y`     | `asm.LDA(0x1234, Y)`    | Load from absolute address + Y       |
| [Indirect](https://www.masswerk.at/6502/6502_instruction_set.html#modes_indirect)        | `JMP ($1234)`     | `asm.JMP(_[0x1234])`    | Jump to address stored at $1234      |
| [(Indirect,X)](https://www.masswerk.at/6502/6502_instruction_set.html#modes_preindexed_indirect)    | `LDA ($10,X)`     | `asm.LDA(_[0x10, X])`   | Load from address at (zero page + X) |
| [(Indirect),Y](https://www.masswerk.at/6502/6502_instruction_set.html#modes_postindexed_indirect)    | `LDA ($10),Y`     | `asm.LDA(_[0x10], Y)`   | Load from address at (zero page) + Y |

Notice that immediate values are suffixed with `_Imm`, and indirect addressing uses the `_[]` syntax. The registers `X` and `Y` are used directly in the method calls.

In order to access the X and Y registers as well as the indirect addressing modes, the following syntax is used in C#:

```csharp
using static Asm6502.Mos6502Factory;
```

---

## Advanced Usage

### Labels and Branches

Labels can be created and bound to addresses. Branch instructions (e.g., BEQ, BNE) can reference labels, even forward-declared ones. The assembler will resolve all label addresses when `End()` is called.

```csharp
asm
    .Label(out var loop)
    .BNE(loop);    
```

Note that the name of the label is inferred from the variable name if not explicitly provided. In the example above, the label will be named "loop". You can also provide a custom name:

```csharp
asm
    .Label("CUSTOM_LABEL", out var customLabel)
    .BNE(customLabel);
```

> ⚠️ The label name is only used when reporting errors when resolving label addresses.

You can also create forward labels that are resolved later:

```csharp
asm
    .ForwardLabel(out var skipLabel)
    // ... some instructions ...
    .BEQ(skipLabel)    // Branch to SKIP if condition met
    .LDA_Imm(0xFF)     // Load accumulator with 0xFF
    .Label(skipLabel)  // Bind SKIP label later
```

Or directly within the branch instruction:


```csharp
asm
    .BEQ(out var skipLabel)    // Branch to SKIP if condition met
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
    .Label(out var startLabel)
    .LabelForward(out var endLabel)
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
    .LabelForward(out var endLabel)
    .LDA(endLabel + 1) // Load A with the address of endLabel + 1
    .RTS()
    .Label(endLabel)
    .End();
  ```
- Appending as raw data an address or the difference between two labels
  ```csharp
  asm
    .Begin()
    .Label(out var startLabel)
    .LabelForward(out var endLabel)
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

### Org directive

The org directive allows to set the current assembly address to a specific value.

```csharp
using var asm = new Mos6502Assembler();
asm.Begin(0xC000); // Resets the assembler state and sets the org to 0xC000
asm.LDA_Imm(0);
asm.Label(out var label1);
asm.LabelForward(out var label2);
asm.JMP(label2);

// Fill with NOPs to reach address 0xC010
asm.AppendBytes(0x10 - asm.SizeInBytes, (byte)Mos6502OpCode.NOP_Implied);
        
asm.Org(0xC010); // Usage of org directive to set the base address to 0xC010
asm.LDA_Imm(1);
asm.Label(label2);
asm.JMP(label1);
asm.End();
```

This will produce the following code:

```
C000  A9 00      LDA #$00

LL_02:
C002  4C 12 C0   JMP LL_01

C005  EA         NOP
C006  EA         NOP
C007  EA         NOP
C008  EA         NOP
C009  EA         NOP
C00A  EA         NOP
C00B  EA         NOP
C00C  EA         NOP
C00D  EA         NOP
C00E  EA         NOP
C00F  EA         NOP
C010  A9 01      LDA #$01

LL_01:
C012  4C 02 C0   JMP LL_02
```

### 6510 Support

The library also supports the 6510 CPU, which is a variant of the 6502 used in the Commodore 64. For simplicity, it contains all the 6502 instructions including the illegal opcodes. You can use the `Mos6510Assembler` and `Mos6510Disassembler` classes in the same way as the 6502 counterparts.

## Tips and Best Practices

- Always call `End()` after assembling to resolve labels and finalize the buffer.
- Instead of raw addresses you can use labels for all branch targets; the assembler will resolve them.
- Customize disassembly output for integration with tools or documentation.
- Use `AppendBuffer` and `AppendBytes` for embedding data or padding.
- Dispose the assembler (`using var asm = ...`) to release internal buffers.

## Supported instructions

### 6502 Instructions

The following instructions are supported by the `Mos6502Assembler` and `Mos6510Assembler` classes:

| Byte | Instruction | C# Syntax    | Description |
|------|-------------|--------------|-------------|
| `0x6d` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` address` | `asm.ADC(address);` | Add with carry |
| `0x7d` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` address, X` | `asm.ADC(address, X);` | Add with carry |
| `0x79` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` address, Y` | `asm.ADC(address, Y);` | Add with carry |
| `0x69` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` #value` | `asm.ADC_Imm(value);` | Add with carry |
| `0x61` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` (zp, X)` | `asm.ADC(_[zp, X]);` | Add with carry |
| `0x71` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` (zp), Y` | `asm.ADC(_[zp], Y);` | Add with carry |
| `0x65` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` zp` | `asm.ADC(zp);` | Add with carry |
| `0x75` | [`ADC`](https://www.masswerk.at/6502/6502_instruction_set.html#ADC) ` zp, X` | `asm.ADC(zp, X);` | Add with carry |
| `0x2d` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` address` | `asm.AND(address);` | Logical AND |
| `0x3d` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` address, X` | `asm.AND(address, X);` | Logical AND |
| `0x39` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` address, Y` | `asm.AND(address, Y);` | Logical AND |
| `0x29` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` #value` | `asm.AND_Imm(value);` | Logical AND |
| `0x21` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` (zp, X)` | `asm.AND(_[zp, X]);` | Logical AND |
| `0x31` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` (zp), Y` | `asm.AND(_[zp], Y);` | Logical AND |
| `0x25` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` zp` | `asm.AND(zp);` | Logical AND |
| `0x35` | [`AND`](https://www.masswerk.at/6502/6502_instruction_set.html#AND) ` zp, X` | `asm.AND(zp, X);` | Logical AND |
| `0x0e` | [`ASL`](https://www.masswerk.at/6502/6502_instruction_set.html#ASL) ` address` | `asm.ASL(address);` | Arithmetic shift left |
| `0x1e` | [`ASL`](https://www.masswerk.at/6502/6502_instruction_set.html#ASL) ` address, X` | `asm.ASL(address, X);` | Arithmetic shift left |
| `0x0a` | [`ASL`](https://www.masswerk.at/6502/6502_instruction_set.html#ASL) ` A` | `asm.ASL(A);` | Arithmetic shift left |
| `0x06` | [`ASL`](https://www.masswerk.at/6502/6502_instruction_set.html#ASL) ` zp` | `asm.ASL(zp);` | Arithmetic shift left |
| `0x16` | [`ASL`](https://www.masswerk.at/6502/6502_instruction_set.html#ASL) ` zp, X` | `asm.ASL(zp, X);` | Arithmetic shift left |
| `0x90` | [`BCC`](https://www.masswerk.at/6502/6502_instruction_set.html#BCC) ` label` | `asm.BCC(label);` | Branch if carry clear |
| `0xb0` | [`BCS`](https://www.masswerk.at/6502/6502_instruction_set.html#BCS) ` label` | `asm.BCS(label);` | Branch if carry set |
| `0xf0` | [`BEQ`](https://www.masswerk.at/6502/6502_instruction_set.html#BEQ) ` label` | `asm.BEQ(label);` | Branch if equal |
| `0x2c` | [`BIT`](https://www.masswerk.at/6502/6502_instruction_set.html#BIT) ` address` | `asm.BIT(address);` | Bit test |
| `0x24` | [`BIT`](https://www.masswerk.at/6502/6502_instruction_set.html#BIT) ` zp` | `asm.BIT(zp);` | Bit test |
| `0x30` | [`BMI`](https://www.masswerk.at/6502/6502_instruction_set.html#BMI) ` label` | `asm.BMI(label);` | Branch if minus |
| `0xd0` | [`BNE`](https://www.masswerk.at/6502/6502_instruction_set.html#BNE) ` label` | `asm.BNE(label);` | Branch if not equal |
| `0x10` | [`BPL`](https://www.masswerk.at/6502/6502_instruction_set.html#BPL) ` label` | `asm.BPL(label);` | Branch if positive |
| `0x00` | [`BRK`](https://www.masswerk.at/6502/6502_instruction_set.html#BRK) | `asm.BRK();` | Break / Software Interrupt |
| `0x50` | [`BVC`](https://www.masswerk.at/6502/6502_instruction_set.html#BVC) ` label` | `asm.BVC(label);` | Branch if overflow clear |
| `0x70` | [`BVS`](https://www.masswerk.at/6502/6502_instruction_set.html#BVS) ` label` | `asm.BVS(label);` | Branch if overflow set |
| `0x18` | [`CLC`](https://www.masswerk.at/6502/6502_instruction_set.html#CLC) | `asm.CLC();` | Clear carry |
| `0xd8` | [`CLD`](https://www.masswerk.at/6502/6502_instruction_set.html#CLD) | `asm.CLD();` | Clear decimal mode |
| `0x58` | [`CLI`](https://www.masswerk.at/6502/6502_instruction_set.html#CLI) | `asm.CLI();` | Clear interrupt disable |
| `0xb8` | [`CLV`](https://www.masswerk.at/6502/6502_instruction_set.html#CLV) | `asm.CLV();` | Clear overflow flag |
| `0xcd` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` address` | `asm.CMP(address);` | Compare |
| `0xdd` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` address, X` | `asm.CMP(address, X);` | Compare |
| `0xd9` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` address, Y` | `asm.CMP(address, Y);` | Compare |
| `0xc9` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` #value` | `asm.CMP_Imm(value);` | Compare |
| `0xc1` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` (zp, X)` | `asm.CMP(_[zp, X]);` | Compare |
| `0xd1` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` (zp), Y` | `asm.CMP(_[zp], Y);` | Compare |
| `0xc5` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` zp` | `asm.CMP(zp);` | Compare |
| `0xd5` | [`CMP`](https://www.masswerk.at/6502/6502_instruction_set.html#CMP) ` zp, X` | `asm.CMP(zp, X);` | Compare |
| `0xec` | [`CPX`](https://www.masswerk.at/6502/6502_instruction_set.html#CPX) ` address` | `asm.CPX(address);` | Compare X register |
| `0xe0` | [`CPX`](https://www.masswerk.at/6502/6502_instruction_set.html#CPX) ` #value` | `asm.CPX_Imm(value);` | Compare X register |
| `0xe4` | [`CPX`](https://www.masswerk.at/6502/6502_instruction_set.html#CPX) ` zp` | `asm.CPX(zp);` | Compare X register |
| `0xcc` | [`CPY`](https://www.masswerk.at/6502/6502_instruction_set.html#CPY) ` address` | `asm.CPY(address);` | Compare Y register |
| `0xc0` | [`CPY`](https://www.masswerk.at/6502/6502_instruction_set.html#CPY) ` #value` | `asm.CPY_Imm(value);` | Compare Y register |
| `0xc4` | [`CPY`](https://www.masswerk.at/6502/6502_instruction_set.html#CPY) ` zp` | `asm.CPY(zp);` | Compare Y register |
| `0xce` | [`DEC`](https://www.masswerk.at/6502/6502_instruction_set.html#DEC) ` address` | `asm.DEC(address);` | Decrement memory |
| `0xde` | [`DEC`](https://www.masswerk.at/6502/6502_instruction_set.html#DEC) ` address, X` | `asm.DEC(address, X);` | Decrement memory |
| `0xc6` | [`DEC`](https://www.masswerk.at/6502/6502_instruction_set.html#DEC) ` zp` | `asm.DEC(zp);` | Decrement memory |
| `0xd6` | [`DEC`](https://www.masswerk.at/6502/6502_instruction_set.html#DEC) ` zp, X` | `asm.DEC(zp, X);` | Decrement memory |
| `0xca` | [`DEX`](https://www.masswerk.at/6502/6502_instruction_set.html#DEX) | `asm.DEX();` | Decrement X register |
| `0x88` | [`DEY`](https://www.masswerk.at/6502/6502_instruction_set.html#DEY) | `asm.DEY();` | Decrement Y register |
| `0x4d` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` address` | `asm.EOR(address);` | Logical Exclusive OR (XOR) |
| `0x5d` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` address, X` | `asm.EOR(address, X);` | Logical Exclusive OR (XOR) |
| `0x59` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` address, Y` | `asm.EOR(address, Y);` | Logical Exclusive OR (XOR) |
| `0x49` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` #value` | `asm.EOR_Imm(value);` | Logical Exclusive OR (XOR) |
| `0x41` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` (zp, X)` | `asm.EOR(_[zp, X]);` | Logical Exclusive OR (XOR) |
| `0x51` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` (zp), Y` | `asm.EOR(_[zp], Y);` | Logical Exclusive OR (XOR) |
| `0x45` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` zp` | `asm.EOR(zp);` | Logical Exclusive OR (XOR) |
| `0x55` | [`EOR`](https://www.masswerk.at/6502/6502_instruction_set.html#EOR) ` zp, X` | `asm.EOR(zp, X);` | Logical Exclusive OR (XOR) |
| `0xee` | [`INC`](https://www.masswerk.at/6502/6502_instruction_set.html#INC) ` address` | `asm.INC(address);` | Increment memory |
| `0xfe` | [`INC`](https://www.masswerk.at/6502/6502_instruction_set.html#INC) ` address, X` | `asm.INC(address, X);` | Increment memory |
| `0xe6` | [`INC`](https://www.masswerk.at/6502/6502_instruction_set.html#INC) ` zp` | `asm.INC(zp);` | Increment memory |
| `0xf6` | [`INC`](https://www.masswerk.at/6502/6502_instruction_set.html#INC) ` zp, X` | `asm.INC(zp, X);` | Increment memory |
| `0xe8` | [`INX`](https://www.masswerk.at/6502/6502_instruction_set.html#INX) | `asm.INX();` | Increment X register |
| `0xc8` | [`INY`](https://www.masswerk.at/6502/6502_instruction_set.html#INY) | `asm.INY();` | Increment Y register |
| `0x4c` | [`JMP`](https://www.masswerk.at/6502/6502_instruction_set.html#JMP) ` address` | `asm.JMP(address);` | Unconditional Jump |
| `0x6c` | [`JMP`](https://www.masswerk.at/6502/6502_instruction_set.html#JMP) ` (address)` | `asm.JMP(_[address]);` | Unconditional Jump |
| `0x20` | [`JSR`](https://www.masswerk.at/6502/6502_instruction_set.html#JSR) ` address` | `asm.JSR(address);` | Jump to subroutine |
| `0xad` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` address` | `asm.LDA(address);` | Load accumulator |
| `0xbd` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` address, X` | `asm.LDA(address, X);` | Load accumulator |
| `0xb9` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` address, Y` | `asm.LDA(address, Y);` | Load accumulator |
| `0xa9` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` #value` | `asm.LDA_Imm(value);` | Load accumulator |
| `0xa1` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` (zp, X)` | `asm.LDA(_[zp, X]);` | Load accumulator |
| `0xb1` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` (zp), Y` | `asm.LDA(_[zp], Y);` | Load accumulator |
| `0xa5` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` zp` | `asm.LDA(zp);` | Load accumulator |
| `0xb5` | [`LDA`](https://www.masswerk.at/6502/6502_instruction_set.html#LDA) ` zp, X` | `asm.LDA(zp, X);` | Load accumulator |
| `0xae` | [`LDX`](https://www.masswerk.at/6502/6502_instruction_set.html#LDX) ` address` | `asm.LDX(address);` | Load X register |
| `0xbe` | [`LDX`](https://www.masswerk.at/6502/6502_instruction_set.html#LDX) ` address, Y` | `asm.LDX(address, Y);` | Load X register |
| `0xa2` | [`LDX`](https://www.masswerk.at/6502/6502_instruction_set.html#LDX) ` #value` | `asm.LDX_Imm(value);` | Load X register |
| `0xa6` | [`LDX`](https://www.masswerk.at/6502/6502_instruction_set.html#LDX) ` zp` | `asm.LDX(zp);` | Load X register |
| `0xb6` | [`LDX`](https://www.masswerk.at/6502/6502_instruction_set.html#LDX) ` zp, Y` | `asm.LDX(zp, Y);` | Load X register |
| `0xac` | [`LDY`](https://www.masswerk.at/6502/6502_instruction_set.html#LDY) ` address` | `asm.LDY(address);` | Load Y register |
| `0xbc` | [`LDY`](https://www.masswerk.at/6502/6502_instruction_set.html#LDY) ` address, X` | `asm.LDY(address, X);` | Load Y register |
| `0xa0` | [`LDY`](https://www.masswerk.at/6502/6502_instruction_set.html#LDY) ` #value` | `asm.LDY_Imm(value);` | Load Y register |
| `0xa4` | [`LDY`](https://www.masswerk.at/6502/6502_instruction_set.html#LDY) ` zp` | `asm.LDY(zp);` | Load Y register |
| `0xb4` | [`LDY`](https://www.masswerk.at/6502/6502_instruction_set.html#LDY) ` zp, X` | `asm.LDY(zp, X);` | Load Y register |
| `0x4e` | [`LSR`](https://www.masswerk.at/6502/6502_instruction_set.html#LSR) ` address` | `asm.LSR(address);` | Logical shift right |
| `0x5e` | [`LSR`](https://www.masswerk.at/6502/6502_instruction_set.html#LSR) ` address, X` | `asm.LSR(address, X);` | Logical shift right |
| `0x4a` | [`LSR`](https://www.masswerk.at/6502/6502_instruction_set.html#LSR) ` A` | `asm.LSR(A);` | Logical shift right |
| `0x46` | [`LSR`](https://www.masswerk.at/6502/6502_instruction_set.html#LSR) ` zp` | `asm.LSR(zp);` | Logical shift right |
| `0x56` | [`LSR`](https://www.masswerk.at/6502/6502_instruction_set.html#LSR) ` zp, X` | `asm.LSR(zp, X);` | Logical shift right |
| `0xea` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP();` | No operation |
| `0x0d` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` address` | `asm.ORA(address);` | Logical Inclusive OR |
| `0x1d` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` address, X` | `asm.ORA(address, X);` | Logical Inclusive OR |
| `0x19` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` address, Y` | `asm.ORA(address, Y);` | Logical Inclusive OR |
| `0x09` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` #value` | `asm.ORA_Imm(value);` | Logical Inclusive OR |
| `0x01` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` (zp, X)` | `asm.ORA(_[zp, X]);` | Logical Inclusive OR |
| `0x11` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` (zp), Y` | `asm.ORA(_[zp], Y);` | Logical Inclusive OR |
| `0x05` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` zp` | `asm.ORA(zp);` | Logical Inclusive OR |
| `0x15` | [`ORA`](https://www.masswerk.at/6502/6502_instruction_set.html#ORA) ` zp, X` | `asm.ORA(zp, X);` | Logical Inclusive OR |
| `0x48` | [`PHA`](https://www.masswerk.at/6502/6502_instruction_set.html#PHA) | `asm.PHA();` | Push accumulator |
| `0x08` | [`PHP`](https://www.masswerk.at/6502/6502_instruction_set.html#PHP) | `asm.PHP();` | Push processor status |
| `0x68` | [`PLA`](https://www.masswerk.at/6502/6502_instruction_set.html#PLA) | `asm.PLA();` | Pull accumulator |
| `0x28` | [`PLP`](https://www.masswerk.at/6502/6502_instruction_set.html#PLP) | `asm.PLP();` | Pull processor status |
| `0x2e` | [`ROL`](https://www.masswerk.at/6502/6502_instruction_set.html#ROL) ` address` | `asm.ROL(address);` | Rotate left |
| `0x3e` | [`ROL`](https://www.masswerk.at/6502/6502_instruction_set.html#ROL) ` address, X` | `asm.ROL(address, X);` | Rotate left |
| `0x2a` | [`ROL`](https://www.masswerk.at/6502/6502_instruction_set.html#ROL) ` A` | `asm.ROL(A);` | Rotate left |
| `0x26` | [`ROL`](https://www.masswerk.at/6502/6502_instruction_set.html#ROL) ` zp` | `asm.ROL(zp);` | Rotate left |
| `0x36` | [`ROL`](https://www.masswerk.at/6502/6502_instruction_set.html#ROL) ` zp, X` | `asm.ROL(zp, X);` | Rotate left |
| `0x6e` | [`ROR`](https://www.masswerk.at/6502/6502_instruction_set.html#ROR) ` address` | `asm.ROR(address);` | Rotate right |
| `0x7e` | [`ROR`](https://www.masswerk.at/6502/6502_instruction_set.html#ROR) ` address, X` | `asm.ROR(address, X);` | Rotate right |
| `0x6a` | [`ROR`](https://www.masswerk.at/6502/6502_instruction_set.html#ROR) ` A` | `asm.ROR(A);` | Rotate right |
| `0x66` | [`ROR`](https://www.masswerk.at/6502/6502_instruction_set.html#ROR) ` zp` | `asm.ROR(zp);` | Rotate right |
| `0x76` | [`ROR`](https://www.masswerk.at/6502/6502_instruction_set.html#ROR) ` zp, X` | `asm.ROR(zp, X);` | Rotate right |
| `0x40` | [`RTI`](https://www.masswerk.at/6502/6502_instruction_set.html#RTI) | `asm.RTI();` | Return from interrupt |
| `0x60` | [`RTS`](https://www.masswerk.at/6502/6502_instruction_set.html#RTS) | `asm.RTS();` | Return from subroutine |
| `0xed` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` address` | `asm.SBC(address);` | Subtract with carry |
| `0xfd` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` address, X` | `asm.SBC(address, X);` | Subtract with carry |
| `0xf9` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` address, Y` | `asm.SBC(address, Y);` | Subtract with carry |
| `0xe9` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` #value` | `asm.SBC_Imm(value);` | Subtract with carry |
| `0xe1` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` (zp, X)` | `asm.SBC(_[zp, X]);` | Subtract with carry |
| `0xf1` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` (zp), Y` | `asm.SBC(_[zp], Y);` | Subtract with carry |
| `0xe5` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` zp` | `asm.SBC(zp);` | Subtract with carry |
| `0xf5` | [`SBC`](https://www.masswerk.at/6502/6502_instruction_set.html#SBC) ` zp, X` | `asm.SBC(zp, X);` | Subtract with carry |
| `0x38` | [`SEC`](https://www.masswerk.at/6502/6502_instruction_set.html#SEC) | `asm.SEC();` | Set carry |
| `0xf8` | [`SED`](https://www.masswerk.at/6502/6502_instruction_set.html#SED) | `asm.SED();` | Set decimal flag |
| `0x78` | [`SEI`](https://www.masswerk.at/6502/6502_instruction_set.html#SEI) | `asm.SEI();` | Set interrupt disable |
| `0x8d` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` address` | `asm.STA(address);` | Store accumulator |
| `0x9d` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` address, X` | `asm.STA(address, X);` | Store accumulator |
| `0x99` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` address, Y` | `asm.STA(address, Y);` | Store accumulator |
| `0x81` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` (zp, X)` | `asm.STA(_[zp, X]);` | Store accumulator |
| `0x91` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` (zp), Y` | `asm.STA(_[zp], Y);` | Store accumulator |
| `0x85` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` zp` | `asm.STA(zp);` | Store accumulator |
| `0x95` | [`STA`](https://www.masswerk.at/6502/6502_instruction_set.html#STA) ` zp, X` | `asm.STA(zp, X);` | Store accumulator |
| `0x8e` | [`STX`](https://www.masswerk.at/6502/6502_instruction_set.html#STX) ` address` | `asm.STX(address);` | Store X register |
| `0x86` | [`STX`](https://www.masswerk.at/6502/6502_instruction_set.html#STX) ` zp` | `asm.STX(zp);` | Store X register |
| `0x96` | [`STX`](https://www.masswerk.at/6502/6502_instruction_set.html#STX) ` zp, Y` | `asm.STX(zp, Y);` | Store X register |
| `0x8c` | [`STY`](https://www.masswerk.at/6502/6502_instruction_set.html#STY) ` address` | `asm.STY(address);` | Store Y register |
| `0x84` | [`STY`](https://www.masswerk.at/6502/6502_instruction_set.html#STY) ` zp` | `asm.STY(zp);` | Store Y register |
| `0x94` | [`STY`](https://www.masswerk.at/6502/6502_instruction_set.html#STY) ` zp, X` | `asm.STY(zp, X);` | Store Y register |
| `0xaa` | [`TAX`](https://www.masswerk.at/6502/6502_instruction_set.html#TAX) | `asm.TAX();` | Transfer acc to X |
| `0xa8` | [`TAY`](https://www.masswerk.at/6502/6502_instruction_set.html#TAY) | `asm.TAY();` | Transfer acc to Y |
| `0xba` | [`TSX`](https://www.masswerk.at/6502/6502_instruction_set.html#TSX) | `asm.TSX();` | Transfer stack pointer to X |
| `0x8a` | [`TXA`](https://www.masswerk.at/6502/6502_instruction_set.html#TXA) | `asm.TXA();` | Transfer X to acc |
| `0x9a` | [`TXS`](https://www.masswerk.at/6502/6502_instruction_set.html#TXS) | `asm.TXS();` | Transfer X to SP |
| `0x98` | [`TYA`](https://www.masswerk.at/6502/6502_instruction_set.html#TYA) | `asm.TYA();` | Transfer Y to acc |

### 6510 Additional Illegal Instructions

The following instructions are supported by the `Mos6510Assembler` class:

| Byte | Instruction | C# Syntax    | Aliases | Description | Unstable |
|------|-------------|--------------|---------|-------------|----------|
| `0x4b` | [`ALR`](https://www.masswerk.at/6502/6502_instruction_set.html#ALR) ` #value` | `asm.ALR_Imm(value);` | `ALR`, `ASR` | AND then LSR |  |
| `0x0b` | [`ANC`](https://www.masswerk.at/6502/6502_instruction_set.html#ANC) ` #value` | `asm.ANC_Imm(value);` | `ANC` | AND then set carry |  |
| `0x2b` | [`ANC`](https://www.masswerk.at/6502/6502_instruction_set.html#ANC) ` #value` | `asm.ANC_2B_Imm(value);` | `ANC`, `ANC2` | AND then set carry |  |
| `0x8b` | [`ANE`](https://www.masswerk.at/6502/6502_instruction_set.html#ANE) ` #value` | `asm.ANE_Imm(value);` | `ANE`, `XAA` | Undocumented: AND with X then AND operand | ❌ |
| `0x6b` | [`ARR`](https://www.masswerk.at/6502/6502_instruction_set.html#ARR) ` #value` | `asm.ARR_Imm(value);` | `ARR` | AND then ROR |  |
| `0xcf` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` address` | `asm.DCP(address);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xdf` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` address, X` | `asm.DCP(address, X);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xdb` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` address, Y` | `asm.DCP(address, Y);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xc3` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` (zp, X)` | `asm.DCP(_[zp, X]);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xd3` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` (zp), Y` | `asm.DCP(_[zp], Y);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xc7` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` zp` | `asm.DCP(zp);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xd7` | [`DCP`](https://www.masswerk.at/6502/6502_instruction_set.html#DCP) ` zp, X` | `asm.DCP(zp, X);` | `DCP`, `DCM` | DEC then CMP |  |
| `0xef` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` address` | `asm.ISC(address);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xff` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` address, X` | `asm.ISC(address, X);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xfb` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` address, Y` | `asm.ISC(address, Y);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xe3` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` (zp, X)` | `asm.ISC(_[zp, X]);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xf3` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` (zp), Y` | `asm.ISC(_[zp], Y);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xe7` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` zp` | `asm.ISC(zp);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0xf7` | [`ISC`](https://www.masswerk.at/6502/6502_instruction_set.html#ISC) ` zp, X` | `asm.ISC(zp, X);` | `ISC`, `ISB`, `INS` | INC then SBC |  |
| `0x02` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x12` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_12();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x22` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_22();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x32` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_32();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x42` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_42();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x52` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_52();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x62` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_62();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x72` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_72();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0x92` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_92();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0xb2` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_B2();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0xd2` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_D2();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0xf2` | [`JAM`](https://www.masswerk.at/6502/6502_instruction_set.html#JAM) | `asm.JAM_F2();` | `JAM`, `KIL`, `HLT` | Jam the CPU (halt) |  |
| `0xbb` | [`LAS`](https://www.masswerk.at/6502/6502_instruction_set.html#LAS) ` address, Y` | `asm.LAS(address, Y);` | `LAS`, `LAR` | Load accumulator and transfer SP to X |  |
| `0xaf` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` address` | `asm.LAX(address);` | `LAX` | LDA then LDX |  |
| `0xbf` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` address, Y` | `asm.LAX(address, Y);` | `LAX` | LDA then LDX |  |
| `0xa3` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` (zp, X)` | `asm.LAX(_[zp, X]);` | `LAX` | LDA then LDX |  |
| `0xb3` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` (zp), Y` | `asm.LAX(_[zp], Y);` | `LAX` | LDA then LDX |  |
| `0xa7` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` zp` | `asm.LAX(zp);` | `LAX` | LDA then LDX |  |
| `0xb7` | [`LAX`](https://www.masswerk.at/6502/6502_instruction_set.html#LAX) ` zp, Y` | `asm.LAX(zp, Y);` | `LAX` | LDA then LDX |  |
| `0xab` | [`LXA`](https://www.masswerk.at/6502/6502_instruction_set.html#LXA) ` #value` | `asm.LXA_Imm(value);` | `LXA`, `LAX`, `immediate` | LDA then LDX | ❌ |
| `0x0c` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address` | `asm.NOP(address);` | `NOP` | No operation |  |
| `0x1c` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP(address, X);` | `NOP` | No operation |  |
| `0x3c` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP_3C(address, X);` | `NOP` | No operation |  |
| `0x5c` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP_5C(address, X);` | `NOP` | No operation |  |
| `0x7c` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP_7C(address, X);` | `NOP` | No operation |  |
| `0xdc` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP_DC(address, X);` | `NOP` | No operation |  |
| `0xfc` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` address, X` | `asm.NOP_FC(address, X);` | `NOP` | No operation |  |
| `0x80` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` #value` | `asm.NOP_Imm(value);` | `NOP` | No operation |  |
| `0x82` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` #value` | `asm.NOP_82_Imm(value);` | `NOP` | No operation |  |
| `0x89` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` #value` | `asm.NOP_89_Imm(value);` | `NOP` | No operation |  |
| `0xc2` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` #value` | `asm.NOP_C2_Imm(value);` | `NOP` | No operation |  |
| `0xe2` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` #value` | `asm.NOP_E2_Imm(value);` | `NOP` | No operation |  |
| `0x1a` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_1A();` | `NOP` | No operation |  |
| `0x3a` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_3A();` | `NOP` | No operation |  |
| `0x5a` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_5A();` | `NOP` | No operation |  |
| `0x7a` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_7A();` | `NOP` | No operation |  |
| `0xda` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_DA();` | `NOP` | No operation |  |
| `0xfa` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) | `asm.NOP_FA();` | `NOP` | No operation |  |
| `0x04` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp` | `asm.NOP(zp);` | `NOP` | No operation |  |
| `0x44` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp` | `asm.NOP_44(zp);` | `NOP` | No operation |  |
| `0x64` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp` | `asm.NOP_64(zp);` | `NOP` | No operation |  |
| `0x14` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP(zp, X);` | `NOP` | No operation |  |
| `0x34` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP_34(zp, X);` | `NOP` | No operation |  |
| `0x54` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP_54(zp, X);` | `NOP` | No operation |  |
| `0x74` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP_74(zp, X);` | `NOP` | No operation |  |
| `0xd4` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP_D4(zp, X);` | `NOP` | No operation |  |
| `0xf4` | [`NOP`](https://www.masswerk.at/6502/6502_instruction_set.html#NOP) ` zp, X` | `asm.NOP_F4(zp, X);` | `NOP` | No operation |  |
| `0x2f` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` address` | `asm.RLA(address);` | `RLA` | ROL then AND |  |
| `0x3f` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` address, X` | `asm.RLA(address, X);` | `RLA` | ROL then AND |  |
| `0x3b` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` address, Y` | `asm.RLA(address, Y);` | `RLA` | ROL then AND |  |
| `0x23` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` (zp, X)` | `asm.RLA(_[zp, X]);` | `RLA` | ROL then AND |  |
| `0x33` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` (zp), Y` | `asm.RLA(_[zp], Y);` | `RLA` | ROL then AND |  |
| `0x27` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` zp` | `asm.RLA(zp);` | `RLA` | ROL then AND |  |
| `0x37` | [`RLA`](https://www.masswerk.at/6502/6502_instruction_set.html#RLA) ` zp, X` | `asm.RLA(zp, X);` | `RLA` | ROL then AND |  |
| `0x6f` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` address` | `asm.RRA(address);` | `RRA` | ROR then ADC |  |
| `0x7f` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` address, X` | `asm.RRA(address, X);` | `RRA` | ROR then ADC |  |
| `0x7b` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` address, Y` | `asm.RRA(address, Y);` | `RRA` | ROR then ADC |  |
| `0x63` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` (zp, X)` | `asm.RRA(_[zp, X]);` | `RRA` | ROR then ADC |  |
| `0x73` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` (zp), Y` | `asm.RRA(_[zp], Y);` | `RRA` | ROR then ADC |  |
| `0x67` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` zp` | `asm.RRA(zp);` | `RRA` | ROR then ADC |  |
| `0x77` | [`RRA`](https://www.masswerk.at/6502/6502_instruction_set.html#RRA) ` zp, X` | `asm.RRA(zp, X);` | `RRA` | ROR then ADC |  |
| `0x8f` | [`SAX`](https://www.masswerk.at/6502/6502_instruction_set.html#SAX) ` address` | `asm.SAX(address);` | `SAX`, `AXS`, `AAX` | Store accumulator AND X |  |
| `0x83` | [`SAX`](https://www.masswerk.at/6502/6502_instruction_set.html#SAX) ` (zp, X)` | `asm.SAX(_[zp, X]);` | `SAX`, `AXS`, `AAX` | Store accumulator AND X |  |
| `0x87` | [`SAX`](https://www.masswerk.at/6502/6502_instruction_set.html#SAX) ` zp` | `asm.SAX(zp);` | `SAX`, `AXS`, `AAX` | Store accumulator AND X |  |
| `0x97` | [`SAX`](https://www.masswerk.at/6502/6502_instruction_set.html#SAX) ` zp, Y` | `asm.SAX(zp, Y);` | `SAX`, `AXS`, `AAX` | Store accumulator AND X |  |
| `0xcb` | [`SBX`](https://www.masswerk.at/6502/6502_instruction_set.html#SBX) ` #value` | `asm.SBX_Imm(value);` | `SBX`, `AXS`, `SAX` | Compute (A AND X) then subtract with carry |  |
| `0x9f` | [`SHA`](https://www.masswerk.at/6502/6502_instruction_set.html#SHA) ` address, Y` | `asm.SHA(address, Y);` | `SHA`, `AHX`, `AXA` | Store A AND X AND (high address + 1) |  |
| `0x93` | [`SHA`](https://www.masswerk.at/6502/6502_instruction_set.html#SHA) ` (zp), Y` | `asm.SHA(_[zp], Y);` | `SHA`, `AHX`, `AXA` | Store A AND X AND (high address + 1) | ❌ |
| `0x9e` | [`SHX`](https://www.masswerk.at/6502/6502_instruction_set.html#SHX) ` address, Y` | `asm.SHX(address, Y);` | `SHX`, `A11`, `SXA`, `XAS` | Store A AND X AND (high address + 1) | ❌ |
| `0x9c` | [`SHY`](https://www.masswerk.at/6502/6502_instruction_set.html#SHY) ` address, X` | `asm.SHY(address, X);` | `SHY`, `A11`, `SYA`, `SAY` | Store Y AND (high address + 1) | ❌ |
| `0x0f` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` address` | `asm.SLO(address);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x1f` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` address, X` | `asm.SLO(address, X);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x1b` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` address, Y` | `asm.SLO(address, Y);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x03` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` (zp, X)` | `asm.SLO(_[zp, X]);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x13` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` (zp), Y` | `asm.SLO(_[zp], Y);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x07` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` zp` | `asm.SLO(zp);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x17` | [`SLO`](https://www.masswerk.at/6502/6502_instruction_set.html#SLO) ` zp, X` | `asm.SLO(zp, X);` | `SLO`, `ASO` | ASL then ORA |  |
| `0x4f` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` address` | `asm.SRE(address);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x5f` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` address, X` | `asm.SRE(address, X);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x5b` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` address, Y` | `asm.SRE(address, Y);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x43` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` (zp, X)` | `asm.SRE(_[zp, X]);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x53` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` (zp), Y` | `asm.SRE(_[zp], Y);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x47` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` zp` | `asm.SRE(zp);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x57` | [`SRE`](https://www.masswerk.at/6502/6502_instruction_set.html#SRE) ` zp, X` | `asm.SRE(zp, X);` | `SRE`, `LSE` | LSR then EOR |  |
| `0x9b` | [`TAS`](https://www.masswerk.at/6502/6502_instruction_set.html#TAS) ` address, Y` | `asm.TAS(address, Y);` | `TAS`, `XAS`, `SHS` | Transfer A AND X to SP, store A AND X AND (high address + 1) | ❌ |
| `0xeb` | [`USBC`](https://www.masswerk.at/6502/6502_instruction_set.html#USBC) ` #value` | `asm.USBC_Imm(value);` | `USBC`, `SBC` | SBC with NOP behavior |  |
