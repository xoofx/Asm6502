// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AsmMos6502;

internal static partial class Mos6502Tables
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mos6502AddressingMode GetAddressingModeFromOpcode(byte c) => (Mos6502AddressingMode)Unsafe.Add(ref MemoryMarshal.GetReference(MapOpCodeToAddressingMode), c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mos6502Mnemonic GetMnemonicFromOpcode(byte c) => (Mos6502Mnemonic)Unsafe.Add(ref MemoryMarshal.GetReference(MapOpCodeToMnemonic), c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSizeInBytesFromAddressingMode(Mos6502AddressingMode addressingMode) => Unsafe.Add(ref MemoryMarshal.GetReference(MapAddressingModeToBytes), (byte)addressingMode & 0xF);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCycleCountFromOpcode(Mos6502OpCode opcode) => Unsafe.Add(ref MemoryMarshal.GetReference(MapOpCodeToCycles), (byte)opcode);
    
    public static string GetMnemonicText(Mos6502Mnemonic mnemonic, bool lowercase = false)
        => lowercase
            ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(MapMnemonicToTextLowercase), (byte)mnemonic)
            : Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(MapMnemonicToTextUppercase), (byte)mnemonic);
}