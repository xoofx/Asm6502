// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Options for configuring the ARM64 disassembler.
/// </summary>
public class Mos6502DisassemblerOptions
{
    private int _formatLineBufferLength;
    private int _indentSize;
    private int _instructionTextPaddingLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502DisassemblerOptions"/> class.
    /// </summary>
    public Mos6502DisassemblerOptions()
    {
        BaseAddress = 0xC000;
        FormatLineBufferLength = 4096;
        IndentSize = 2;
        LocalLabelPrefix = "LL_";
        PrintNewLineBeforeLabel = true;
        PrintNewLineAfterBranch = true;
        PrintLabelBeforeFirstInstruction = true;
        InstructionTextPaddingLength = 16;
    }

    /// <summary>
    /// Gets or sets the delegate to format labels.
    /// </summary>
    public Mos6502TryFormatDelegate? TryFormatLabel { get; set; }

    /// <summary>
    /// Gets or sets the delegate to format instruction comments.
    /// </summary>
    public Mos6502TryFormatInstructionCommentDelegate? TryFormatComment { get; set; }

    /// <summary>
    /// Gets or sets the length of the instruction text padding.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int InstructionTextPaddingLength
    {
        get => _instructionTextPaddingLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _instructionTextPaddingLength = value;
        }
    }

    /// <summary>
    /// Gets or sets the delegate to print a text before the instruction being printed.
    /// </summary>
    public Mos6502InstructionPrinterDelegate? PreInstructionPrinter { get; set; }

    /// <summary>
    /// Gets or sets the delegate to print a text after the instruction being printed.
    /// </summary>
    public Mos6502InstructionPrinterDelegate? PostInstructionPrinter { get; set; }

    /// <summary>
    /// Gets or sets the length of the format line buffer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 256.</exception>
    public int FormatLineBufferLength
    {
        get => _formatLineBufferLength;
        set
        {
            if (value < 256) throw new ArgumentOutOfRangeException(nameof(value), "The format buffer length must be at least 256");
            _formatLineBufferLength = value;
        }
    }

    /// <summary>
    /// Gets or sets the prefix for local labels.
    /// </summary>
    public string LocalLabelPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to print the address.
    /// </summary>
    public bool PrintAddress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to print the assembly bytes.
    /// </summary>
    public bool PrintAssemblyBytes { get; set; }

    /// <summary>
    /// Gets or sets the base address.
    /// </summary>
    public ushort BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the size of the indent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int IndentSize
    {
        get => _indentSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _indentSize = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to print a label before the first instruction.
    /// </summary>
    public bool PrintLabelBeforeFirstInstruction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to print a new line after a branch.
    /// </summary>
    public bool PrintNewLineAfterBranch { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to print a new line before a label.
    /// </summary>
    public bool PrintNewLineBeforeLabel { get; set; }

    /// <summary>
    /// Gets or sets the format provider.
    /// </summary>
    public IFormatProvider? FormatProvider { get; set; }
}

/// <summary>
/// Options for configuring the ARM64 disassembler.
/// </summary>
public class Mos6510DisassemblerOptions : Mos6502DisassemblerOptions
{
    /// <summary>
    /// Gets or sets the delegate to format instruction comments.
    /// </summary>
    public new Mos6510TryFormatInstructionCommentDelegate? TryFormatComment { get; set; }

    /// <summary>
    /// Gets or sets the delegate to print a text before the instruction being printed.
    /// </summary>
    public new Mos6510InstructionPrinterDelegate? PreInstructionPrinter { get; set; }

    /// <summary>
    /// Gets or sets the delegate to print a text after the instruction being printed.
    /// </summary>
    public new Mos6510InstructionPrinterDelegate? PostInstructionPrinter { get; set; }
}