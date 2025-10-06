// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502.Expressions;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Asm6502;

/// <summary>
/// Base class for a 6502 assembler for generating machine code and managing labels.
/// </summary>
public abstract partial class Mos6502AssemblerBase : IDisposable
{
    private byte[] _buffer;
    private protected readonly List<Patch> Patches;

    /// <summary>
    /// Creates a new instance of the <see cref="Mos6502AssemblerBase"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    protected Mos6502AssemblerBase(ushort baseAddress)
    {
        _buffer = [];
        Patches = new();
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Gets the base address for code generation. This address can be changed using the <see cref="Org(ushort)"/> method.
    /// </summary>
    public ushort BaseAddress { get; private set; }

    /// <summary>
    /// Gets the current address in the assembler.
    /// </summary>
    public ushort CurrentAddress => SafeAddress(BaseAddress + CurrentOffset);
    
    /// <summary>
    /// Gets the current offset relative to the <see cref="BaseAddress"/>.
    /// </summary>
    public ushort CurrentOffset { get; private protected set; }
    
    /// <summary>
    /// Gets the current size in bytes of generated assembly code.
    /// </summary>
    public ushort SizeInBytes { get; private protected set; }

    /// <summary>
    /// Gets the current cycle count for the assembled instructions.
    /// </summary>
    public int CurrentCycleCount { get; private protected set; }

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
    public Mos6502AssemblerBase Append(ReadOnlySpan<byte> input)
    {
        var newSizeInBytes = SafeAddress(SizeInBytes + input.Length);
        if (input.Length > 0)
        {
            var span = GetBuffer(input.Length);
            input.CopyTo(span);
            SizeInBytes = newSizeInBytes;
            CurrentOffset += (ushort)input.Length;
        }

        return this;
    }

    /// <summary>
    /// Writes a buffer of bytes to the assembler's internal buffer.
    /// </summary>
    /// <param name="input">A buffer to append to the assembler's internal buffer.</param>
    /// <returns>The current assembler instance.</returns>
    [Obsolete("This method is deprecated. Please use Append instead.")]
    public Mos6502AssemblerBase AppendBuffer(ReadOnlySpan<byte> input) => Append(input);

    /// <summary>
    /// Appends an 8-bit value to the assembler's internal buffer.
    /// </summary>
    /// <param name="value">An 8-bit value to append.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase Append(byte value)
    {
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 1);
        var span = GetBuffer(1);
        span[0] = value;
        SizeInBytes = newSizeInBytes;
        CurrentOffset += 1;

        return this;
    }

    /// <summary>
    /// Appends an 16-bit value to the assembler's internal buffer.
    /// </summary>
    /// <param name="value">An 16-bit value to append.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase Append(ushort value)
    {
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 2);
        var span = GetBuffer(2);
        span[0] = (byte)value;
        span[1] = (byte)(value >> 8);
        SizeInBytes = newSizeInBytes;
        CurrentOffset += 2;

