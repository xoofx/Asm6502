// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AsmMos6502.Expressions;

namespace AsmMos6502;

/// <summary>
/// Represents a 6502 assembler for generating machine code and managing labels.
/// </summary>
public partial class Mos6502Assembler : IDisposable
{
    private byte[] _buffer;
    private readonly List<Patch> _patches;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    public Mos6502Assembler(ushort baseAddress = 0xC000)
    {
        _buffer = [];
        _patches = new();
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Gets or sets the base address for code generation.
    /// </summary>
    public ushort BaseAddress { get; set; }

    /// <summary>
    /// Gets the current address in the assembler.
    /// </summary>
    public ushort CurrentAddress => SafeAddress(BaseAddress + SizeInBytes);

    /// <summary>
    /// Gets the current size in bytes of generated assembly code.
    /// </summary>
    public ushort SizeInBytes { get; private set; }

    /// <summary>
    /// Gets the current cycle count for the assembled instructions.
    /// </summary>
    public int CurrentCycleCount { get; private set; }
    
    /// <summary>
    /// Gets or sets the debug map for the assembler.
    /// </summary>
    public IMos6502AssemblerDebugMap? DebugMap { get; set; }
    
    /// <summary>
    /// Gets the buffer containing the assembled instructions.
    /// </summary>
    /// <remarks>
    /// The buffer is a shared buffer that can be used to retrieve the assembled instructions.
    /// The method <see cref="End"/> must be called before using this buffer (if there are labels to patch).
    /// </remarks>
    public Span<byte> Buffer => _buffer.AsSpan(0, (int)SizeInBytes);

    /// <summary>
    /// Writes a buffer of bytes to the assembler's internal buffer.
    /// </summary>
    /// <param name="input">A buffer to append to the assembler's internal buffer.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AppendBuffer(ReadOnlySpan<byte> input)
    {
        var newSizeInBytes = SafeAddress(SizeInBytes + input.Length);
        if (input.Length > 0)
        {
            var span = GetBuffer(input.Length);
            input.CopyTo(span);
            SizeInBytes = newSizeInBytes;
        }

        return this;
    }

    /// <summary>
    /// Appends an 8-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public Mos6502Assembler Append(Expressions.Mos6502ExpressionU8 expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 1);
        var span = GetBuffer(1);
        span[0] = 0; // Initialize with zero
        _patches.Add(new(sizeInBytes, Mos6502AddressingMode.Immediate, expression));
        SizeInBytes = newSizeInBytes;
        return this;
    }

