// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// The code in this file was originally written by Jacob Paul at https://github.com/ericssonpaul/O2
// 
// It has been heavily modified to:
// - Integrate it better with Asm6502 and C#
// - Rely on Asm6502 builtin decoding logic
// - Fix issues with regular opcode (BCD mode)
// - Add support for all illegal opcodes in Mos6510Cpu
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

#pragma warning disable CS1591

namespace Asm6502;

/// <summary>
/// Represents a MOS 6510 CPU, providing full emulation of all documented and undocumented (illegal) instructions
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
        // Mos6510 covers all Mos6502 opcodes including illegal ones
        var opcode = (Mos6510OpCode)opcode6502;
        addressingMode = opcode.ToAddressingMode();
        mnemonic = (Mos6502Mnemonic)opcode.ToMnemonic();
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
                Read(PC);
                break;
            case 2:
            case 3:
                Read(0xFFFE);
                break;
            default:
                Read(0xFFFF);
                break;
        }

        // Special case for JAM, we update the CPU cycle on the go unlike the other instructions
        InstructionCycles = (uint)2 + _cycleCount;
    }

    private void SLO()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0); // We need to write the original value first (according to the tests)
                SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                _wv0 = (byte)(_wv0 << 1);
                A = (byte)(_wv0 | A);
                UpdateNZFlag(A);
                break;
            default:
                Write(_av, _wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void ANC()
    {
        AND();
        SetFlag(Mos6502CpuFlags.C, GetFlag(Mos6502CpuFlags.N));
    }

    private void RLA()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0); // We need to write the original value first (according to the tests)
                bool tmp = GetFlag(Mos6502CpuFlags.C);
                SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                _wv0 = (byte)(_wv0 << 1);
                if (tmp) _wv0 |= 1;
                A = (byte)(_wv0 & A);
                UpdateNZFlag(A);
                break;
            case 3:
                Write(_av, _wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void SRE()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0); // We need to write the original value first (according to the tests)
                SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                _wv0 = (byte)(_wv0 >> 1);
                A = (byte)(_wv0 ^ A);
                UpdateNZFlag(A);
                break;
            default:
                Write(_av, _wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
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

    private void RRA()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0); // We need to write the original value first (according to the tests)
                bool tmp = GetFlag(Mos6502CpuFlags.C);
                SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                _wv0 = (byte)(_wv0 >> 1);
                if (tmp) _wv0 |= 0x80;
                break;
            case 3:
            {
                Write(_av, _wv0);
                ADC(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
            }
        }
    }

    private void ARR()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0));
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

    private void SHY()
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);

            if (_pageCrossed)
            {
                _wv0 = (byte)(Y & _wv1);
                _av = (ushort)((_av & 0xFF) | (_wv0 << 8));
                Write(_av, _wv0);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(Y & (_wv1 + 1));
                Read(_av);
            }
        }
        else
        {
            Write(_av, _wv0);
            _runState = Mos6502CpuRunState.Fetch;
        }
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
                Write(_av, _wv0);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(X & (_wv1 + 1));
                Read(_av);
            }
        }
        else
        {
            Write(_av, _wv0);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void SAX()
    {
        byte p = (byte)(A & X);
        ST(ref p);
    }

    private void SBX()
    {
        var p = Read(_av);
        X = (byte)(A & X);
        p = (byte)(X - p);
        UpdateNZFlag(p);
        SetFlag(Mos6502CpuFlags.C, X >= p);
        X = p;
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ANE()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0));
        A = (byte)((A | 0xee) & X & p);
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void SHA()
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);
            if (_pageCrossed)
            {
                _wv0 = (byte)(A & X & _wv1); // this one is not documented but matching tests
                Write((ushort)((_av & 0xFF) | (_wv0 << 8)), _wv0);
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _wv0 = (byte)(A & X & (_wv1 + 1));
                Read(_av);
            }
        }
        else
        {
            Write(_av, _wv0);
            _runState = Mos6502CpuRunState.Fetch;
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
                Read(_av);
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

            Write(_av, d);
            //UpdateNZFlag(A);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void LAX()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0));
        A = p;
        X = p;
        UpdateNZFlag(p);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LXA()
    {
        // (A OR CONST) AND oper -> A -> X
        byte p = Read((ushort)((_wv1 << 8) | _wv0));
        A = (byte)((A | 0xee) & p); // 0xee unstable bits
        X = A;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LAS()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0));
        A = (byte)(p & S);
        X = A;
        S = A;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void DCP()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0); // dummy write for the bus cycle
                _wv0--;
                UpdateNZFlag(_wv0);
                break;
            case 3:
                Write(_av, _wv0);

                var result = (byte)(A - _wv0);
                UpdateNZFlag(result);
                SetFlag(Mos6502CpuFlags.C, A >= result);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void ISC()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av);
                if (_addressingMode == Mos6502AddressingMode.Absolute ||
                    _addressingMode == Mos6502AddressingMode.ZeroPage ||
                    _addressingMode == Mos6502AddressingMode.ZeroPageX ||
                    _addressingMode == Mos6502AddressingMode.Indirect ||
                    _addressingMode == Mos6502AddressingMode.IndirectX ||
                    _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av);
                break;
            case 2:
                Write(_av, _wv0);
                _wv0++;
                break;
            case 3:
            {
                Write(_av, _wv0);
                SBC(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
            }
        }
    }
}