        return this;
    }

    /// <summary>
    /// Appends an 8-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public Mos6502AssemblerBase Append(Expressions.Mos6502ExpressionU8 expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 1);
        var span = GetBuffer(1);
        span[0] = 0; // Initialize with zero
        Patches.Add(new(CurrentAddress, sizeInBytes, Mos6502AddressingMode.Immediate, expression));
        SizeInBytes = newSizeInBytes;
        CurrentOffset += 1;

        return this;
    }

    /// <summary>
    /// Appends an 16-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public Mos6502AssemblerBase Append(Expressions.Mos6502ExpressionU16 expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var sizeInBytes = SizeInBytes;
        var newSizeInBytes = SafeAddress(sizeInBytes + 2);
        var span = GetBuffer(2);
        span[0] = 0; // Initialize with zero
        span[1] = 0;
        Patches.Add(new(CurrentAddress, sizeInBytes, Mos6502AddressingMode.Absolute, expression));
        SizeInBytes = newSizeInBytes;
        CurrentOffset += 2;

        return this;
    }

    /// <summary>
    /// Writes a number of bytes to the assembler's internal buffer, filling them with a specified byte value.
    /// </summary>
    /// <param name="length">The number of bytes to write.</param>
    /// <param name="c">The byte value to fill the buffer with. Default is 0.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase AppendBytes(int length, byte c = 0)
    {
        if (length <= 0) return this;
        var newSizeInBytes = SafeAddress(SizeInBytes + length);
        var span = GetBuffer(length);
        span.Fill(c);
        SizeInBytes = newSizeInBytes;
        CurrentOffset += (ushort)length;

        return this;
    }

    /// <summary>
    /// Aligns the current output position to the specified byte boundary, optionally filling any added padding with a
    /// specified byte value.
    /// </summary>
    /// <remarks>This method appends padding bytes as needed to advance the output position to the next
    /// multiple of the specified alignment relative to the <see cref="CurrentOffset"/>.
    /// If the <see cref="CurrentOffset"/> is already aligned, no padding is added.</remarks>
    /// <param name="alignment">The byte alignment boundary to align to. Must be greater than zero.</param>
    /// <param name="fill">The byte value to use for padding. The default is 0.</param>
    /// <returns>The current assembler instance, to allow for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if alignment is zero or not power of 2.</exception>
    public Mos6502AssemblerBase Align(ushort alignment, byte fill = 0)
    {
        if (alignment == 0 || !BitOperations.IsPow2(alignment)) 
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be > 0 and a power of 2.");

        var currentOffset = CurrentOffset;
        var alignedSize = (currentOffset + (alignment - 1)) & ~(alignment - 1);
        var padding = alignedSize - currentOffset;
        if (padding > 0)
        {
            AppendBytes((int)padding, fill);
        }
        return this;
    }

    /// <summary>
    /// Resets the assembler state.
    /// </summary>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase Begin(ushort address = 0xc000)
    {
        ReleaseSharedBuffer();
        Patches.Clear();
        SizeInBytes = 0;
        CurrentCycleCount = 0;
        Org(address);

        return this;
    }

    /// <summary>
    /// Resets the current cycle count to zero.
    /// </summary>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase ResetCycle()
    {
        CurrentCycleCount = 0;
        return this;
    }

    /// <summary>
    /// Gets the current cycle count and outputs it to the provided variable.
    /// </summary>
    /// <param name="cycleCount">The output of the current cycle count.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase Cycle(out int cycleCount)
    {
        cycleCount = CurrentCycleCount;
        return this;
    }
    
    /// <summary>
    /// Sets the origin address for the assembler, resets the <see cref="CurrentOffset"/>.
    /// </summary>
    public Mos6502AssemblerBase Org(ushort address)
    {
        BaseAddress = address;
        CurrentOffset = 0;

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
    public Mos6502AssemblerBase End()
    {
        for (var i = 0; i < Patches.Count; i++)
        {
            var patch = Patches[i];
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
                    throw new InvalidOperationException($"Unsupported expression type `{expression.GetType()}` for patch at offset 0x{patch.BufferOffset:X4} with expression `{expression}`");
            }

            var patchRef = Buffer.Slice(patch.BufferOffset);

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
                    var deltaPc = resolved - (patch.Address + 2);
                    if (deltaPc < sbyte.MinValue || deltaPc > sbyte.MaxValue)
                        throw new InvalidOperationException($"Relative address for expression `{expression}` at buffer offset 0x`{patch.BufferOffset - 1:X4}` is out of range: {deltaPc}. Must be [-128, 127] ");

                    patchRef[0] = (byte)deltaPc;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported addressing mode {patch.AddressingMode} for patch at offset 0x{patch.BufferOffset:X4} with expression `{expression}`");
            }
        }

        Patches.Clear();

        // Notifies the current address
        DebugMap?.EndProgram(CurrentAddress);

        return this;
    }

    /// <summary>
    /// Collects all labels used in the assembler's patches.
    /// </summary>
    /// <param name="labels">The labels set to populate.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase CollectLabels(HashSet<IMos6502Label> labels)
    {
        var patches = Patches;
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
    public Mos6502AssemblerBase Label(Mos6502Label label, bool force = false)
    {
        if (!force && label.IsBound) throw new InvalidOperationException($"Label {label.Name} is already bound");
        label.Address = CurrentAddress;
        label.IsBound = true;
        return this;
    }

    /// <summary>
    /// Binds a new label to the current address.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <param name="labelExpression">The label expression (for debugging purpose). This argument is automatically setup.</param>
    /// <returns>The current assembler instance.</returns>
    /// <remarks>
    /// The label name is extracted from the C# expression passed as argument. For example: <c>assembler.LabelForward(out var myLabel);</c> will create a label with the name "myLabel".
    /// </remarks>
    public Mos6502AssemblerBase Label(out Mos6502Label label, [CallerArgumentExpression(nameof(label))] string? labelExpression = null)
    {
        return Label(Mos6502Label.ParseCSharpExpression(labelExpression), out label);
    }

    /// <summary>
    /// Binds a new label with a specified name to the current address.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase Label(string? name, out Mos6502Label label)
    {
        label = new Mos6502Label(name); // Create an anonymous label
        return Label(label);
    }

    /// <summary>
    /// Creates a new forward label with a specified name that will need to be bound later via <see cref="Label(Asm6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase LabelForward(string? name, out Mos6502Label label)
    {
        label = new Mos6502Label(name); // Create an anonymous label
        return this;
    }

    /// <summary>
    /// Creates a new forward label that will need to be bound later via <see cref="Label(Asm6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <param name="labelExpression">The label expression (for debugging purpose). This argument is automatically setup.</param>
    /// <returns>The current assembler instance.</returns>
    /// <remarks>
    /// The label name is extracted from the C# expression passed as argument. For example: <c>assembler.LabelForward(out var myLabel);</c> will create a label with the name "myLabel".
    /// </remarks>
    public Mos6502AssemblerBase LabelForward(out Mos6502Label label, [CallerArgumentExpression(nameof(label))] string? labelExpression = null)
    {
        label = new Mos6502Label(Mos6502Label.ParseCSharpExpression(labelExpression)); // Create an anonymous label
        return this;
    }

    /// <summary>
    /// Releases resources used by the assembler.
    /// </summary>
    public void Dispose()
    {
        ReleaseSharedBuffer();
    }

    private protected Span<byte> GetBuffer(int requestedSize)
    {
        if (SizeInBytes + requestedSize > _buffer.Length)
        {
            // Resize the buffer to accommodate the new instruction
            var newSize = Math.Max(_buffer.Length * 2, Math.Max(SizeInBytes + requestedSize, 16));
            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.CopyTo(newBuffer.AsSpan());
            ReleaseSharedBuffer();
            _buffer = newBuffer;
        }

        return _buffer.AsSpan((int)SizeInBytes, requestedSize);
    }

    private void ReleaseSharedBuffer()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
        }
    }

    private protected static ushort SafeAddress(int address)
    {
        if (address > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(address), $"Address {address} is out of range for a 16-bit address space.");
        return (ushort)address;
    }

    /// <summary>
    /// Represents a patch that needs to be applied to a memory location after labels have been bound.
    /// </summary>
    /// <param name="Address">The base address where the patch is applied.</param>
    /// <param name="BufferOffset">The offset in the buffer where the patch should be applied.</param>
    /// <param name="AddressingMode">The kind of address for the patch (8 bit, 16 bit).</param>
    /// <param name="Expression">An expression (U8 or U16).</param>
    private protected record struct Patch(ushort Address, ushort BufferOffset, Mos6502AddressingMode AddressingMode, Mos6502Expression Expression);
}

