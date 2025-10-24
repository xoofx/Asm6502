// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace Asm6502;

/// <summary>
/// Represents a contiguous block of bytes to be placed in the assembled output.
/// </summary>
/// <remarks>
/// An <see cref="AsmBlock"/> bundles:
/// - a <see cref="Mos6502Label"/> that may be already bound (fixed address) or unbound,
/// - a <see cref="Buffer"/> containing the raw bytes to emit,
/// - an <see cref="Alignment"/> constraint (power of two).
/// <para>
/// This type is consumed by <see cref="Mos6502AssemblerBase.ArrangeBlocks(AsmBlock[])"/> to layout blocks in memory:
/// fixed-address blocks are placed at their label address; unbound labels are bound when their block is placed
/// at the next suitable address respecting the block alignment. Gaps are filled with zero bytes.
/// </para>
/// </remarks>
[DebuggerDisplay("{" + nameof(ToDebuggerDisplay) + "(),nq}")]
public class AsmBlock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsmBlock"/> class.
    /// </summary>
    /// <param name="label">
    /// The label associated with this block. If the label is already bound (<see cref="Mos6502Label.IsBound"/> is true),
    /// the block is placed at <see cref="Mos6502Label.Address"/>; otherwise, it is placed sequentially and the label is bound when arranged.
    /// </param>
    /// <param name="buffer">The raw bytes that compose the contents of the block.</param>
    /// <param name="alignment">
    /// The required alignment in bytes for the starting address of this block. Must be a power of two. Defaults to 1 (no alignment).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="alignment"/> is not a power of two.</exception>
    public AsmBlock(Mos6502Label label, byte[] buffer, ushort alignment = 1)
    {
        if (!BitOperations.IsPow2(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a power of 2.");
        }
        this.Label = label;
        this.Buffer = buffer;
        this.Alignment = alignment;
    }

    /// <summary>
    /// Gets the label associated with this block.
    /// </summary>
    /// <remarks>
    /// When arranging blocks, a bound label fixes the placement to <see cref="Mos6502Label.Address"/>.
    /// An unbound label is bound to the address at which the block is placed.
    /// </remarks>
    public Mos6502Label Label { get; }

    /// <summary>
    /// Gets the raw bytes that make up the contents of this block.
    /// </summary>
    public byte[] Buffer { get; }

    /// <summary>
    /// Gets the required alignment (in bytes, power of two) for the block starting address.
    /// </summary>
    /// <remarks>
    /// A value of 1 means no alignment constraint. Higher values align the block's start to the specified boundary.
    /// </remarks>
    public ushort Alignment { get; }

    /// <summary>
    /// Returns a human-readable representation of this block for debugging purposes.
    /// </summary>
    /// <returns>A string containing the label, address, size, and alignment.</returns>
    public override string ToString() => ToDebuggerDisplay();
    
    private string ToDebuggerDisplay()
    {
        return $"AsmBlockData: {Label}(${Label.Address:x4}), Size={Buffer.Length}, Alignment={Alignment}";
    }
}