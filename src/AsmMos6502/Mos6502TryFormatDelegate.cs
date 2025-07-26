// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

/// <summary>
/// Delegates used to format a label.
/// </summary>
/// <param name="address">The address of the label. This address is relative to the instruction being decoded when using <see cref="Mos6502Instruction.Decode"/> and relative to the beginning of the buffer being decoded when using <see cref="Mos6502Disassembler"/>.</param>
/// <param name="destination">The char destination buffer.</param>
/// <param name="charsWritten">The number of character written.</param>
/// <returns><c>true</c> if the label has been formatted; <c>false</c> otherwise.</returns>
public delegate bool Mos6502TryFormatDelegate(ushort address, Span<char> destination, out int charsWritten);