// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AsmMos6502;

public class Mos6502Disassembler
{
    private readonly Dictionary<ushort, int> _internalLabels;
    private int _internalLabelId;
    private ushort _currentOffset;
    private readonly Mos6502TryFormatDelegate _tryFormatLabelDelegate;
    private ushort _currentPc;
    private bool _isOperandRelative;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Disassembler"/> class with default options.
    /// </summary>
    public Mos6502Disassembler() : this(new Mos6502DisassemblerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Disassembler"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options to use for disassembling.</param>
    public Mos6502Disassembler(Mos6502DisassemblerOptions options)
    {
        _internalLabels = new();
        Options = options;
        _tryFormatLabelDelegate = TryFormatLabel;
    }

    /// <summary>
    /// Gets the options used for disassembling.
    /// </summary>
    public Mos6502DisassemblerOptions Options { get; }

    /// <summary>
    /// Disassembles the specified byte buffer and returns the disassembled instructions as a string.
    /// </summary>
    /// <param name="buffer">The byte buffer containing the instructions to disassemble.</param>
    /// <returns>A string containing the disassembled instructions.</returns>
    public string Disassemble(Span<byte> buffer)
    {
        var writer = new StringWriter();
        Disassemble(buffer, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Disassembles the specified byte buffer and writes the disassembled instructions to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="buffer">The byte buffer containing the instructions to disassemble.</param>
    /// <param name="writer">The <see cref="TextWriter"/> to write the disassembled instructions to.</param>
    public void Disassemble(Span<byte> buffer, TextWriter writer)
    {
        // Clear the internal pending labels
        // And collect any new internal labels from the buffer
        // So that we can create labels for the disassembled instructions
        _internalLabels.Clear();
        _internalLabelId = 0;

        // Create a label for the first instruction if requested
        if (Options.PrintLabelBeforeFirstInstruction)
        {
            _internalLabels.Add(0, 1);
        }

        // Resolve all potential labels used by instructions
        int position = 0;
        while (position < buffer.Length)
        {
            var instruction = Mos6502Instruction.Decode(buffer.Slice(position));
            var instructionSizeInBytes = instruction.SizeInBytes;

            if (!instruction.IsValid)
            {
                instructionSizeInBytes = 1; // Skip invalid instructions
            }
            else if (CanHaveLabel(instruction.AddressingMode))
            {
                int addressOffset;
                if (instruction.AddressingMode == Mos6502AddressingMode.Relative)
                {
                    addressOffset = position + instruction.SizeInBytes + (sbyte)(byte)instruction.Operand;
                }
                else
                {
                    addressOffset = instruction.Operand - Options.BaseAddress;
                }

                if (addressOffset >= 0)
                {
                    var absoluteOffset = (ushort)addressOffset;
                    ++_internalLabelId;
                    _internalLabels.TryAdd(absoluteOffset, _internalLabelId);
                }
            }

            position += instructionSizeInBytes;
        }

        // Verify that labels are sorted by offset and check that a label is on a valid offset, otherwise remove it
        var sortedLabels = _internalLabels.OrderBy(kv => kv.Key).ToList();
        int sortedIndex = 0;

        position = 0;
        while (position < buffer.Length)
        {
            var instruction = Mos6502Instruction.Decode(buffer.Slice(position));
            var instructionSizeInBytes = instruction.SizeInBytes;

            if (!instruction.IsValid)
            {
                instructionSizeInBytes = 1; // Skip invalid instructions
            }
            
            if (sortedIndex < sortedLabels.Count)
            {
                var labelOffset = sortedLabels[sortedIndex].Key;

                if (position == labelOffset)
                {
                    // If the current position matches a label offset, we can keep it
                    sortedIndex++;
                }
                else if (position > labelOffset)
                {
                    // Remove the label if it is not valid
                    _internalLabels.Remove(labelOffset);
                    sortedIndex++;
                }
            }

            position += instructionSizeInBytes;
        }

        // Remove remaining labels not valid
        for(int i = sortedLabels.Count - 1; i >= sortedIndex; i--)
        {
            // Remove any remaining labels that are not valid
            _internalLabels.Remove(sortedLabels[i].Key);
        }
        
        // Process instructions
        var textBuffer = ArrayPool<char>.Shared.Rent(Options.FormatLineBufferLength);
        try
        {
            var textSpan = textBuffer.AsSpan();

            bool nextNewLine = false;

            // Disassemble the instructions
            _currentOffset = 0;
            while (_currentOffset < buffer.Length)
            {
                position = _currentOffset; // Save the current position
                var instruction = Mos6502Instruction.Decode(buffer.Slice(position));
                var instructionSizeInBytes = instruction.SizeInBytes;

                var bytes = instruction.AsSpan;

                if (!instruction.IsValid)
                {
                    bytes = buffer.Slice(position, 1);
                    instructionSizeInBytes = 1;
                }

                PrintLabel(_currentOffset, textSpan, writer, nextNewLine, position == 0, false);
                nextNewLine = false;

                // Pre instruction printer
                if (Options.PreInstructionPrinter is not null)
                {
                    Options.PreInstructionPrinter(_currentOffset, instruction, writer);
                }

                var runningSpan = textSpan;
                int charsWritten = 0;

                // Write the indent
                if (Options.PrintAddress || Options.PrintAssemblyBytes)
                {
                    if (Options.PrintAddress)
                    {
                        textSpan.TryWrite($"{Options.BaseAddress + _currentOffset:X4}", out var localCharsWritten);
                        charsWritten += localCharsWritten;
                        runningSpan = textSpan.Slice(localCharsWritten);

                        TryWriteSpaces(runningSpan, Options.IndentSize);
                        charsWritten += Options.IndentSize;
                        runningSpan = runningSpan.Slice(Options.IndentSize);
                    }

                    if (Options.PrintAssemblyBytes)
                    {
                        int localCharsWritten = 0;
                        switch (bytes.Length)
                        {
                            case 1:
                                runningSpan.TryWrite($"{bytes[0]:X2}       ", out localCharsWritten);
                                break;
                            case 2:
                                runningSpan.TryWrite($"{bytes[0]:X2} {bytes[1]:X2}    ", out localCharsWritten);
                                break;
                            case 3:
                                runningSpan.TryWrite($"{bytes[0]:X2} {bytes[1]:X2} {bytes[2]:X2} ", out localCharsWritten);
                                break;
                        }

                        charsWritten += localCharsWritten;
                        runningSpan = runningSpan.Slice(localCharsWritten);

                        TryWriteSpaces(runningSpan, Options.IndentSize);
                        charsWritten += Options.IndentSize;
                        runningSpan = runningSpan.Slice(Options.IndentSize);
                    }
                }
                else
                {
                    TryWriteSpaces(runningSpan, Options.IndentSize);
                    charsWritten += Options.IndentSize;
                    runningSpan = runningSpan.Slice(Options.IndentSize);
                }

                // Write the instruction
                {
                    _currentPc = (ushort)(_currentOffset + instructionSizeInBytes); // Check overflow
                    _isOperandRelative = instruction.AddressingMode == Mos6502AddressingMode.Relative;

                    int instructionCharsWritten = 0;
                    try
                    {
                        if (instruction.IsValid)
                        {
                            instruction.TryFormat(runningSpan, out instructionCharsWritten, null, Options.FormatProvider, _tryFormatLabelDelegate);
                        }
                        else
                        {
                            runningSpan.TryWrite($"???", out instructionCharsWritten);
                        }
                    }
                    finally
                    {
                        _isOperandRelative = false;
                    }
                    charsWritten += instructionCharsWritten;
                    runningSpan = runningSpan.Slice(instructionCharsWritten);

                    // Write padding
                    if (Options.InstructionTextPaddingLength > 0 && instructionCharsWritten < Options.InstructionTextPaddingLength && TryWriteSpaces(runningSpan, Options.InstructionTextPaddingLength - instructionCharsWritten))
                    {
                        var paddingWritten = Options.InstructionTextPaddingLength - instructionCharsWritten;
                        charsWritten += paddingWritten;
                        runningSpan = runningSpan.Slice(paddingWritten);
                    }

                    if (Options.TryFormatComment is not null && TryWriteSpaces(runningSpan, 4))
                    {
                        charsWritten += 4;
                        runningSpan = runningSpan.Slice(4);

                        if ("; ".TryCopyTo(runningSpan))
                        {
                            charsWritten += 2;
                            runningSpan = runningSpan.Slice(2);
                            if (Options.TryFormatComment(_currentOffset, instruction, runningSpan, out var commentsCharsWritten))
                            {
                                charsWritten += commentsCharsWritten;
                                runningSpan = runningSpan.Slice(commentsCharsWritten);
                            }
                        }
                    }

                    runningSpan = textSpan.Slice(0, charsWritten);
                    runningSpan = runningSpan.TrimEnd(' ');

                    writer.WriteLine(runningSpan);

                    if (instruction.OpCode.IsBranch())
                    {
                        nextNewLine = true;
                    }
                }

                // Post instruction printer
                if (Options.PostInstructionPrinter is not null)
                {
                    Options.PostInstructionPrinter(_currentOffset, instruction, writer);
                }

                _currentOffset = _currentPc;
            }

            // Print any pending labels
            PrintLabel(_currentOffset, textSpan, writer, false, false, true);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(textBuffer);
        }
    }

    private Span<char> GetIndentSpan(Span<char> textSpan)
    {
        var indentSize = Math.Min((int)Options.IndentSize, textSpan.Length);
        if (indentSize > 0)
        {
            for (int indent = 0; indent < indentSize; indent++)
            {
                textSpan[indent] = ' ';
            }

            return textSpan.Slice(0, indentSize);
        }

        return default;
    }

    private bool TryWriteSpaces(Span<char> textSpan, int count)
    {
        if (count > textSpan.Length)
        {
            return false;
        }
        for (int i = 0; i < count; i++)
        {
            textSpan[i] = ' ';
        }
        return true;
    }

    private void WriteIndentSize(Span<char> textSpan, TextWriter writer)
        => writer.Write(GetIndentSpan(textSpan));

    private void PrintLabel(ushort offset, Span<char> textSpan, TextWriter writer, bool nextNewLine, bool isFirstLabel, bool isLast)
    {
        if (_internalLabels.TryGetValue(offset, out var labelIndex))
        {
            if (Options.PrintNewLineBeforeLabel && !isFirstLabel)
            {
                writer.WriteLine();
            }
            if (TryFormatLabelExtended(0, false, textSpan, out var charsWritten) && charsWritten + 1 < textSpan.Length)
            {
                textSpan[charsWritten] = ':';
                charsWritten++;
            }
            else
            {
                var result = textSpan.TryWrite($"{Options.LocalLabelPrefix}{labelIndex:00}:", out charsWritten);
                Debug.Assert(result);
            }
            writer.WriteLine(textSpan.Slice(0, charsWritten));
        }
        else
        {
            if (Options.PrintNewLineAfterBranch && nextNewLine && !isLast)
            {
                writer.WriteLine();
            }
        }
    }

    private bool TryFormatLabelExtended(ushort address, bool handleInternalLabels, Span<char> textSpan, out int charsWritten)
    {
        var offset = _isOperandRelative ? (int)(ushort)(_currentPc + (short)address) : (int)(address - Options.BaseAddress); // TODO: check for overflow

        if (offset < 0 || offset >= ushort.MaxValue)
        {
            charsWritten = 0;
            return false; // Invalid address
        }

        var absoluteOffset = (ushort)offset;

        var tryFormatLabel = Options.TryFormatLabel;
        if (tryFormatLabel is not null && tryFormatLabel(absoluteOffset, textSpan, out charsWritten))
        {
            return true;
        }

        if (handleInternalLabels)
        {
            if (_internalLabels.TryGetValue(absoluteOffset, out var labelIndex))
            {
                return textSpan.TryWrite($"{Options.LocalLabelPrefix}{labelIndex:00}", out charsWritten);
            }

            // If not found, we will provide print absolute address
            textSpan.TryWrite($"${Options.BaseAddress + absoluteOffset:X4}", out charsWritten);
            return false;
        }

        charsWritten = 0;
        return false;
    }

    private static bool CanHaveLabel(Mos6502AddressingMode mode)
    {
        return mode switch
        {
            Mos6502AddressingMode.Absolute
                or Mos6502AddressingMode.AbsoluteX
                or Mos6502AddressingMode.AbsoluteY
                or Mos6502AddressingMode.Indirect
                or Mos6502AddressingMode.IndirectX
                or Mos6502AddressingMode.IndirectY
                or Mos6502AddressingMode.Relative => true,
            _ => false
        };
    }

    private bool TryFormatLabel(ushort offset, Span<char> textSpan, out int charsWritten) => TryFormatLabelExtended(offset, true, textSpan, out charsWritten);
}

/// <summary>
/// A delegate used to print a text before/after an instruction.
/// </summary>
/// <param name="offset">The offset relative to the beginning of the buffer.</param>
/// <param name="instruction">The instruction being decoded.</param>
/// <param name="writer">The text writer to write the text.</param>
public delegate void Mos6502InstructionPrinterDelegate(ushort offset, Mos6502Instruction instruction, TextWriter writer);

/// <summary>
/// A delegate used to format the comment of an instruction.
/// </summary>
/// <param name="offset">The offset relative to the beginning of the buffer.</param>
/// <param name="instruction">The instruction being decoded.</param>
/// <param name="destination">The destination buffer receiving the comment.</param>
/// <param name="charsWritten">The number of character written.</param>
/// <returns><c>true</c> if formatting the comment was successful.</returns>
public delegate bool Mos6502TryFormatInstructionCommentDelegate(ushort offset, Mos6502Instruction instruction, Span<char> destination, out int charsWritten);