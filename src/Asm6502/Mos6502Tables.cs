// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Asm6502;

internal static partial class Mos6502Tables
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSizeInBytesFromAddressingMode(Mos6502AddressingMode addressingMode) => Unsafe.Add(ref MemoryMarshal.GetReference(MapAddressingModeToBytes), (byte)addressingMode & 0xF);
}