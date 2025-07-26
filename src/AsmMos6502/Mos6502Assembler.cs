// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace AsmMos6502;

/// <summary>
/// Represents a 6502 assembler for generating machine code and managing labels.
/// </summary>
public partial class Mos6502Assembler : IDisposable
{
    private byte[] _buffer;
    private readonly List<UnboundInstructionLabel> _instructionsWithLabelToPatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Assembler"/> class.
    /// </summary>
    /// <param name="baseAddress">The base address for code generation.</param>
    public Mos6502Assembler(ushort baseAddress = 0xC000)
    {
        _buffer = [];
        _instructionsWithLabelToPatch = new();
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
    public void Begin()
    {
        ReleaseSharedBuffer();
        _instructionsWithLabelToPatch.Clear();
        SizeInBytes = 0;
        CurrentCycleCount = 0;
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
        for (var i = 0; i < _instructionsWithLabelToPatch.Count; i++)
        {
            var unboundLabel = _instructionsWithLabelToPatch[i];
            var label = unboundLabel.Label;

            if (!label.IsBound)
            {
                throw new InvalidOperationException($"Label number #{i} `{label}` is not bound. Please bind it before assembling.");
            }

            var instruction = Mos6502Instruction.Decode(Buffer.Slice(unboundLabel.InstructionOffset));

            // TODO: adjust cycle count based on the target address (if it crosses a page boundary)
            switch (unboundLabel.AddressKind)
            {
                case Mos6502AddressKind.None:
                case Mos6502AddressKind.Absolute:
                    instruction = new(instruction.OpCode, (ushort)label.Address);
                    break;
                case Mos6502AddressKind.Relative:
                    var deltaPc = label.Address - (BaseAddress + unboundLabel.InstructionOffset + instruction.SizeInBytes);
                    if (deltaPc < sbyte.MinValue || deltaPc > sbyte.MaxValue)
                        throw new InvalidOperationException($"Relative address for label `{label}` at instruction `{instruction}` is out of range: {deltaPc}. Must be [-128, 127] ");

                    instruction = new(instruction.OpCode, (byte)deltaPc);
                    break;
            }

            instruction.AsSpan.CopyTo(Buffer.Slice((int)unboundLabel.InstructionOffset));
        }

        _instructionsWithLabelToPatch.Clear();

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
    /// Adds an instruction to the assembler.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AddInstruction(Mos6502Instruction instruction)
    {
        if (!instruction.IsValid) throw new ArgumentException("Invalid instruction", nameof(instruction));

        var sizeInBytes = instruction.SizeInBytes;
        Debug.Assert(sizeInBytes > 0);
        var span = GetBuffer(sizeInBytes);
        instruction.AsSpan.CopyTo(span);
        SizeInBytes = SafeAddress(SizeInBytes + (byte)sizeInBytes);
        CurrentCycleCount += instruction.CycleCount;

        return this;
    }

    /// <summary>
    /// Adds an instruction to the assembler, possibly referencing a label.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    /// <param name="label">The label to reference.</param>
    /// <returns>The current assembler instance.</returns>
    public Mos6502Assembler AddInstruction(Mos6502Instruction instruction, Mos6502Label label)
    {
        if (label.IsBound)
        {
            instruction = new Mos6502Instruction(instruction.OpCode, (ushort)label.Address);
        }

        var offset = SizeInBytes;
        AddInstruction(instruction);

        var addressKind = instruction.GetAddressKind();
        if (!label.IsBound || addressKind == Mos6502AddressKind.Relative)
        {
            _instructionsWithLabelToPatch.Add(new(offset, label, addressKind));
        }

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

    private record struct UnboundInstructionLabel(ushort InstructionOffset, Mos6502Label Label, Mos6502AddressKind AddressKind);

    private static ushort SafeAddress(int address)
    {
        if (address > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(address), $"Address {address} is out of range for a 16-bit address space.");
        return (ushort)address;
    }
}
