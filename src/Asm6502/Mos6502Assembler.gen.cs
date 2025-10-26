// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502.Expressions;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Asm6502;

/// <summary>
/// Represents a 6502 assembler for generating machine code and managing labels.
/// </summary>
public partial class Mos6502Assembler : Mos6502Assembler<Mos6502Assembler>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    public  Mos6502Assembler(ushort baseAddress = 0xC000) : base(baseAddress)
    {
    }
}

/// <summary>
/// Represents a 6502 assembler base class for generating machine code and managing labels.
/// </summary>
public abstract partial class Mos6502Assembler<TAsm> : Mos6502AssemblerBase<TAsm> where TAsm : Mos6502Assembler<TAsm>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    protected Mos6502Assembler(ushort baseAddress = 0xC000) : base(baseAddress)
    {
    }

    /// <summary>
    /// Adds an instruction to the assembler.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public TAsm AddInstruction(Mos6502Instruction instruction, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        if (!instruction.IsValid) throw new ArgumentException("Invalid instruction", nameof(instruction));

        var sizeInBytes = instruction.SizeInBytes;
        Debug.Assert(sizeInBytes > 0);

        var totalSizeInBytes = SafeAddress(SizeInBytes + (byte)sizeInBytes);
        var currentAddress = CurrentAddress;

        var span = GetBuffer(sizeInBytes);
        instruction.AsSpan.CopyTo(span);
        SizeInBytes = totalSizeInBytes;
        CurrentCycleCount += instruction.CycleCount;
        CurrentOffset += (ushort)sizeInBytes;

        // Log debug information for the instruction
        DebugMap?.LogDebugInfo(new(currentAddress, Mos6502AssemblerDebugInfoKind.LineInfo, debugFilePath, debugLineNumber));

        return (TAsm)this;
    }

    /// <summary>
    /// Adds an instruction to the assembler, possibly referencing a label.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public TAsm AddInstruction(Mos6502Instruction instruction, Mos6502Expression expression, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        var currentAddress = CurrentAddress;
        var bufferOffset = SizeInBytes;
        // ReSharper disable ExplicitCallerInfoArgument
        AddInstruction(instruction, debugFilePath, debugLineNumber);
        // ReSharper restore ExplicitCallerInfoArgument

        Patches.Add(new(currentAddress, (ushort)(bufferOffset + 1), instruction.AddressingMode, expression));

        return (TAsm)this;
    }
}

/// <summary>
/// Represents a 6510 assembler for generating machine code and managing labels.
/// </summary>
public partial class Mos6510Assembler : Mos6510Assembler<Mos6510Assembler>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6510Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    public  Mos6510Assembler(ushort baseAddress = 0xC000) : base(baseAddress)
    {
    }
}

/// <summary>
/// Represents a 6510 assembler base class for generating machine code and managing labels.
/// </summary>
public abstract partial class Mos6510Assembler<TAsm> : Mos6502AssemblerBase<TAsm> where TAsm : Mos6510Assembler<TAsm>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6510Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    protected Mos6510Assembler(ushort baseAddress = 0xC000) : base(baseAddress)
    {
    }

    /// <summary>
    /// Adds an instruction to the assembler.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public TAsm AddInstruction(Mos6510Instruction instruction, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        if (!instruction.IsValid) throw new ArgumentException("Invalid instruction", nameof(instruction));

        var sizeInBytes = instruction.SizeInBytes;
        Debug.Assert(sizeInBytes > 0);

        var totalSizeInBytes = SafeAddress(SizeInBytes + (byte)sizeInBytes);
        var currentAddress = CurrentAddress;

        var span = GetBuffer(sizeInBytes);
        instruction.AsSpan.CopyTo(span);
        SizeInBytes = totalSizeInBytes;
        CurrentCycleCount += instruction.CycleCount;
        CurrentOffset += (ushort)sizeInBytes;

        // Log debug information for the instruction
        DebugMap?.LogDebugInfo(new(currentAddress, Mos6502AssemblerDebugInfoKind.LineInfo, debugFilePath, debugLineNumber));

        return (TAsm)this;
    }

    /// <summary>
    /// Adds an instruction to the assembler, possibly referencing a label.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public TAsm AddInstruction(Mos6510Instruction instruction, Mos6502Expression expression, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        var currentAddress = CurrentAddress;
        var bufferOffset = SizeInBytes;
        // ReSharper disable ExplicitCallerInfoArgument
        AddInstruction(instruction, debugFilePath, debugLineNumber);
        // ReSharper restore ExplicitCallerInfoArgument

        Patches.Add(new(currentAddress, (ushort)(bufferOffset + 1), instruction.AddressingMode, expression));

        return (TAsm)this;
    }
}