/// <summary>
/// Represents a 6502 assembler for generating machine code and managing labels.
/// </summary>
public abstract partial class Mos6502AssemblerBase<TAsm> : Mos6502AssemblerBase where TAsm : Mos6502AssemblerBase<TAsm>
{

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    protected Mos6502AssemblerBase(ushort baseAddress = 0xC000) : base(baseAddress)
    {
    }

    /// <summary>
    /// Writes a buffer of bytes to the assembler's internal buffer.
    /// </summary>
    /// <param name="input">A buffer to append to the assembler's internal buffer.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Append(ReadOnlySpan<byte> input) => (TAsm)base.Append(input);

    /// <summary>
    /// Writes a buffer of bytes to the assembler's internal buffer.
    /// </summary>
    /// <param name="input">A buffer to append to the assembler's internal buffer.</param>
    /// <returns>The current assembler instance.</returns>
    [Obsolete("This method is deprecated. Please use Append instead.")]
    public new TAsm AppendBuffer(ReadOnlySpan<byte> input) => (TAsm)base.Append(input);

    /// <summary>
    /// Appends an 8-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public new TAsm Append(Expressions.Mos6502ExpressionU8 expression) => (TAsm)base.Append(expression);

    /// <summary>
    /// Appends an 16-bit expression to the assembler's internal buffer.
    /// </summary>
    /// <param name="expression">An 16-bit expression to append.</param>
    /// <returns>The current assembler instance.</returns>
    /// <exception cref="ArgumentNullException">if expression is null</exception>
    public new TAsm Append(Expressions.Mos6502ExpressionU16 expression) => (TAsm)base.Append(expression);

    /// <summary>
    /// Appends an 8-bit value to the assembler's internal buffer.
    /// </summary>
    /// <param name="value">An 8-bit value to append.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Append(byte value) => (TAsm)base.Append(value);

    /// <summary>
    /// Appends an 16-bit value to the assembler's internal buffer.
    /// </summary>
    /// <param name="value">An 16-bit value to append.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Append(ushort value) => (TAsm)base.Append(value);

