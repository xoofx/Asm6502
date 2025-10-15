// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// The code in this file was originally written by Jacob Paul at https://github.com/ericssonpaul/O2
// 
// It has been heavily modified to:
// - Integrate it better with Asm6502 and C#
// - Rely on Asm6502 builtin decoding logic
// - Fix issues with regular opcode (BCD mode)
// - Add support for all undocumented opcodes in Mos6510Cpu
// - Fix accurate cycles for all instructions to pass Thomas Harte test suite for the 6502
//   https://github.com/SingleStepTests/65x02
//
// MIT License
// 
// Copyright (c) 2021 Jacob Paul
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// ReSharper disable InconsistentNaming
#pragma warning disable CS1591

namespace Asm6502;

/// <summary>
/// Represents a MOS 6510 CPU, providing full emulation of all documented and undocumented instructions
/// supported by the 6510 processor. Inherits core functionality from the MOS 6502 CPU implementation and extends it to
/// support the additional opcodes and behaviors specific to the 6510.
/// </summary>
/// <remarks>
/// This class inherits from <see cref="Mos6502Cpu"/> and overrides methods to implement the unique features of the 6510 CPU.
///
/// This class is preferred to be used when emulating systems that utilize the 6510 CPU, such as the Commodore 64,
/// </remarks>
public class Mos6510Cpu : Mos6502Cpu
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6510Cpu"/> class.
    /// </summary>
    public Mos6510Cpu()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6510Cpu"/> class with the specified memory bus.
    /// </summary>
    /// <param name="bus">The memory bus used by the CPU to read from and write to memory.</param>
    public Mos6510Cpu(IMos6502CpuMemoryBus bus) : base(bus)
    {
    }

    private protected override void DecodeOpCode(Mos6502OpCode opcode6502, out Mos6502AddressingMode addressingMode, out Mos6502Mnemonic mnemonic)
    {
        // Mos6510 covers all Mos6502 opcodes including undocumented ones
        var opcode = (Mos6510OpCode)opcode6502;
        addressingMode = opcode.ToAddressingMode();
        mnemonic = (Mos6502Mnemonic)opcode.ToMnemonic();
    }

    private protected override void InternalFastStep()
    {
        switch ((Mos6510OpCode)_opcode)
        {
            // Undocumented opcodes - ALR
            case Mos6510OpCode.ALR_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                ALR_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - ANC
            case Mos6510OpCode.ANC_Immediate:
            case Mos6510OpCode.ANC_2B_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                ANC_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - ANE
            case Mos6510OpCode.ANE_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                ANE_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - ARR
            case Mos6510OpCode.ARR_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                ARR_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - DCP
            case Mos6510OpCode.DCP_ZeroPage:
                ZeroPage_Fast();
                DCP_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.DCP_ZeroPageX:
                ZeroPageX_Fast();
                DCP_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.DCP_Absolute:
                Absolute_Fast();
                DCP_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.DCP_AbsoluteX:
                AbsoluteX_Fast();
                DCP_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.DCP_AbsoluteY:
                AbsoluteY_Fast();
                DCP_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.DCP_IndirectX:
                IndirectX_Fast();
                DCP_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.DCP_IndirectY:
                IndirectY_Fast();
                DCP_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - ISC
            case Mos6510OpCode.ISC_ZeroPage:
                ZeroPage_Fast();
                ISC_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.ISC_ZeroPageX:
                ZeroPageX_Fast();
                ISC_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.ISC_Absolute:
                Absolute_Fast();
                ISC_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.ISC_AbsoluteX:
                AbsoluteX_Fast();
                ISC_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.ISC_AbsoluteY:
                AbsoluteY_Fast();
                ISC_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.ISC_IndirectX:
                IndirectX_Fast();
                ISC_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.ISC_IndirectY:
                IndirectY_Fast();
                ISC_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - JAM
            case Mos6510OpCode.JAM_Implied:
            case Mos6510OpCode.JAM_12_Implied:
            case Mos6510OpCode.JAM_22_Implied:
            case Mos6510OpCode.JAM_32_Implied:
            case Mos6510OpCode.JAM_42_Implied:
            case Mos6510OpCode.JAM_52_Implied:
            case Mos6510OpCode.JAM_62_Implied:
            case Mos6510OpCode.JAM_72_Implied:
            case Mos6510OpCode.JAM_92_Implied:
            case Mos6510OpCode.JAM_B2_Implied:
            case Mos6510OpCode.JAM_D2_Implied:
            case Mos6510OpCode.JAM_F2_Implied:
                JAM_Fast();
                InstructionCycles = 2; // JAM continues indefinitely but we set a minimal cycle count
                break;

            // Undocumented opcodes - LAS
            case Mos6510OpCode.LAS_AbsoluteY:
                AbsoluteY_Fast();
                LAS_Fast();
                InstructionCycles = _pageCrossed ? 5u : 4u;
                break;

            // Undocumented opcodes - LAX
            case Mos6510OpCode.LAX_ZeroPage:
                ZeroPage_Fast();
                LAX_Fast();
                InstructionCycles = 3;
                break;
            case Mos6510OpCode.LAX_ZeroPageY:
                ZeroPageY_Fast();
                LAX_Fast();
                InstructionCycles = 4;
                break;
            case Mos6510OpCode.LAX_Absolute:
                Absolute_Fast();
                LAX_Fast();
                InstructionCycles = 4;
                break;
            case Mos6510OpCode.LAX_AbsoluteY:
                AbsoluteY_Fast();
                LAX_Fast();
                InstructionCycles = _pageCrossed ? 5u : 4u;
                break;
            case Mos6510OpCode.LAX_IndirectX:
                IndirectX_Fast();
                LAX_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.LAX_IndirectY:
                IndirectY_Fast();
                LAX_Fast();
                InstructionCycles = _pageCrossed ? 6u : 5u;
                break;

            // Undocumented opcodes - LXA
            case Mos6510OpCode.LXA_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                LXA_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - NOP
            case Mos6510OpCode.NOP_Absolute:
                Absolute_Fast();
                NOP_Fast();
                InstructionCycles = 4;
                break;
            case Mos6510OpCode.NOP_AbsoluteX:
            case Mos6510OpCode.NOP_3C_AbsoluteX:
            case Mos6510OpCode.NOP_5C_AbsoluteX:
            case Mos6510OpCode.NOP_7C_AbsoluteX:
            case Mos6510OpCode.NOP_DC_AbsoluteX:
            case Mos6510OpCode.NOP_FC_AbsoluteX:
                AbsoluteX_Fast();
                NOP_Fast();
                InstructionCycles = _pageCrossed ? 5u : 4u;
                break;
            case Mos6510OpCode.NOP_Immediate:
            case Mos6510OpCode.NOP_82_Immediate:
            case Mos6510OpCode.NOP_89_Immediate:
            case Mos6510OpCode.NOP_C2_Immediate:
            case Mos6510OpCode.NOP_E2_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                NOP_Fast();
                InstructionCycles = 2;
                break;
            case Mos6510OpCode.NOP_1A_Implied:
            case Mos6510OpCode.NOP_3A_Implied:
            case Mos6510OpCode.NOP_5A_Implied:
            case Mos6510OpCode.NOP_7A_Implied:
            case Mos6510OpCode.NOP_DA_Implied:
            case Mos6510OpCode.NOP_FA_Implied:
                NOP_Fast();
                InstructionCycles = 2;
                break;
            case Mos6510OpCode.NOP_ZeroPage:
            case Mos6510OpCode.NOP_44_ZeroPage:
            case Mos6510OpCode.NOP_64_ZeroPage:
                ZeroPage_Fast();
                NOP_Fast();
                InstructionCycles = 3;
                break;
            case Mos6510OpCode.NOP_ZeroPageX:
            case Mos6510OpCode.NOP_34_ZeroPageX:
            case Mos6510OpCode.NOP_54_ZeroPageX:
            case Mos6510OpCode.NOP_74_ZeroPageX:
            case Mos6510OpCode.NOP_D4_ZeroPageX:
            case Mos6510OpCode.NOP_F4_ZeroPageX:
                ZeroPageX_Fast();
                NOP_Fast();
                InstructionCycles = 4;
                break;

            // Undocumented opcodes - RLA
            case Mos6510OpCode.RLA_ZeroPage:
                ZeroPage_Fast();
                RLA_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.RLA_ZeroPageX:
                ZeroPageX_Fast();
                RLA_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.RLA_Absolute:
                Absolute_Fast();
                RLA_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.RLA_AbsoluteX:
                AbsoluteX_Fast();
                RLA_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.RLA_AbsoluteY:
                AbsoluteY_Fast();
                RLA_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.RLA_IndirectX:
                IndirectX_Fast();
                RLA_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.RLA_IndirectY:
                IndirectY_Fast();
                RLA_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - RRA
            case Mos6510OpCode.RRA_ZeroPage:
                ZeroPage_Fast();
                RRA_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.RRA_ZeroPageX:
                ZeroPageX_Fast();
                RRA_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.RRA_Absolute:
                Absolute_Fast();
                RRA_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.RRA_AbsoluteX:
                AbsoluteX_Fast();
                RRA_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.RRA_AbsoluteY:
                AbsoluteY_Fast();
                RRA_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.RRA_IndirectX:
                IndirectX_Fast();
                RRA_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.RRA_IndirectY:
                IndirectY_Fast();
                RRA_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - SAX
            case Mos6510OpCode.SAX_ZeroPage:
                ZeroPage_Fast();
                SAX_Fast();
                InstructionCycles = 3;
                break;
            case Mos6510OpCode.SAX_ZeroPageY:
                ZeroPageY_Fast();
                SAX_Fast();
                InstructionCycles = 4;
                break;
            case Mos6510OpCode.SAX_Absolute:
                Absolute_Fast();
                SAX_Fast();
                InstructionCycles = 4;
                break;
            case Mos6510OpCode.SAX_IndirectX:
                IndirectX_Fast();
                SAX_Fast();
                InstructionCycles = 6;
                break;

            // Undocumented opcodes - SBX
            case Mos6510OpCode.SBX_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                SBX_Fast();
                InstructionCycles = 2;
                break;

            // Undocumented opcodes - SHA
            case Mos6510OpCode.SHA_AbsoluteY:
                AbsoluteY_Fast();
                SHA_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.SHA_IndirectY:
                IndirectY_Fast();
                SHA_Fast();
                InstructionCycles = 6;
                break;

            // Undocumented opcodes - SHX
            case Mos6510OpCode.SHX_AbsoluteY:
                AbsoluteY_Fast();
                SHX_Fast();
                InstructionCycles = 5;
                break;

            // Undocumented opcodes - SHY
            case Mos6510OpCode.SHY_AbsoluteX:
                AbsoluteX_Fast();
                SHY_Fast();
                InstructionCycles = 5;
                break;

            // Undocumented opcodes - SLO
            case Mos6510OpCode.SLO_ZeroPage:
                ZeroPage_Fast();
                SLO_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.SLO_ZeroPageX:
                ZeroPageX_Fast();
                SLO_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.SLO_Absolute:
                Absolute_Fast();
                SLO_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.SLO_AbsoluteX:
                AbsoluteX_Fast();
                SLO_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.SLO_AbsoluteY:
                AbsoluteY_Fast();
                SLO_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.SLO_IndirectX:
                IndirectX_Fast();
                SLO_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.SLO_IndirectY:
                IndirectY_Fast();
                SLO_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - SRE
            case Mos6510OpCode.SRE_ZeroPage:
                ZeroPage_Fast();
                SRE_Fast();
                InstructionCycles = 5;
                break;
            case Mos6510OpCode.SRE_ZeroPageX:
                ZeroPageX_Fast();
                SRE_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.SRE_Absolute:
                Absolute_Fast();
                SRE_Fast();
                InstructionCycles = 6;
                break;
            case Mos6510OpCode.SRE_AbsoluteX:
                AbsoluteX_Fast();
                SRE_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.SRE_AbsoluteY:
                AbsoluteY_Fast();
                SRE_Fast();
                InstructionCycles = 7;
                break;
            case Mos6510OpCode.SRE_IndirectX:
                IndirectX_Fast();
                SRE_Fast();
                InstructionCycles = 8;
                break;
            case Mos6510OpCode.SRE_IndirectY:
                IndirectY_Fast();
                SRE_Fast();
                InstructionCycles = 8;
                break;

            // Undocumented opcodes - TAS
            case Mos6510OpCode.TAS_AbsoluteY:
                AbsoluteY_Fast();
                TAS_Fast();
                InstructionCycles = 5;
                break;

            // Undocumented opcodes - USBC
            case Mos6510OpCode.USBC_Immediate:
                _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.OperandImmediate;
                PC++;
                SBC_Fast();
                InstructionCycles = 2;
                break;

            // All standard 6502 opcodes - delegate to base class
            default:
                base.InternalFastStep();
                break;
        }
    }

    private protected override void Exec()
    {
        switch ((Mos6510Mnemonic)_mnemonic)
        {
            case Mos6510Mnemonic.ALR:
                ALR();
                break;
            case Mos6510Mnemonic.ANC:
                ANC();
                break;
            case Mos6510Mnemonic.ANE:
                ANE();
                break;
            case Mos6510Mnemonic.ARR:
                ARR();
                break;
            case Mos6510Mnemonic.DCP:
                DCP();
                break;
            case Mos6510Mnemonic.ISC:
                ISC();
                break;
            case Mos6510Mnemonic.JAM:
                JAM();
                break;
            case Mos6510Mnemonic.LAS:
                LAS();
                break;
            case Mos6510Mnemonic.LAX:
                LAX();
                break;
            case Mos6510Mnemonic.LXA:
                LXA();
                break;
            case Mos6510Mnemonic.RLA:
                RLA();
                break;
            case Mos6510Mnemonic.RRA:
                RRA();
                break;
            case Mos6510Mnemonic.SAX:
                SAX();
                break;
            case Mos6510Mnemonic.SBX:
                SBX();
                break;
            case Mos6510Mnemonic.SHA:
                SHA();
                break;
            case Mos6510Mnemonic.SHX:
                SHX();
                break;
            case Mos6510Mnemonic.SHY:
                SHY();
                break;
            case Mos6510Mnemonic.SLO:
                SLO();
                break;
            case Mos6510Mnemonic.SRE:
                SRE();
                break;
            case Mos6510Mnemonic.TAS:
                TAS();
                break;
            case Mos6510Mnemonic.USBC:
                SBC();
                break;
            default:
                base.Exec();
                break;
        }
    }

    private protected override void JAM()
    {
        _jammed = true;
        switch (_cycleCount)
        {
            case 0:
                Read(PC, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
            case 3:
                Read(0xFFFE, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            default:
                Read(0xFFFF, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
        }

        // Special case for JAM, we update the CPU cycle on the go unlike the other instructions
        InstructionCycles = (uint)2 + _cycleCount;
    }

    private protected void JAM_Fast()
    {
        _jammed = true;
    }

    private void SLO()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite); // We need to write the original value first (according to the tests)
                SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                _wv0 = (byte)(_wv0 << 1);
                A = (byte)(_wv0 | A);
                UpdateNZFlag(A);
                break;
            default:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void SLO_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
        _wv0 = (byte)(_wv0 << 1);
        A = (byte)(_wv0 | A);
        UpdateNZFlag(A);
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void ANC()
    {
        AND();
        SetFlag(Mos6502CpuFlags.C, GetFlag(Mos6502CpuFlags.N));
    }

    private void ANC_Fast()
    {
        AND_Fast();
        SetFlag(Mos6502CpuFlags.C, GetFlag(Mos6502CpuFlags.N));
    }

    private void RLA()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite); // We need to write the original value first (according to the tests)
                bool tmp = GetFlag(Mos6502CpuFlags.C);
                SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                _wv0 = (byte)(_wv0 << 1);
                if (tmp) _wv0 |= 1;
                A = (byte)(_wv0 & A);
                UpdateNZFlag(A);
                break;
            case 3:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void RLA_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        bool tmp = GetFlag(Mos6502CpuFlags.C);
        SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
        _wv0 = (byte)(_wv0 << 1);
        if (tmp) _wv0 |= 1;
        A = (byte)(_wv0 & A);
        UpdateNZFlag(A);
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void SRE()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite); // We need to write the original value first (according to the tests)
                SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                _wv0 = (byte)(_wv0 >> 1);
                A = (byte)(_wv0 ^ A);
                UpdateNZFlag(A);
                break;
            default:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void SRE_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
        _wv0 = (byte)(_wv0 >> 1);
        A = (byte)(_wv0 ^ A);
        UpdateNZFlag(A);
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void ALR()
    {
        AND();
        _addressingMode = Mos6502AddressingMode.Implied;

        SetFlag(Mos6502CpuFlags.C, (A & 1) != 0);
        A = (byte)(A >> 1);
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ALR_Fast()
    {
        AND_Fast();
        SetFlag(Mos6502CpuFlags.C, (A & 1) != 0);
        A = (byte)(A >> 1);
        UpdateNZFlag(A);
    }

    private void RRA()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite); // We need to write the original value first (according to the tests)
                bool tmp = GetFlag(Mos6502CpuFlags.C);
                SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                _wv0 = (byte)(_wv0 >> 1);
                if (tmp) _wv0 |= 0x80;
                break;
            case 3:
            {
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                ADC(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
            }
        }
    }

    private void RRA_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        bool tmp = GetFlag(Mos6502CpuFlags.C);
        SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
        _wv0 = (byte)(_wv0 >> 1);
        if (tmp) _wv0 |= 0x80;
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
        ADC(_wv0);
    }

    private void ARR()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A &= p;

        if (GetFlag(Mos6502CpuFlags.D))
        {
            var r = (byte)(A >> 1);
            if (GetFlag(Mos6502CpuFlags.C)) r |= 0x80;

            SetFlag(Mos6502CpuFlags.N, GetFlag(Mos6502CpuFlags.C));
            SetFlag(Mos6502CpuFlags.Z, r == 0);
            SetFlag(Mos6502CpuFlags.V, ((r ^ A) & 0x40) != 0);

            if (((A & 0xf) + (A & 0x1)) > 0x5)
            {                
                r = (byte)((r & 0xf0) | ((r + 0x6) & 0xf));
            }
            
            if (((A & 0xf0) + (A & 0x10)) > 0x50)
            {
                r = (byte)((r & 0x0f) | ((r + 0x60) & 0xf0));
                SetFlag(Mos6502CpuFlags.C, true);        
            }
            else
            {
                SetFlag(Mos6502CpuFlags.C, false);
            }
            A = r;
        }
        else
        {
            A = (byte)(A >> 1);
            if (GetFlag(Mos6502CpuFlags.C)) A |= 0x80;

            UpdateNZFlag(A);
            SetFlag(Mos6502CpuFlags.C, (A & 0x40) != 0);
            SetFlag(Mos6502CpuFlags.V, ((A & 0x40) ^ ((A & 0x20) << 1)) != 0);
        }

        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ARR_Fast()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A &= p;

        if (GetFlag(Mos6502CpuFlags.D))
        {
            var r = (byte)(A >> 1);
            if (GetFlag(Mos6502CpuFlags.C)) r |= 0x80;

            SetFlag(Mos6502CpuFlags.N, GetFlag(Mos6502CpuFlags.C));
            SetFlag(Mos6502CpuFlags.Z, r == 0);
            SetFlag(Mos6502CpuFlags.V, ((r ^ A) & 0x40) != 0);

            if (((A & 0xf) + (A & 0x1)) > 0x5)
            {                
                r = (byte)((r & 0xf0) | ((r + 0x6) & 0xf));
            }
            
            if (((A & 0xf0) + (A & 0x10)) > 0x50)
            {
                r = (byte)((r & 0x0f) | ((r + 0x60) & 0xf0));
                SetFlag(Mos6502CpuFlags.C, true);        
            }
            else
            {
                SetFlag(Mos6502CpuFlags.C, false);
            }
            A = r;
        }
        else
        {
            A = (byte)(A >> 1);
            if (GetFlag(Mos6502CpuFlags.C)) A |= 0x80;

            UpdateNZFlag(A);
            SetFlag(Mos6502CpuFlags.C, (A & 0x40) != 0);
            SetFlag(Mos6502CpuFlags.V, ((A & 0x40) ^ ((A & 0x20) << 1)) != 0);
        }
    }

    private void SHY()
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);

            if (_pageCrossed)
            {
                _wv0 = (byte)(Y & _wv1);
                _av = (ushort)((_av & 0xFF) | (_wv0 << 8));
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(Y & (_wv1 + 1));
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            }
        }
        else
        {
            Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void SHY_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);

        if (_pageCrossed)
        {
            _wv0 = (byte)(Y & _wv1);
            _av = (ushort)((_av & 0xFF) | (_wv0 << 8));
        }
        else
        {
            _wv0 = (byte)(Y & (_wv1 + 1));
        }
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void SHX()
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);

            if (_pageCrossed)
            {
                _wv0 = (byte)(X & _wv1);
                _av = (ushort)((_av & 0xFF) | (_wv0 << 8));
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(X & (_wv1 + 1));
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            }
        }
        else
        {
            Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void SHX_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);

        if (_pageCrossed)
        {
            _wv0 = (byte)(X & _wv1);
            _av = (ushort)((_av & 0xFF) | (_wv0 << 8));
        }
        else
        {
            _wv0 = (byte)(X & (_wv1 + 1));
        }
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void SAX()
    {
        byte p = (byte)(A & X);
        ST(ref p);
    }

    private void SAX_Fast()
    {
        byte p = (byte)(A & X);
        ST_Fast(ref p);
    }

    private void SBX()
    {
        var p = Read(_av, _pendingExecuteReadKind);
        X = (byte)(A & X);
        p = (byte)(X - p);
        UpdateNZFlag(p);
        SetFlag(Mos6502CpuFlags.C, X >= p);
        X = p;
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void SBX_Fast()
    {
        var p = Read(_av, _pendingExecuteReadKind);
        X = (byte)(A & X);
        p = (byte)(X - p);
        UpdateNZFlag(p);
        SetFlag(Mos6502CpuFlags.C, X >= p);
        X = p;
    }

    private void ANE()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A = (byte)((A | 0xee) & X & p);
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ANE_Fast()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A = (byte)((A | 0xee) & X & p);
        UpdateNZFlag(A);
    }

    private void SHA()
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);
            if (_pageCrossed)
            {
                _wv0 = (byte)(A & X & _wv1); // this one is not documented but matching tests
                Write((ushort)((_av & 0xFF) | (_wv0 << 8)), _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(A & X & (_wv1 + 1));
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            }
        }
        else
        {
            Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void SHA_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        if (_pageCrossed)
        {
            _wv0 = (byte)(A & X & _wv1);
            Write((ushort)((_av & 0xFF) | (_wv0 << 8)), _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
        }
        else
        {
            _wv0 = (byte)(A & X & (_wv1 + 1));
            Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
        }
    }

    private void TAS()
    {
        bool lastStep = false;
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);
            S = (byte)(A & X);
            if (_pageCrossed)
            {
                lastStep = true;
            }
            else
            {
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            }
        }
        else
        {
            lastStep = true;
        }


        if (lastStep)
        {
            var d = (byte)(S & (_wv1 + 1));
            if (_pageCrossed)
            {
                d = (byte)(S & _wv1);
                _av = (ushort)((_av & 0xFF) | (d << 8));
            }

            Write(_av, d, Mos6502MemoryBusAccessKind.ExecuteWrite);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void TAS_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        S = (byte)(A & X);
        
        var d = (byte)(S & (_wv1 + 1));
        if (_pageCrossed)
        {
            d = (byte)(S & _wv1);
            _av = (ushort)((_av & 0xFF) | (d << 8));
        }

        Write(_av, d, Mos6502MemoryBusAccessKind.ExecuteWrite);
    }

    private void LAX()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteRead);
        A = p;
        X = p;
        UpdateNZFlag(p);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LAX_Fast()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteRead);
        A = p;
        X = p;
        UpdateNZFlag(p);
    }

    private void LXA()
    {
        // (A OR CONST) AND oper -> A -> X
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A = (byte)((A | 0xee) & p); // 0xee unstable bits
        X = A;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LXA_Fast()
    {
        // (A OR CONST) AND oper -> A -> X
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A = (byte)((A | 0xee) & p); // 0xee unstable bits
        X = A;
        UpdateNZFlag(A);
    }

    private void LAS()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteRead);
        A = (byte)(p & S);
        X = A;
        S = A;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LAS_Fast()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteRead);
        A = (byte)(p & S);
        X = A;
        S = A;
        UpdateNZFlag(A);
    }

    private void DCP()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite); // dummy write for the bus cycle
                _wv0--;
                UpdateNZFlag(_wv0);
                break;
            case 3:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);

                var result = (byte)(A - _wv0);
                UpdateNZFlag(result);
                SetFlag(Mos6502CpuFlags.C, A >= result);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void DCP_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        _wv0--;
        UpdateNZFlag(_wv0);
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);

        var result = (byte)(A - _wv0);
        UpdateNZFlag(result);
        SetFlag(Mos6502CpuFlags.C, A >= result);
    }

    private void ISC()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                _wv0++;
                break;
            case 3:
            {
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                SBC(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
            }
        }
    }

    private void ISC_Fast()
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
        _wv0++;
        Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
        SBC(_wv0);
    }
}