    /// <summary>
    /// Appends an 16-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public Mos6502Assembler Append(Expressions.Mos6502ExpressionU16 expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 2);
        var span = GetBuffer(2);
        span[0] = 0; // Initialize with zero
        span[1] = 0;
        _patches.Add(new(sizeInBytes, Mos6502AddressingMode.Absolute, expression));
        SizeInBytes = newSizeInBytes;
        return this;
    }
    
    /// <summary>
    /// Writes a number of bytes to the assembler's internal buffer, filling them with a specified byte value.
    /// </summary>
    /// <param name="length">The number of bytes to write.</param>
    /// <param name="c">The byte value to fill the buffer with. Default is 0.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AppendBytes(int length, byte c = 0)
    {
        if (length <= 0) return this;
        var newSizeInBytes = SafeAddress(SizeInBytes + length);
        var span = GetBuffer(length);
        span.Fill(c);
        SizeInBytes = newSizeInBytes;
        return this;
    }

    /// <summary>
    /// Resets the assembler state.
    /// </summary>
    public Mos6502Assembler Begin(ushort address = 0xc000)
    {
        BaseAddress = address;
        ReleaseSharedBuffer();
        _patches.Clear();
        SizeInBytes = 0;
        CurrentCycleCount = 0;

        // Reset the base address to the default value
        DebugMap?.BeginProgram(BaseAddress);
        return this;
    }

    /// <summary>
    /// Assembles the instructions and patches the labels.
    /// </summary>
    /// <remarks>
    /// The buffer <see cref="Buffer"/> can be used to retrieve the assembled instructions after calling this method.
    /// </remarks>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler End()
    {
        for (var i = 0; i < _patches.Count; i++)
        {
            var patch = _patches[i];
            var expression = patch.Expression;

            ushort resolved;
            switch (expression)
            {
                case Mos6502Label label:
                    if (!label.IsBound)
                    {
                        throw new InvalidOperationException($"Label number #{i} `{label}` is not bound. Please bind it before assembling.");
                    }
                    resolved = label.Address;
                    break;
                case Mos6502LabelZp label:
                    if (!label.IsBound)
                    {
                        throw new InvalidOperationException($"ZeroPage Label number #{i} `{label}` is not bound. Please bind it before assembling.");
                    }
                    resolved = label.Address;
                    break;
                case Mos6502ExpressionU8 exprU8:
                    resolved = exprU8.Evaluate();
                    break;

                case Mos6502ExpressionU16 exprU16:
                    resolved = exprU16.Evaluate();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported expression type `{expression.GetType()}` for patch at offset 0x{patch.Offset:X4} with expression `{expression}`");
            }
            
            var patchRef = Buffer.Slice(patch.Offset);

            switch (patch.AddressingMode)
            {
                case Mos6502AddressingMode.Immediate:
                case Mos6502AddressingMode.ZeroPage:
                case Mos6502AddressingMode.ZeroPageX:
                case Mos6502AddressingMode.ZeroPageY:
                case Mos6502AddressingMode.IndirectX:
                case Mos6502AddressingMode.IndirectY:
                    patchRef[0] = (byte)(resolved); // Low byte
                    break;
                case Mos6502AddressingMode.Indirect:
                case Mos6502AddressingMode.Absolute:
                case Mos6502AddressingMode.AbsoluteX:
                case Mos6502AddressingMode.AbsoluteY:
                    patchRef[0] = (byte)(resolved); // Low byte
                    patchRef[1] = (byte)(resolved >> 8); // High byte
                    break;
                case Mos6502AddressingMode.Relative:
                    var deltaPc = resolved - (BaseAddress + patch.Offset + 1);
                    if (deltaPc < sbyte.MinValue || deltaPc > sbyte.MaxValue)
                        throw new InvalidOperationException($"Relative address for expression `{expression}` at instruction offset 0x`{patch.Offset - 1:X4}` is out of range: {deltaPc}. Must be [-128, 127] ");

                    patchRef[0] = (byte)deltaPc;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported addressing mode {patch.AddressingMode} for patch at offset 0x{patch.Offset:X4} with expression `{expression}`");
            }
        }

        _patches.Clear();

        // Notifies the current address
        DebugMap?.EndProgram(CurrentAddress);

        return this;
    }

    /// <summary>
    /// Collects all labels used in the assembler's patches.
    /// </summary>
    /// <param name="labels">The labels set to populate.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler CollectLabels(HashSet<IMos6502Label> labels)
    {
        var patches = _patches;
        foreach (var patch in patches)
        {
            patch.Expression.CollectLabels(labels);
        }
        return this;
    }

    /// <summary>
    /// Binds a label to the current address.
    /// </summary>
    /// <param name="label">The label identifier.</param>
    /// <param name="force"><c>true</c> to force rebinding an existing label. Default is <c>false</c></param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler Label(Mos6502Label label, bool force = false)
    {
        if (!force && label.IsBound) throw new InvalidOperationException($"Label {label.Name} is already bound");
        label.Address = SafeAddress(BaseAddress + SizeInBytes);
        label.IsBound = true;
        return this;
    }

    /// <summary>
    /// Binds a new label to the current address.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler Label(out Mos6502Label label) => Label(null, out label);

    /// <summary>
    /// Binds a new label with a specified name to the current address.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler Label(string? name, out Mos6502Label label)
    {
        label = new Mos6502Label(name); // Create an anonymous label
        return Label(label);
    }

    /// <summary>
    /// Creates a new forward label with a specified name that will need to be bound later via <see cref="Label(AsmMos6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler LabelForward(string? name, out Mos6502Label label)
    {
        label = new Mos6502Label(name); // Create an anonymous label
        return this;
    }

    /// <summary>
    /// Creates a new anonymous forward label that will need to be bound later via <see cref="Label(AsmMos6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler LabelForward(out Mos6502Label label)
    {
        label = new Mos6502Label(); // Create an anonymous label
        return this;
    }

    /// <summary>
    /// Adds an instruction to the assembler.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AddInstruction(Mos6502Instruction instruction, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
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

        var debugMap = DebugMap;
        if (debugMap != null)
        {
            // Log debug information for the instruction
            var debugLineInfo = new Mos6502AssemblerDebugLineInfo(currentAddress, debugFilePath ?? string.Empty, debugLineNumber);
            debugMap.LogDebugLineInfo(debugLineInfo);
        }

        return this;
    }

    /// <summary>
    /// Adds an instruction to the assembler, possibly referencing a label.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="debugFilePath">The file path for debugging information (optional).</param>
    /// <param name="debugLineNumber">The line number for debugging information (optional).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AddInstruction(Mos6502Instruction instruction, Mos6502Expression expression, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        var offset = SizeInBytes;
        // ReSharper disable ExplicitCallerInfoArgument
        AddInstruction(instruction, debugFilePath, debugLineNumber);
        // ReSharper restore ExplicitCallerInfoArgument

        _patches.Add(new((ushort)(offset + 1), instruction.AddressingMode, expression));

        return this;
    }

    /// <summary>
    /// Releases resources used by the assembler.
    /// </summary>
    public void Dispose()
    {
        ReleaseSharedBuffer();
    }

    private Span<byte> GetBuffer(int minimumSize)
    {
        if (SizeInBytes + minimumSize > _buffer.Length)
        {
            // Resize the buffer to accommodate the new instruction
            var newSize = Math.Max(_buffer.Length * 2, Math.Max(minimumSize, 16));
            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.CopyTo(newBuffer.AsSpan());
            ReleaseSharedBuffer();
            _buffer = newBuffer;
        }

        return _buffer.AsSpan((int)SizeInBytes, _buffer.Length - (int)SizeInBytes);
    }

    private void ReleaseSharedBuffer()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
        }
    }

    private static ushort SafeAddress(int address)
    {
        if (address > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(address), $"Address {address} is out of range for a 16-bit address space.");
        return (ushort)address;
    }

    /// <summary>
    /// Represents a patch that needs to be applied to a memory location after labels have been bound.
    /// </summary>
    /// <param name="Offset">The offset in the buffer where the patch should be applied.</param>
    /// <param name="AddressingMode">The kind of address for the patch (8 bit, 16 bit).</param>
    /// <param name="Expression">An expression (U8 or U16).</param>
    private record struct Patch(ushort Offset, Mos6502AddressingMode AddressingMode, Mos6502Expression Expression);

}