    /// <summary>
    /// Writes a number of bytes to the assembler's internal buffer, filling them with a specified byte value.
    /// </summary>
    /// <param name="length">The number of bytes to write.</param>
    /// <param name="c">The byte value to fill the buffer with. Default is 0.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm AppendBytes(int length, byte c = 0) => (TAsm)base.AppendBytes(length, c);

    /// <summary>
    /// Aligns the current output position to the specified byte boundary, optionally filling any added padding with a
    /// specified byte value.
    /// </summary>
    /// <remarks>This method appends padding bytes as needed to advance the output position to the next
    /// multiple of the specified alignment relative to the <see cref="Mos6502AssemblerBase.CurrentOffset"/>.
    /// If the <see cref="Mos6502AssemblerBase.CurrentOffset"/> is already aligned, no padding is added.</remarks>
    /// <param name="alignment">The byte alignment boundary to align to. Must be greater than zero.</param>
    /// <param name="fill">The byte value to use for padding. The default is 0.</param>
    /// <returns>The current assembler instance, to allow for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if alignment is zero.</exception>
    public new TAsm Align(ushort alignment, byte fill = 0) => (TAsm)base.Align(alignment, fill);

    /// <summary>
    /// Resets the assembler state.
    /// </summary>
    public new TAsm Begin(ushort address = 0xc000) => (TAsm)base.Begin(address);

    /// <summary>
    /// Resets the current cycle count to zero.
    /// </summary>
    /// <returns>The current assembler instance.</returns>
    public new TAsm ResetCycle() => (TAsm)base.ResetCycle();

    /// <summary>
    /// Gets the current cycle count and outputs it to the provided variable.
    /// </summary>
    /// <param name="cycleCount">The output of the current cycle count.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Cycle(out int cycleCount) => (TAsm)base.Cycle(out cycleCount);

    /// <summary>
    /// Sets the origin address for the assembler and resets the <see cref="Mos6502AssemblerBase.CurrentOffset"/>.
    /// </summary>
    public new TAsm Org(ushort address) => (TAsm)base.Org(address);

    /// <summary>
    /// Assembles the instructions and patches the labels.
    /// </summary>
    /// <remarks>
    /// The buffer <see cref="Buffer"/> can be used to retrieve the assembled instructions after calling this method.
    /// </remarks>
    /// <returns>The current assembler instance.</returns>
    public new TAsm End() => (TAsm)base.End();

    /// <summary>
    /// Collects all labels used in the assembler's patches.
    /// </summary>
    /// <param name="labels">The labels set to populate.</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm CollectLabels(HashSet<IMos6502Label> labels) => (TAsm)base.CollectLabels(labels);

    /// <summary>
    /// Binds a label to the current address.
    /// </summary>
    /// <param name="label">The label identifier.</param>
    /// <param name="force"><c>true</c> to force rebinding an existing label. Default is <c>false</c></param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Label(Mos6502Label label, bool force = false) => (TAsm)base.Label(label, force);

    /// <summary>
    /// Binds a new label to the current address.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <param name="labelExpression">The label expression (for debugging purpose). This argument is automatically setup.</param>
    /// <returns>The current assembler instance.</returns>
    /// <remarks>
    /// The label name is extracted from the C# expression passed as argument. For example: <c>assembler.LabelForward(out var myLabel);</c> will create a label with the name "myLabel".
    /// </remarks>
    public new TAsm Label(out Mos6502Label label, [CallerArgumentExpression(nameof(label))] string? labelExpression = null) => (TAsm)base.Label(out label, labelExpression);

    /// <summary>
    /// Binds a new label with a specified name to the current address.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm Label(string? name, out Mos6502Label label) => (TAsm)base.Label(name, out label);

    /// <summary>
    /// Creates a new forward label with a specified name that will need to be bound later via <see cref="Label(Asm6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="label">The label identifier (output).</param>
    /// <returns>The current assembler instance.</returns>
    public new TAsm LabelForward(string? name, out Mos6502Label label) => (TAsm)base.LabelForward(name, out label);

    /// <summary>
    /// Creates a new anonymous forward label that will need to be bound later via <see cref="Label(Asm6502.Mos6502Label,bool)"/>.
    /// </summary>
    /// <param name="label">The label identifier (output).</param>
    /// <param name="labelExpression">The label expression (for debugging purpose). This argument is automatically setup.</param>
    /// <returns>The current assembler instance.</returns>
    /// <remarks>
    /// The label name is extracted from the C# expression passed as argument. For example: <c>assembler.LabelForward(out var myLabel);</c> will create a label with the name "myLabel".
    /// </remarks>
    public new TAsm LabelForward(out Mos6502Label label, [CallerArgumentExpression(nameof(label))] string? labelExpression = null) => (TAsm)base.LabelForward(out label, labelExpression);
}