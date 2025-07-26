// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502;

public static class Mos6502Extensions
{
    /// <summary>
    /// Returns <c>true</c> if the specified opcode is a branch instruction (conditional jump, jump or return).
    /// </summary>
    /// <param name="opcode">The opcode to test against.</param>
    /// <returns><c>true</c> if the specified opcode is a branch instruction (conditional jump, jump or return), otherwise <c>false</c>.</returns>
    public static bool IsBranch(this Mos6502OpCode opcode)
    {
        switch (opcode)
        {
            case Mos6502OpCode.BCC_Relative:
            case Mos6502OpCode.BCS_Relative:
            case Mos6502OpCode.BEQ_Relative:
            case Mos6502OpCode.BMI_Relative:
            case Mos6502OpCode.BNE_Relative:
            case Mos6502OpCode.BPL_Relative:
            case Mos6502OpCode.BVC_Relative:
            case Mos6502OpCode.BVS_Relative:
            case Mos6502OpCode.JMP_Absolute:
            case Mos6502OpCode.JMP_Indirect:
            case Mos6502OpCode.JSR_Absolute:
            case Mos6502OpCode.RTS_Implied:
            case Mos6502OpCode.RTI_Implied:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the text representation of a <see cref="Mos6502Mnemonic"/>.
    /// </summary>
    /// <param name="mnemonic">The mnemonic value.</param>
    /// <param name="lowercase"><c>true</c> to get a lowercase mnemonic. Default is <c>false</c>.</param>
    /// <returns>A text representation of the mnemonic.</returns>
    public static string ToText(this Mos6502Mnemonic mnemonic, bool lowercase = false) => Mos6502Tables.GetMnemonicText(mnemonic, lowercase);


    /// <summary>
    /// Gets the size in bytes of an instruction from its addressing mode.
    /// </summary>
    /// <param name="addressingMode">Addressing mode of an instruction</param>
    /// <returns>The size in bytes of an instruction.</returns>
    public static int ToSizeInBytes(this Mos6502AddressingMode addressingMode) => Mos6502Tables.GetSizeInBytesFromAddressingMode(addressingMode);

    /// <summary>
    /// Gets the cycle count from an opcode.
    /// </summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The cycle count for the specified opcode.</returns>
    public static int ToCycleCount(this Mos6502OpCode opcode) => Mos6502Tables.GetCycleCountFromOpcode(opcode);
    
    /// <summary>
    /// Gets the mnemonic from an opcode.
    /// </summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The mnemonic for the specified opcode.</returns>
    public static Mos6502Mnemonic ToMnemonic(this Mos6502OpCode opcode) => Mos6502Tables.GetMnemonicFromOpcode((byte)opcode);
    
    /// <summary>
    /// Gets the addressing mode from an opcode.
    /// </summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The addressing mode for the specified opcode.</returns>
    public static Mos6502AddressingMode ToAddressingMode(this Mos6502OpCode opcode) => Mos6502Tables.GetAddressingModeFromOpcode((byte)opcode);
}