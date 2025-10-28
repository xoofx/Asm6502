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
    private List<Action> _callBacksOnResolve;

    /// <summary>
    /// Creates a new instance of the <see cref="Mos6502AssemblerBase"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    protected Mos6502AssemblerBase(ushort baseAddress)
    {
        _buffer = [];
        Patches = new();
        _callBacksOnResolve = new();
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Gets the base address for code generation. This address can be changed using the <see cref="Org"/> method.
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
    /// Initializes a new assembly session starting at the specified address and optionally assigns a name to the
    /// session.
    /// </summary>
    /// <param name="address">The starting memory address for the assembly session. Defaults to 0xC000 if not specified.</param>
    /// <param name="name">An optional name to associate with the assembly session. Can be null if no name is required.</param>
    /// <returns>The current instance of the assembler, allowing for method chaining.</returns>
    public Mos6502AssemblerBase Begin(ushort address = 0xc000, string? name = null)
    {
        ReleaseSharedBuffer();
        Patches.Clear();
        SizeInBytes = 0;
        CurrentCycleCount = 0;
        Org(address, name);

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
    /// Sets the program's origin address and optionally assigns a name to the program section.
    /// </summary>
    /// <remarks>Calling this method resets the current offset to zero and updates the debug mapping if
    /// available. Use this method to define the starting point for code generation in the assembled output.</remarks>
    /// <param name="address">The starting address in memory where the program will be assembled.</param>
    /// <param name="name">An optional name for the program section. If null, no name is assigned.</param>
    /// <returns>The current instance of the assembler, allowing for method chaining.</returns>
    public Mos6502AssemblerBase Org(ushort address, string? name = null)
    {
        BaseAddress = address;
        CurrentOffset = 0;

        // Reset the base address to the default value
        DebugMap?.LogDebugInfo(new(BaseAddress, Mos6502AssemblerDebugInfoKind.OriginBegin, name));

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
        DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.End));

        // Invoke callbacks
        foreach (var callback in _callBacksOnResolve)
        {
            callback();
        }
        _callBacksOnResolve.Clear();

        return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the method <see cref="End"/> is called and the resolve label addresses completes.
    /// </summary>
    /// <param name="callback">The action to execute when the resolve operation ends. Cannot be null.</param>
    /// <returns>The current assembler instance, allowing for method chaining.</returns>
    /// <remarks>
    /// The callbacks are removed after <see cref="End"/> is called.
    /// </remarks>
    public Mos6502AssemblerBase OnResolveEnd(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callBacksOnResolve.Add(callback);
        return this;
    }

    /// <summary>
    /// Collects all labels used in the assembler's patches.
    /// </summary>
    /// <param name="labels">The labels set to populate.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502AssemblerBase CollectLabels(HashSet<Mos6502Label> labels)
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
    /// Arranges the specified assembly blocks in memory, filling any gaps and binding labels as needed.
    /// </summary>
    /// <remarks>This method places each block at the current address, fills any gaps between blocks with zero
    /// bytes, and ensures that labels are bound to their corresponding addresses. Use this method to control the layout
    /// of code or data blocks within the assembled output.
    /// Some blocks may have fixed addresses (if their label is already bound), while others will be placed sequentially.
    /// </remarks>
    /// <param name="blocks">An array of assembly blocks to arrange sequentially in memory.</param>
    /// <returns>The current assembler instance, allowing for method chaining.</returns>
    public Mos6502AssemblerBase ArrangeBlocks(params AsmBlock[] blocks)
    {
        var newBlocks = ArrangeBlocks(blocks, CurrentAddress);

        foreach (var (address, block) in newBlocks)
        {
            var fill = address - CurrentAddress;
            if (fill > 0)
            {
                // Fill gap
                AppendBytes(fill, 0);
            }

            // Place label
            if (!block.Label.IsBound)
            {
                Label(block.Label);
            }

            // Append block data
            Append(block.Buffer);
        }

        return this;
    }

    /// <summary>
    /// Begins a new code section in the assembler, optionally assigning it a name.
    /// </summary>
    /// <param name="name">The optional name to assign to the new code section. If null, the section is unnamed.</param>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public Mos6502AssemblerBase BeginCodeSection(string? name = null)
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.CodeSectionBegin, name));
        return this;
    }

    /// <summary>
    /// Ends the current code section and finalizes any associated debug mapping for the current address.
    /// </summary>
    /// <remarks>Call this method after completing a code section to ensure that debug information is properly
    /// finalized.</remarks>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public Mos6502AssemblerBase EndCodeSection()
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.CodeSectionEnd));
        return this;
    }

    /// <summary>
    /// Begins a new data section in the assembly, optionally assigning it a name.
    /// </summary>
    /// <param name="name">The optional name to assign to the data section. If null, the section is unnamed.</param>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public Mos6502AssemblerBase BeginDataSection(string? name = null)
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.DataSectionBegin, name));
        return this;
    }

    /// <summary>
    /// Ends the current data section in the assembler and updates the debug map accordingly.
    /// </summary>
    /// <returns>The current instance of <see cref="Mos6502AssemblerBase"/> to allow for method chaining.</returns>
    public Mos6502AssemblerBase EndDataSection()
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.DataSectionEnd));
        return this;
    }

    /// <summary>
    /// Begins the definition of a new function in the assembler source code and logs associated debug information.
    /// </summary>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public Mos6502AssemblerBase BeginFunction([CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.FunctionBegin, debugFilePath, debugLineNumber));
        return this;
    }

    /// <summary>
    /// Marks the end of the current function in the assembly process and updates the debug information accordingly.
    /// </summary>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public Mos6502AssemblerBase EndFunction()
    {
        this.DebugMap?.LogDebugInfo(new(CurrentAddress, Mos6502AssemblerDebugInfoKind.FunctionEnd));
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

    private static List<(ushort Address, AsmBlock Block)> ArrangeBlocks(AsmBlock[] blocks, ushort startAddress)
    {
        var fixedBlocks = blocks.Where(x => x.Label.IsBound)
            .OrderBy(x => x.Label.Address)
            .ToList();

        // Step 0: Check that fixed address are not before the start address
        foreach (var fixedBlk in fixedBlocks)
        {
            if (fixedBlk.Label.Address < startAddress)
                throw new InvalidOperationException($"Fixed block address 0x{fixedBlk.Label.Address:X4} for label {fixedBlk.Label} is before the start address 0x{startAddress:X4}");
        }
        
        // Step 1: Validate fixed blocks (no overlaps)
        for (int i = 0; i < fixedBlocks.Count - 1; i++)
        {
            var a = fixedBlocks[i];
            var b = fixedBlocks[i + 1];
            ushort aEnd = (ushort)(a.Label.Address + a.Buffer.Length);
            if (aEnd > b.Label.Address)
                throw new InvalidOperationException($"Fixed block overlap: {a.Label} vs {b.Label}");
        }

        var floatingBlocks = blocks.Where(x => !x.Label.IsBound)
            .OrderByDescending(x => x.Buffer.Length)
            .ToList();

        // Build gaps: (startAddr, size) -> dynamic updates
        var gaps = new List<(ushort StartAddress, ushort EndAddress)>();
        ushort cursor = startAddress;

        foreach (var fixedBlk in fixedBlocks)
        {
            if (cursor < fixedBlk.Label.Address)
                gaps.Add((cursor, fixedBlk.Label.Address));
            cursor = (ushort)(fixedBlk.Label.Address + fixedBlk.Buffer.Length);
        }

        gaps.Add((cursor, ushort.MaxValue)); // open ended final gap

        List<(ushort Address, AsmBlock Block)> result = new();
        foreach (var fixedBlk in fixedBlocks)
        {
            result.Add((fixedBlk.Label.Address, fixedBlk));
        }

        // Place floating blocks
        foreach (var block in floatingBlocks)
        {
            int size = block.Buffer.Length;
            ushort align = block.Alignment;
            bool placed = false;

            for (int g = 0; g < gaps.Count && !placed; g++)
            {
                var gap = gaps[g];
                ushort addr = AlignUp(gap.StartAddress, align);
                if (addr + size <= gap.EndAddress)
                {
                    // Place block
                    result.Add((addr, block));

                    // Update gap
                    gaps[g] = ((ushort)(addr + size), gap.EndAddress);
                    placed = true;
                }
            }

            if (!placed)
                throw new InvalidOperationException($"Cannot place block {block.Label}");
        }

        return result.OrderBy(x => x.Address).ToList();

        static ushort AlignUp(ushort value, ushort align) => (ushort)((value + (align - 1)) & ~(align - 1));
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
    public new TAsm Begin(ushort address = 0xc000, string? name = null) => (TAsm)base.Begin(address, name);

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
    public new TAsm Org(ushort address, string? name = null) => (TAsm)base.Org(address, name);

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
    public new TAsm CollectLabels(HashSet<Mos6502Label> labels) => (TAsm)base.CollectLabels(labels);

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

    /// <summary>
    /// Arranges the specified assembly blocks in memory, filling any gaps and binding labels as needed.
    /// </summary>
    /// <remarks>This method places each block at the current address, fills any gaps between blocks with zero
    /// bytes, and ensures that labels are bound to their corresponding addresses. Use this method to control the layout
    /// of code or data blocks within the assembled output.
    /// Some blocks may have fixed addresses (if their label is already bound), while others will be placed sequentially.
    /// </remarks>
    /// <param name="blocks">An array of assembly blocks to arrange sequentially in memory.</param>
    /// <returns>The current assembler instance, allowing for method chaining.</returns>
    public new TAsm ArrangeBlocks(params AsmBlock[] blocks) => (TAsm)base.ArrangeBlocks(blocks);

    /// <summary>
    /// Registers a callback to be invoked when the method <see cref="End"/> is called and the resolve label addresses completes.
    /// </summary>
    /// <param name="callback">The action to execute when the resolve operation ends. Cannot be null.</param>
    /// <returns>The current assembler instance, allowing for method chaining.</returns>
    /// <remarks>
    /// The callbacks are removed after <see cref="End"/> is called.
    /// </remarks>
    public new TAsm OnResolveEnd(Action callback) => (TAsm)base.OnResolveEnd(callback);

    /// <summary>
    /// Begins a new code section in the assembler, optionally assigning it a name.
    /// </summary>
    /// <param name="name">The optional name to assign to the new code section. If null, the section is unnamed.</param>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public new TAsm BeginCodeSection(string? name = null) => (TAsm)base.BeginCodeSection(name);

    /// <summary>
    /// Ends the current code section and finalizes any associated debug mapping for the current address.
    /// </summary>
    /// <remarks>Call this method after completing a code section to ensure that debug information is properly
    /// finalized.</remarks>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public new TAsm EndCodeSection() => (TAsm)base.EndCodeSection();

    /// <summary>
    /// Begins a new data section in the assembly, optionally assigning it a name.
    /// </summary>
    /// <param name="name">The optional name to assign to the data section. If null, the section is unnamed.</param>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public new TAsm BeginDataSection(string? name = null) => (TAsm)base.BeginDataSection(name);

    /// <summary>
    /// Ends the current data section in the assembler and updates the debug map accordingly.
    /// </summary>
    /// <returns>The current instance of <see cref="Mos6502AssemblerBase"/> to allow for method chaining.</returns>
    public new TAsm EndDataSection() => (TAsm)base.EndDataSection();
    
    /// <summary>
    /// Begins the definition of a new function in the assembler source code and logs associated debug information.
    /// </summary>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    /// <remarks>
    /// The debug file path and line numbers should be provided by the calling function in order to propagate where the function is used.
    /// </remarks>
    public new TAsm BeginFunction([CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0) => (TAsm)base.BeginFunction(debugFilePath, debugLineNumber);

    /// <summary>
    /// Marks the end of the current function in the assembly process and updates the debug information accordingly.
    /// </summary>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public new TAsm EndFunction() => (TAsm)base.EndFunction();
}