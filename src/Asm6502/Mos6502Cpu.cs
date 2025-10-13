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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Asm6502;

/// <summary>
/// Emulates a MOS 6502 CPU with accurate cycle timing, addressing modes, interrupts, and legal opcodes.
/// Exposes a pluggable memory bus and utilities for stepping by cycle or instruction.
/// </summary>
/// <remarks>
/// This class provides a base class for decoding MOS 6502 CPU variants.
///
/// For a full support of the original 6502 with undocumented opcodes, consider using <see cref="Mos6510Cpu"/>.
/// </remarks>
public class Mos6502Cpu
{
    // Memory bus
    private IMos6502CpuMemoryBus? _bus;

    // Internal CPU state machine
    private protected Mos6502CpuRunState _runState;
    private bool _cold;
    private protected Mos6502AddressingMode _addressingMode;
    private InterruptFlags _interruptFlags;
    private bool _raisedNmi;
    private bool _raisedIrq;
    private bool _raisedBrk;
    private bool _runningBrk;
    private bool _runningInterrupt;
    private protected bool _jammed;
    private bool _halted;
    private protected byte _cycleCount;
    private int _cycleExecCorrection;
    private protected Mos6502OpCode _opcode;
    private protected Mos6502Mnemonic _mnemonic;

    private protected Mos6502MemoryBusAccessKind _pendingExecuteReadKind;
    // This is the PC value at the memory location the opcode is fetched
    private ushort _PCAtOpcode;
    // working values during instruction execution
    private protected byte _wv0;
    private protected byte _wv1;
    // address value during instruction execution
    private protected ushort _av;
    // page crossed during address calculation
    private protected bool _pageCrossed;

    /// <summary>
    /// Address of the Non-Maskable Interrupt (NMI) vector.
    /// </summary>
    public const ushort CpuVectorNmi = 0xFFFA;

    /// <summary>
    /// Address of the Reset vector.
    /// </summary>
    public const ushort CpuVectorReset = 0xFFFC;

    /// <summary>
    /// Address of the Interrupt Request (IRQ) vector.
    /// </summary>
    public const ushort CpuVectorIrq = 0xFFFE;

    /// <summary>
    /// Default value returned when no memory bus is attached. Value is NOP (0xEA).
    /// </summary>
    public const byte CpuDefaultReadValue = 0xEA; // NOP

    // Construction

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Cpu"/> class with no memory bus attached.
    /// </summary>
    public Mos6502Cpu()
    {
        StateInit();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mos6502Cpu"/> class with the specified memory bus.
    /// </summary>
    /// <param name="bus">The memory bus used by the CPU to read from and write to memory.</param>
    public Mos6502Cpu(IMos6502CpuMemoryBus bus)
    {
        _bus = bus;
        StateInit();
    }

    /// <summary>
    /// Gets or sets the Program Counter (PC).
    /// </summary>
    public ushort PC { get; set; }

    /// <summary>
    /// Gets the value of the program counter at the current opcode.
    /// </summary>
    public ushort PCAtOpcode => _PCAtOpcode;

    /// <summary>
    /// Accumulator register (A).
    /// </summary>
    public byte A;

    /// <summary>
    /// Index register X.
    /// </summary>
    public byte X;

    /// <summary>
    /// Index register Y.
    /// </summary>
    public byte Y;

    /// <summary>
    /// Gets or sets the Stack Pointer (S).
    /// </summary>
    public byte S { get; set; }

    /// <summary>
    /// Gets or sets the Status Register (SR) containing CPU flags.
    /// </summary>
    public Mos6502CpuFlags SR { get; set; }

    /// <summary>
    /// Gets or sets the CPU registers as a single record value.
    /// </summary>
    public Mos6502CpuRegisters Registers
    {
        get => new(PC, A, X, Y, S, SR);
        set
        {
            PC = value.PC;
            A = value.A;
            X = value.X;
            Y = value.Y;
            S = value.S;
            SR = value.SR;
        }
    }

    /// <summary>
    /// Gets the current CPU run state.
    /// </summary>
    public Mos6502CpuRunState RunState => _runState;

    /// <summary>
    /// Gets the current opcode being executed.
    /// </summary>
    /// <remarks>
    /// Only valid during <see cref="Mos6502CpuRunState.Load"/> or <see cref="Mos6502CpuRunState.Execute"/>.
    /// It requires calling <see cref="Cycle"/> (or methods that call it) to keep its value updated.
    /// </remarks>
    public Mos6502OpCode CurrentOpCode => _opcode;

    /// <summary>
    /// Gets the number of cycles consumed by the last completed instruction.
    /// </summary>
    public uint InstructionCycles { get; private protected set; }

    /// <summary>
    /// Gets the monotonically increasing cycle counter, incremented at each CPU cycle.
    /// </summary>
    public ulong TimestampCounter { get; private set; }

    /// <summary>
    /// Gets or sets the memory bus used by the CPU to access memory.
    /// </summary>
    public IMos6502CpuMemoryBus? MemoryBus
    {
        get => _bus;
        set => _bus = value;
    }

    /// <summary>
    /// Gets a value indicating whether the CPU is halted.
    /// </summary>
    public bool IsHalted => _halted;

    /// <summary>
    /// Gets a value indicating whether the CPU is jammed by an undocumented opcode (JAM/KIL/HLT).
    /// </summary>
    public bool IsJammed => _jammed;

    /// <summary>
    /// Executes a single CPU cycle, advancing the internal state machine by one cycle.
    /// </summary>
    public void Cycle()
    {
        if (_halted) return;

        TimestampCounter++;

        if (_jammed && !_runningInterrupt && _interruptFlags != InterruptFlags.Reset)
        {
            JAM();
            _cycleCount++;
            return;
        }

        if (_runningInterrupt)
        {
            HandleInterrupt();
            if (!_runningInterrupt)
            {
                InstructionCycles = (uint)(_cycleCount + _cycleExecCorrection);
                _cycleCount = 0;
                switch (_interruptFlags)
                {
                    case InterruptFlags.Nmi: _raisedNmi = false; break;
                    case InterruptFlags.Irq: _raisedIrq = false; break;
                }
                _interruptFlags = InterruptFlags.None;
                _runState = Mos6502CpuRunState.Fetch;
            }
            else
            {
                _cycleCount++;
            }
            return;
        }

        switch (_runState)
        {
            case Mos6502CpuRunState.Fetch:
                if (_raisedNmi)
                {
                    _interruptFlags = InterruptFlags.Nmi;
                    _runningInterrupt = true;
                    HandleInterrupt();
                    if (!_runningInterrupt)
                    {
                        InstructionCycles = _cycleCount;
                        _cycleExecCorrection = 0;
                        _cycleCount = 0;
                        _interruptFlags = InterruptFlags.None;
                        _runState = Mos6502CpuRunState.Fetch;
                    }
                    else
                    {
                        _cycleCount++;
                    }
                    return;
                }
                else if (_raisedIrq && !GetFlag(Mos6502CpuFlags.I))
                {
                    _interruptFlags = InterruptFlags.Irq;
                    _runningInterrupt = true;
                    HandleInterrupt();
                    if (!_runningInterrupt)
                    {
                        InstructionCycles = _cycleCount;
                        _cycleExecCorrection = 0;
                        _cycleCount = 0;
                        _interruptFlags = InterruptFlags.None;
                        _runState = Mos6502CpuRunState.Fetch;
                    }
                    else
                    {
                        _cycleCount++;
                    }
                    return;
                }

                InstructionCycles = 1;
                _cycleExecCorrection = 0;
                Fetch();
                return;

            case Mos6502CpuRunState.Load:
                Load();
                if (_runState != Mos6502CpuRunState.Load)
                {
                    InstructionCycles += (uint)(_cycleCount + 1);
                    _cycleExecCorrection = 0;
                    _cycleCount = 0;
                }
                else
                {
                    _cycleCount++;
                }
                return;

            case Mos6502CpuRunState.Execute:
                var previousCycle = _cycleCount;
                Exec();
                if (previousCycle != _cycleCount)
                {
                    Debug.Assert(_cycleCount == previousCycle + 1);
                    Debug.Assert(_cycleExecCorrection == 0);
                    // BRK adds an extra cycle (+1)
                    // For -1 it happens when the execution of an instruction increments the _cycleCount++
                    // so we need to counter correct it here
                    // The increment happen when a page crossing or some addressing modes occur. In these cases,
                    // the memory read is done already during the load phase and the extra dummy read cycle in the
                    // execute phase is not needed.
                    _cycleExecCorrection = _runningBrk ? 1 : -1;
                }
                if (_runState != Mos6502CpuRunState.Execute)
                {
                    InstructionCycles += (uint)(_cycleCount + 1 + _cycleExecCorrection);
                    _cycleExecCorrection = 0;
                    _cycleCount = 0;
                }
                else
                {
                    _cycleCount++;
                }
                return;
        }
    }

    /// <summary>
    /// Executes the specified number of CPU cycles.
    /// </summary>
    /// <param name="count">The number of cycles to execute.</param>
    public void Cycles(uint count)
    {
        while (count-- > 0) Cycle();
    }

    /// <summary>
    /// Steps the CPU until the next instruction is fetched, allowing the current instruction to complete.
    /// </summary>
    public void Step()
    {
        do
        {
            Cycle();
        } while (_runState != Mos6502CpuRunState.Fetch || _runningInterrupt);
    }

    /// <summary>
    /// Steps the CPU until the next instruction is fetched, allowing the current instruction to complete.
    /// </summary>
    /// <param name="onCycle">A callback function called on each cycle. If it returns false, stepping is aborted.</param>
    public void Step(Func<bool> onCycle)
    {
        do
        {
            Cycle();
            if (!onCycle())
            {
                break;
            }
        } while (_runState != Mos6502CpuRunState.Fetch || _runningInterrupt);
    }

    /// <summary>
    /// Executes the specified number of instructions.
    /// </summary>
    /// <param name="count">The number of instructions to execute.</param>
    public void Steps(uint count)
    {
        while (count-- > 0) Step();
    }

    /// <summary>
    /// Runs the CPU in a loop, executing instructions as fast as possible or according to the specified speed.
    /// </summary>
    /// <param name="speed">The desired speed in Hz. If 0, runs as fast as possible.</param>
    public void Run(uint speed)
    {
        long ns = speed != 0 ? (1_000_000_000L / speed) : 0;
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var start = sw.ElapsedTicks;
            Cycle();
            if (speed != 0)
            {
                var elapsedNs = TicksToNanoseconds(Stopwatch.GetTimestamp() - start);
                var remaining = ns - elapsedNs;
                if (remaining > 0) Thread.Sleep(TimeSpan.FromTicks(NanosecondsToTicks(remaining)));
            }
        }
    }

    /// <summary>
    /// Runs the CPU for the specified duration in milliseconds, executing instructions as fast as possible or according to the specified speed.
    /// </summary>
    /// <param name="ms">The duration to run the CPU for, in milliseconds.</param>
    /// <param name="speed">The desired speed in Hz. If 0, runs as fast as possible.</param>
    public void Run(ulong ms, uint speed)
    {
        long ns = speed != 0 ? (1_000_000_000L / speed) : 0;
        var sw = Stopwatch.StartNew();
        var startTime = sw.Elapsed;
        while (true)
        {
            var start = sw.ElapsedTicks;
            Cycle();
            if ((ulong)sw.ElapsedMilliseconds - (ulong)startTime.TotalMilliseconds > ms) break;
            if (speed != 0)
            {
                var elapsedNs = TicksToNanoseconds(Stopwatch.GetTimestamp() - start);
                var remaining = ns - elapsedNs;
                if (remaining > 0) Thread.Sleep(TimeSpan.FromTicks(NanosecondsToTicks(remaining)));
            }
        }
    }
    
    /// <summary>
    /// Triggers a Non-Maskable Interrupt (NMI), causing an immediate interrupt request.
    /// </summary>
    public void Nmi()
    {
        Raise(InterruptFlags.Nmi);
        Step();
    }

    /// <summary>
    /// Resets the CPU, clearing the interrupt flags and setting the program counter to the reset vector.
    /// </summary>
    public void Reset()
    {
        Raise(InterruptFlags.Reset);
        Step();
    }

    /// <summary>
    /// Triggers an Interrupt Request (IRQ), causing an interrupt request on the next cycle.
    /// </summary>
    public void Irq()
    {
        Raise(InterruptFlags.Irq);
        Step();
    }

    /// <summary>
    /// Puts the CPU in a halted state, stopping normal operation until unhalted.
    /// </summary>
    public void Halt()
    {
        _halted = true; TimestampCounter = 0;
    }

    /// <summary>
    /// Releases the CPU from a halted state, resuming normal operation.
    /// </summary>
    public void UnHalt() => _halted = false;
    
    /// <summary>
    /// Raises a Non-Maskable Interrupt (NMI), causing an immediate interrupt request.
    /// </summary>
    public void RaiseNmi() => Raise(InterruptFlags.Nmi);

    /// <summary>
    /// Raises a Reset interrupt, causing the CPU to reset on the next cycle.
    /// </summary>
    public void RaiseReset() => Raise(InterruptFlags.Reset);

    /// <summary>
    /// Raises an Interrupt Request (IRQ), causing an interrupt request on the next cycle.
    /// </summary>
    public void RaiseIrq() => Raise(InterruptFlags.Irq);
    

    private void Raise(InterruptFlags f)
    {
        switch (f)
        {
            case InterruptFlags.Nmi: _raisedNmi = true; break;
            case InterruptFlags.Irq: _raisedIrq = true; break;
            case InterruptFlags.Reset:
                _interruptFlags = InterruptFlags.Reset;
                _runningInterrupt = true;
                _halted = false;
                break;
            case InterruptFlags.None:
                _raisedNmi = false;
                _raisedIrq = false;
                break;
        }
    }

    private void UnRaise(InterruptFlags f)
    {
        switch (f)
        {
            case InterruptFlags.Nmi: _raisedNmi = false; break;
            case InterruptFlags.Irq: _raisedIrq = false; break;
            case InterruptFlags.Reset:
                if (_runState != Mos6502CpuRunState.Fetch || _cycleCount == 0)
                {
                    _interruptFlags = InterruptFlags.None;
                    _runningInterrupt = false;
                }
                break;
        }
    }


    // Internal helpers/state
    private void StateInit()
    {
        SR = 0;
        A = 0;
        X = 0;
        Y = 0;
        S = 0xFD;
        _PCAtOpcode = 0;

        // SetFlag(Mos6502CpuFlags.Z, true); // TODO: Should it be zero?
        SetFlag(Mos6502CpuFlags.I, true);
        SetFlag(Mos6502CpuFlags.B, true);
        SetFlag(Mos6502CpuFlags.U, true);

        _cold = true;
        _runState = Mos6502CpuRunState.Fetch;
        _raisedNmi = false;
        _raisedIrq = false;
        _raisedBrk = false;
        _runningBrk = _raisedBrk;
        _runningInterrupt = true;
        _jammed = false;
        _interruptFlags = InterruptFlags.Reset;
        _halted = false;
        _cycleCount = 0;
        InstructionCycles = 0;
        TimestampCounter = 0;
        _opcode = 0;
        _mnemonic = 0;
    }

    private void HandleInterrupt()
    {
        if (_interruptFlags == InterruptFlags.None)
        {
            _runningInterrupt = false;
            return;
        }

        if (_jammed)
        {
            _jammed = false;
            Debug.Assert(_interruptFlags == InterruptFlags.Reset);
        }

        switch (_cycleCount)
        {
            case 0:
                if (_raisedBrk)
                {
                    _runningBrk = true;
                    _raisedBrk = false;
                    _cycleCount++;
                }
                return;
            case 1:
                return;
            case 2:
                if (_interruptFlags != InterruptFlags.Reset)
                {
                    Push((byte)(PC >> 8), Mos6502MemoryBusAccessKind.PushInterruptReturnAddressHigh);
                }
                else
                {
                    SetFlag(Mos6502CpuFlags.I, true);
                    if (!_cold)
                        S = (byte)(S - 3);
                    else
                        _cold = false;
                }
                return;
            case 3:
                if (_interruptFlags != InterruptFlags.Reset)
                    Push((byte)(PC & 0xFF), Mos6502MemoryBusAccessKind.PushInterruptReturnAddressLow);
                return;
            case 4:
                if (_interruptFlags != InterruptFlags.Reset)
                {
                    var sr = SR | Mos6502CpuFlags.U;
                    sr = _runningBrk ? (sr | Mos6502CpuFlags.B) : (sr & ~Mos6502CpuFlags.B);
                    Push((byte)sr, Mos6502MemoryBusAccessKind.PushInterruptSR);
                    SetFlag(Mos6502CpuFlags.I, true);
                    _runningBrk = false;
                }
                return;
            case 5:
                switch (_interruptFlags)
                {
                    case InterruptFlags.Nmi: PC = Read(CpuVectorNmi, Mos6502MemoryBusAccessKind.VectorInterruptLow); break;
                    case InterruptFlags.Reset: PC = Read(CpuVectorReset, Mos6502MemoryBusAccessKind.VectorInterruptLow); break;
                    case InterruptFlags.Irq: PC = Read(CpuVectorIrq, Mos6502MemoryBusAccessKind.VectorInterruptLow); break;
                }
                return;
            case 6:
                switch (_interruptFlags)
                {
                    case InterruptFlags.Nmi: PC |= (ushort)(Read(CpuVectorNmi + 1, Mos6502MemoryBusAccessKind.VectorInterruptHigh) << 8); break;
                    case InterruptFlags.Reset: PC |= (ushort)(Read(CpuVectorReset + 1, Mos6502MemoryBusAccessKind.VectorInterruptHigh) << 8); break;
                    case InterruptFlags.Irq: PC |= (ushort)(Read(CpuVectorIrq + 1, Mos6502MemoryBusAccessKind.VectorInterruptHigh) << 8); break;
                }
                _runningInterrupt = false;
                return;
        }
    }

    private void Fetch()
    {
        _PCAtOpcode = PC;
        _opcode = (Mos6502OpCode)Read(PC++, Mos6502MemoryBusAccessKind.OpCode);
        DecodeOpCode(_opcode, out _addressingMode, out _mnemonic);

        // Protection against unknown opcodes
        if (_addressingMode == Mos6502AddressingMode.Unknown)
        {
            throw new InvalidOperationException($"The opcode {_opcode:X2} at address {_PCAtOpcode:X4} is not supported by this CPU variant.");
        }

        _av = PC;
        _wv0 = (byte)(PC & 0xFF);
        _wv1 = (byte)((PC >> 8) & 0xFF);
        _pageCrossed = false;

        _pendingExecuteReadKind = Mos6502MemoryBusAccessKind.ExecuteRead;

        if (IsImmediate(_addressingMode) || IsImplied(_addressingMode))
        {
            _pendingExecuteReadKind = IsImmediate(_addressingMode) ? Mos6502MemoryBusAccessKind.OperandImmediate : Mos6502MemoryBusAccessKind.ExecuteRead;
            _runState = Mos6502CpuRunState.Execute;
            if (IsImmediate(_addressingMode)) PC++;
        }
        else
        {
            _runState = Mos6502CpuRunState.Load;
        }
    }
    
    private protected virtual void DecodeOpCode(Mos6502OpCode opcode, out Mos6502AddressingMode addressingMode, out Mos6502Mnemonic mnemonic)
    {
        addressingMode = opcode.ToAddressingMode();
        mnemonic = opcode.ToMnemonic();
    }

    private void Load()
    {
        switch (_addressingMode)
        {
            case Mos6502AddressingMode.ZeroPage: ZeroPage(); return;
            case Mos6502AddressingMode.ZeroPageX: ZeroPageX(); return;
            case Mos6502AddressingMode.ZeroPageY: ZeroPageY(); return;
            case Mos6502AddressingMode.Absolute: Absolute(); return;
            case Mos6502AddressingMode.AbsoluteX: AbsoluteX(); return;
            case Mos6502AddressingMode.AbsoluteY: AbsoluteY(); return;
            case Mos6502AddressingMode.Indirect: Indirect(); return;
            case Mos6502AddressingMode.IndirectX: IndirectX(); return;
            case Mos6502AddressingMode.IndirectY: IndirectY(); return;
            default:
                throw new InvalidOperationException($"Unexpected addressing mode {_addressingMode} for {_opcode} during Load");
        }
    }

    private static bool IsImmediate(Mos6502AddressingMode addressingMode) => addressingMode == Mos6502AddressingMode.Immediate || addressingMode == Mos6502AddressingMode.Relative;

    private static bool IsImplied(Mos6502AddressingMode addressingMode) => addressingMode == Mos6502AddressingMode.Implied || addressingMode == Mos6502AddressingMode.Accumulator || addressingMode == Mos6502AddressingMode.Relative;

    private protected virtual void Exec()
    {
        switch (_mnemonic)
        {
            case Mos6502Mnemonic.ADC:
                ADC();
                break;
            case Mos6502Mnemonic.AND:
                AND();
                break;
            case Mos6502Mnemonic.ASL:
                ASL();
                break;
            case Mos6502Mnemonic.BCC:
                BRA(Mos6502CpuFlags.C, false);
                break;
            case Mos6502Mnemonic.BCS:
                BRA(Mos6502CpuFlags.C, true);
                break;
            case Mos6502Mnemonic.BEQ:
                BRA(Mos6502CpuFlags.Z, true);
                break;
            case Mos6502Mnemonic.BIT:
                BIT();
                break;
            case Mos6502Mnemonic.BMI:
                BRA(Mos6502CpuFlags.N, true);
                break;
            case Mos6502Mnemonic.BNE:
                BRA(Mos6502CpuFlags.Z, false);
                break;
            case Mos6502Mnemonic.BPL:
                BRA(Mos6502CpuFlags.N, false);
                break;
            case Mos6502Mnemonic.BRK:
                BRK();
                break;
            case Mos6502Mnemonic.BVC:
                BRA(Mos6502CpuFlags.V, false);
                break;
            case Mos6502Mnemonic.BVS:
                BRA(Mos6502CpuFlags.V, true);
                break;
            case Mos6502Mnemonic.CLC:
                CLOrSFlag(Mos6502CpuFlags.C, false);
                break;
            case Mos6502Mnemonic.CLD:
                CLOrSFlag(Mos6502CpuFlags.D, false);
                break;
            case Mos6502Mnemonic.CLI:
                CLOrSFlag(Mos6502CpuFlags.I, false);
                break;
            case Mos6502Mnemonic.CLV:
                CLOrSFlag(Mos6502CpuFlags.V, false);
                break;
            case Mos6502Mnemonic.CMP:
                CMP(ref A);
                break;
            case Mos6502Mnemonic.CPX:
                CMP(ref X);
                break;
            case Mos6502Mnemonic.CPY:
                CMP(ref Y);
                break;
            case Mos6502Mnemonic.DEC:
                DEC();
                break;
            case Mos6502Mnemonic.DEX:
                DEC(ref X);
                break;
            case Mos6502Mnemonic.DEY:
                DEC(ref Y);
                break;
            case Mos6502Mnemonic.EOR:
                EOR();
                break;
            case Mos6502Mnemonic.INC:
                INC();
                break;
            case Mos6502Mnemonic.INX:
                INC(ref X);
                break;
            case Mos6502Mnemonic.INY:
                INC(ref Y);
                break;
            case Mos6502Mnemonic.JMP:
                JMP();
                break;
            case Mos6502Mnemonic.JSR:
                JSR();
                break;
            case Mos6502Mnemonic.LDA:
                LD(ref A);
                break;
            case Mos6502Mnemonic.LDX:
                LD(ref X);
                break;
            case Mos6502Mnemonic.LDY:
                LD(ref Y);
                break;
            case Mos6502Mnemonic.LSR:
                LSR();
                break;
            case Mos6502Mnemonic.NOP:
                NOP();
                break;
            case Mos6502Mnemonic.ORA:
                ORA();
                break;
            case Mos6502Mnemonic.PHA:
                PHA();
                break;
            case Mos6502Mnemonic.PHP:
                PHP();
                break;
            case Mos6502Mnemonic.PLA:
                PLA();
                break;
            case Mos6502Mnemonic.PLP:
                PLP();
                break;
            case Mos6502Mnemonic.ROL:
                ROL();
                break;
            case Mos6502Mnemonic.ROR:
                ROR();
                break;
            case Mos6502Mnemonic.RTI:
                RTI();
                break;
            case Mos6502Mnemonic.RTS:
                RTS();
                break;
            case Mos6502Mnemonic.SBC:
                SBC();
                break;
            case Mos6502Mnemonic.SEC:
                CLOrSFlag(Mos6502CpuFlags.C, true);
                break;
            case Mos6502Mnemonic.SED:
                CLOrSFlag(Mos6502CpuFlags.D, true);
                break;
            case Mos6502Mnemonic.SEI:
                CLOrSFlag(Mos6502CpuFlags.I, true);
                break;
            case Mos6502Mnemonic.STA:
                ST(ref A);
                break;
            case Mos6502Mnemonic.STX:
                ST(ref X);
                break;
            case Mos6502Mnemonic.STY:
                ST(ref Y);
                break;
            case Mos6502Mnemonic.TAX:
                TR(ref X, A);
                break;
            case Mos6502Mnemonic.TAY:
                TR(ref Y, A);
                break;
            case Mos6502Mnemonic.TSX:
                TR(ref X, S);
                break;
            case Mos6502Mnemonic.TXA:
                TR(ref A, X);
                break;
            case Mos6502Mnemonic.TXS:
                TXS();
                break;
            case Mos6502Mnemonic.TYA:
                TR(ref A, Y);
                break;
            default:
                throw new InvalidOperationException($"Execution of 0x{(byte)_opcode:X2} is not supported at PC = 0x{_PCAtOpcode:X4} .");
        }
    }

    // Addressing modes
    private void ZeroPage()
    {
        _wv0 = Read(_av, Mos6502MemoryBusAccessKind.OperandZeroPage);
        _wv1 = 0;
        PC++;
        _runState = Mos6502CpuRunState.Execute;
    }

    private void ZeroPageX()
    {
        if (_cycleCount == 0)
        {
            _wv0 = Read(_av, Mos6502MemoryBusAccessKind.OperandZeroPageX);
        }
        else
        {
            Read(_wv0, Mos6502MemoryBusAccessKind.OperandDummyRead);
            _wv0 = (byte)(_wv0 + X);
            _wv1 = 0;
            PC++;
            _runState = Mos6502CpuRunState.Execute;
        }
    }

    private void ZeroPageY()
    {
        if (_cycleCount == 0)
        {
            _wv0 = Read(_av, Mos6502MemoryBusAccessKind.OperandZeroPageY);
        }
        else
        {
            Read(_wv0, Mos6502MemoryBusAccessKind.OperandDummyRead);
            _wv0 = (byte)(_wv0 + Y);
            _wv1 = 0;
            PC++;
            _runState = Mos6502CpuRunState.Execute;
        }
    }

    private void Absolute()
    {
        if (_cycleCount == 0)
        {
            var isJSR = _opcode == Mos6502OpCode.JSR_Absolute;
            _wv0 = Read(_av++, isJSR ? Mos6502MemoryBusAccessKind.OperandJsrAbsoluteLow : Mos6502MemoryBusAccessKind.OperandAbsoluteLow);

            // Special case for JSR
            if (isJSR)
            {
                _runState = Mos6502CpuRunState.Execute;
            }
        }
        else
        {
            _wv1 = Read(_av, Mos6502MemoryBusAccessKind.OperandAbsoluteHigh);
            PC += 2;
            _runState = Mos6502CpuRunState.Execute;
            if (_opcode == Mos6502OpCode.JMP_Absolute) Exec();
        }
    }

    private void AbsoluteX()
    {
        if (_cycleCount == 0)
        {
            _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandAbsoluteXLow);
        }
        else if (_cycleCount == 1)
        {
            _wv1 = Read(_av, Mos6502MemoryBusAccessKind.OperandAbsoluteXHigh);
            if (!PageCross(ref _wv0, X))
            {
                PC += 2;
                _runState = Mos6502CpuRunState.Execute;
            }
        }
        else
        {
            Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.OperandDummyRead);
            _wv1++;
            PC += 2;
            _runState = Mos6502CpuRunState.Execute;
        }
    }

    private void AbsoluteY()
    {
        if (_cycleCount == 0)
        {
            _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandAbsoluteYLow);
        }
        else if (_cycleCount == 1)
        {
            _wv1 = Read(_av, Mos6502MemoryBusAccessKind.OperandAbsoluteYHigh);
            if (!PageCross(ref _wv0, Y))
            {
                PC += 2;
                _runState = Mos6502CpuRunState.Execute;
            }
        }
        else
        {
            Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.OperandDummyRead);
            _wv1++;
            PC += 2;
            _runState = Mos6502CpuRunState.Execute;
        }
    }

    private void Indirect()
    {
        switch (_cycleCount)
        {
            case 0: _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandIndirectLow); break;
            case 1: _wv1 = Read(_av, Mos6502MemoryBusAccessKind.OperandIndirectHigh); break;
            case 2: _av = Read((ushort)((_wv1 << 8) | _wv0++), Mos6502MemoryBusAccessKind.OperandIndirectResolveLow); break;
            case 3:
                _wv1 = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.OperandIndirectResolveHigh);
                _wv0 = (byte)_av;
                PC += 2;
                _runState = Mos6502CpuRunState.Execute;
                if (_opcode == Mos6502OpCode.JMP_Indirect) Exec();
                break;
        }
    }

    private void IndirectX()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = Read(_av, Mos6502MemoryBusAccessKind.OperandIndirectX);
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.OperandDummyRead);
                _av = (ushort)((_av + X) & 0xFF);
                break;
            case 2:
                _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandIndirectXResolveLow);
                break;
            case 3:
                _wv1 = Read((ushort)(_av & 0xFF), Mos6502MemoryBusAccessKind.OperandIndirectXResolveHigh);
                PC++;
                _runState = Mos6502CpuRunState.Execute;
                break;
        }
    }

    private void IndirectY()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = Read(_av, Mos6502MemoryBusAccessKind.OperandIndirectY);
                PC++;
                break;
            case 1:
                _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandIndirectYResolveLow);
                break;
            case 2:
                _wv1 = Read((ushort)(_av & 0xFF), Mos6502MemoryBusAccessKind.OperandIndirectYResolveHigh);
                if (!PageCross(ref _wv0, Y))
                {
                    _runState = Mos6502CpuRunState.Execute;
                }
                break;
            case 3:
                Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.OperandDummyRead);
                _wv1++;
                _runState = Mos6502CpuRunState.Execute;
                break;
        }
    }

    // Opcodes
    private void ADC()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        ADC(p);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private protected void ADC(byte p)
    {
        if (GetFlag(Mos6502CpuFlags.D))
        {
            // From http://www.6502.org/tutorials/decimal_mode.html

            // To predict the value of the Z flag, simply perform the ADC using binary arithmetic.
            int r = p + A + (GetFlag(Mos6502CpuFlags.C) ? 1 : 0);
            SetFlag(Mos6502CpuFlags.Z, (byte)r == 0);

            // Seq. 1:
            // 
            // 1a. AL = (A & $0F) + (B & $0F) + C
            // 1b. If AL >= $0A, then AL = ((AL + $06) & $0F) + $10
            // 1c. A = (A & $F0) + (B & $F0) + AL
            // 1d. Note that A can be >= $100 at this point
            // 1e. If (A >= $A0), then A = A + $60
            // 1f. The accumulator result is the lower 8 bits of A
            // 1g. The carry result is 1 if A >= $100, and is 0 if A < $100

            var AL = (A & 0x0F) + (p & 0x0F) + (GetFlag(Mos6502CpuFlags.C) ? 1 : 0);
            if (AL >= 0x0A) AL = ((AL + 0x06) & 0x0F) + 0x10;
            r = (A & 0xF0) + (p & 0xF0) + AL;
            if (r >= 0xA0) r += 0x60;
            SetFlag(Mos6502CpuFlags.C, r >= 0x100);

            //
            // Seq. 2:
            // 
            // 2a. AL = (A & $0F) + (B & $0F) + C
            // 2b. If AL >= $0A, then AL = ((AL + $06) & $0F) + $10
            // 2c. A = (A & $F0) + (B & $F0) + AL, using signed (twos complement) arithmetic
            // 2e. The N flag result is 1 if bit 7 of A is 1, and is 0 if bit 7 if A is 0
            // 2f. The V flag result is 1 if A < -128 or A > 127, and is 0 if -128 <= A <= 127
            int r2 = (byte)((byte)(A & 0xF0) + (byte)(p & 0xF0) + (byte)AL);
            SetFlag(Mos6502CpuFlags.N, (r2 & 0x80) != 0);
            SetFlag(Mos6502CpuFlags.V, ((((~(A ^ p)) & (A ^ (byte)r2)) & 0x80) != 0));

            A = (byte)r;
        }
        else
        {
            int r = p + A + (GetFlag(Mos6502CpuFlags.C) ? 1 : 0);
            SetFlag(Mos6502CpuFlags.C, r > 0xFF);
            SetFlag(Mos6502CpuFlags.V, ((((~(A ^ p)) & (A ^ (byte)r)) & 0x80) != 0));
            A = (byte)r;
            UpdateNZFlag(A);
        }
    }

    private protected void AND()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A &= p;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ASL()
    {
        if (IsImplied(_addressingMode))
        {
            Read(PC, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            SetFlag(Mos6502CpuFlags.C, ((A >> 7) & 1) != 0);
            A = (byte)(A << 1);
            UpdateNZFlag(A);
            _runState = Mos6502CpuRunState.Fetch;
        }
        else
        {
            switch (_cycleCount)
            {
                case 0:
                    _av = (ushort)((_wv1 << 8) | _wv0);
                    _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                    if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                        _cycleCount++;
                    break;
                case 1:
                    Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                    break;
                case 2:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                    SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                    _wv0 = (byte)(_wv0 << 1);
                    UpdateNZFlag(_wv0);
                    break;
                case 3:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                    _runState = Mos6502CpuRunState.Fetch;
                    break;
            }
        }
    }

    private void BIT()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteRead);
        SetFlag(Mos6502CpuFlags.V, ((p >> 6) & 1) != 0);
        SetFlag(Mos6502CpuFlags.N, ((p >> 7) & 1) != 0);
        p = (byte)(A & p);
        SetFlag(Mos6502CpuFlags.Z, p == 0);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void BRK()
    {
        Read(PC++, Mos6502MemoryBusAccessKind.OperandDummyRead);
        _runningInterrupt = true;
        _raisedBrk = true;
        _interruptFlags = InterruptFlags.Irq; // uses IRQ vector
        HandleInterrupt();
    }

    private void DEC()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                break;
            case 3:
                _wv0--;
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                UpdateNZFlag(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void EOR()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A = (byte)(A ^ p);
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void INC()
    {
        switch (_cycleCount)
        {
            case 0:
                _av = (ushort)((_wv1 << 8) | _wv0);
                _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                    _cycleCount++;
                break;
            case 1:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                break;
            case 3:
                _wv0++;
                Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                UpdateNZFlag(_wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void JMP()
    {
        PC = (ushort)((_wv1 << 8) | _wv0);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void JSR()
    {
        switch (_cycleCount)
        {
            case 0:
                PC += 2;
                Read((ushort)(0x100 + S), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 1:
                Push((byte)(--PC >> 8), Mos6502MemoryBusAccessKind.PushJsrTargetHigh);
                break;
            case 2:
                Push((byte)(PC & 0xFF), Mos6502MemoryBusAccessKind.PushJsrTargetLow);
                break;
            case 3:
                _wv1 = Read(_av, Mos6502MemoryBusAccessKind.OperandJsrAbsoluteHigh);
                PC = (ushort)((_wv1 << 8) | _wv0);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private protected void LSR()
    {
        if (IsImplied(_addressingMode))
        {
            SetFlag(Mos6502CpuFlags.C, (A & 1) != 0);
            A = (byte)(A >> 1);
            UpdateNZFlag(A);
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            _runState = Mos6502CpuRunState.Fetch;
        }
        else
        {
            switch (_cycleCount)
            {
                case 0:
                    _av = (ushort)((_wv1 << 8) | _wv0);
                    _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                    if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                        _cycleCount++;
                    break;
                case 1:
                    Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                    break;
                case 2:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                    SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                    _wv0 = (byte)(_wv0 >> 1);
                    UpdateNZFlag(_wv0);
                    break;
                case 3:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                    _runState = Mos6502CpuRunState.Fetch;
                    break;
            }
        }
    }

    private void NOP()
    {
        if (IsImmediate(_addressingMode) || IsImmediate(_addressingMode))
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        }
        else if (_addressingMode == Mos6502AddressingMode.ZeroPage || _addressingMode == Mos6502AddressingMode.ZeroPageX)
        {
            Read(_wv0, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        }
        else
        {
            Read((ushort)((_wv1 << 8) | _wv0), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        }
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ORA()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        A |= p;
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void ROL()
    {
        if (IsImplied(_addressingMode))
        {
            bool tmp = GetFlag(Mos6502CpuFlags.C);
            SetFlag(Mos6502CpuFlags.C, ((A >> 7) & 1) != 0);
            A = (byte)(A << 1);
            if (tmp) A |= 1;
            UpdateNZFlag(A);
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            _runState = Mos6502CpuRunState.Fetch;
        }
        else
        {
            switch (_cycleCount)
            {
                case 0:
                    _av = (ushort)((_wv1 << 8) | _wv0);
                    _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                    if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                        _cycleCount++;
                    break;
                case 1:
                    Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                    break;
                case 2:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                    bool tmp = GetFlag(Mos6502CpuFlags.C);
                    SetFlag(Mos6502CpuFlags.C, ((_wv0 >> 7) & 1) != 0);
                    _wv0 = (byte)(_wv0 << 1);
                    if (tmp) _wv0 |= 1;
                    UpdateNZFlag(_wv0);
                    break;
                case 3:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                    _runState = Mos6502CpuRunState.Fetch;
                    break;
            }
        }
    }

    private protected void ROR()
    {
        if (IsImplied(_addressingMode))
        {
            bool tmp = GetFlag(Mos6502CpuFlags.C);
            SetFlag(Mos6502CpuFlags.C, (A & 1) != 0);
            A = (byte)(A >> 1);
            if (tmp) A |= 0x80;
            UpdateNZFlag(A);
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            _runState = Mos6502CpuRunState.Fetch;
        }
        else
        {
            switch (_cycleCount)
            {
                case 0:
                    _av = (ushort)((_wv1 << 8) | _wv0);
                    _wv0 = Read(_av, Mos6502MemoryBusAccessKind.ExecuteRead);
                    if (_addressingMode != Mos6502AddressingMode.AbsoluteX || _pageCrossed)
                        _cycleCount++;
                    break;
                case 1:
                    Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                    break;
                case 2:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteDummyWrite);
                    bool tmp = GetFlag(Mos6502CpuFlags.C);
                    SetFlag(Mos6502CpuFlags.C, (_wv0 & 1) != 0);
                    _wv0 = (byte)(_wv0 >> 1);
                    if (tmp) _wv0 |= 0x80;
                    UpdateNZFlag(_wv0);
                    break;
                case 3:
                    Write(_av, _wv0, Mos6502MemoryBusAccessKind.ExecuteWrite);
                    _runState = Mos6502CpuRunState.Fetch;
                    break;
            }
        }
    }

    private void RTI()
    {
        switch (_cycleCount)
        {
            case 0:
            case 1:
            case 2:
                PLP();
                _runState = Mos6502CpuRunState.Execute;
                break;
            case 3:
                PC = Pop(Mos6502MemoryBusAccessKind.PopRtiLow);
                break;
            case 4:
                PC = (ushort)((Pop(Mos6502MemoryBusAccessKind.PopRtiHigh) << 8) | PC);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private void RTS()
    {
        switch (_cycleCount)
        {
            case 0:
                Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 1:
                Read((ushort)(0x100 + S), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                break;
            case 2:
                PC = Pop(Mos6502MemoryBusAccessKind.PopRtsLow);
                break;
            case 3:
                PC = (ushort)((Pop(Mos6502MemoryBusAccessKind.PopRtsHigh) << 8) | PC);
                break;
            case 4:
                Read(PC++, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
                _runState = Mos6502CpuRunState.Fetch;
                break;
        }
    }

    private protected void SBC()
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        SBC(p);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private protected void SBC(byte p)
    {
        // http://www.6502.org/tutorials/decimal_mode.html
        if (GetFlag(Mos6502CpuFlags.D))
        {
            // 3a. AL = (A & $0F) - (B & $0F) + C-1
            // 3b. If AL < 0, then AL = ((AL - $06) & $0F) - $10
            // 3c. A = (A & $F0) - (B & $F0) + AL
            // 3d. If A < 0, then A = A - $60
            // 3e. The accumulator result is the lower 8 bits of A
            var AL = (A & 0x0F) - (p & 0x0F) + (GetFlag(Mos6502CpuFlags.C) ? 0 : -1);
            if (AL < 0) AL = ((AL - 0x06) & 0x0F) - 0x10;
            int r = (A & 0xF0) - (p & 0xF0) + AL;
            if (r < 0) r -= 0x60;
            var newA = (byte)r;

            r = A + (p ^ 0xFF) + (GetFlag(Mos6502CpuFlags.C) ? 1 : 0);
            SetFlag(Mos6502CpuFlags.C, r > 0xFF);
            SetFlag(Mos6502CpuFlags.V, ((((A ^ r) & (((p ^ 0xFF)) ^ r)) & 0x80) != 0));
            UpdateNZFlag((byte)r);

            A = newA;
        }
        else
        {
            int r = A + (p ^ 0xFF) + (GetFlag(Mos6502CpuFlags.C) ? 1 : 0);
            SetFlag(Mos6502CpuFlags.C, r > 0xFF);
            SetFlag(Mos6502CpuFlags.V, ((((A ^ r) & (((p ^ 0xFF)) ^ r)) & 0x80) != 0));
            A = (byte)r;
            UpdateNZFlag(A);
        }
    }

    private void PHP()
    {
        if (_cycleCount == 0)
        {
            Read(PC, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }

        var sr = SR | Mos6502CpuFlags.B | Mos6502CpuFlags.U;
        
        Push((byte)sr, Mos6502MemoryBusAccessKind.PushSR);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void PLP()
    {
        if (_cycleCount == 0)
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }
        if (_cycleCount == 1)
        {
            Read((ushort)(0x100 + S), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }

        byte p = Pop(Mos6502MemoryBusAccessKind.PopSR);
        SetFlag(Mos6502CpuFlags.C, (p & 1) != 0);
        SetFlag(Mos6502CpuFlags.Z, ((p >> 1) & 1) != 0);
        SetFlag(Mos6502CpuFlags.I, ((p >> 2) & 1) != 0);
        SetFlag(Mos6502CpuFlags.D, ((p >> 3) & 1) != 0);
        SetFlag(Mos6502CpuFlags.B, false);
        SetFlag(Mos6502CpuFlags.U, true);
        SetFlag(Mos6502CpuFlags.V, ((p >> 6) & 1) != 0);
        SetFlag(Mos6502CpuFlags.N, ((p >> 7) & 1) != 0);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void TXS()
    {
        S = X;
        Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        _runState = Mos6502CpuRunState.Fetch;
    }

    // Wildcard opcodes
    private void BRA(Mos6502CpuFlags flag, bool value)
    {
        if (_cycleCount == 0)
        {
            _av = (ushort)((_wv1 << 8) | _wv0);
            _wv0 = Read(_av++, Mos6502MemoryBusAccessKind.OperandBranchOffset);
            if (GetFlag(flag) == value)
            {
                return;
            }
            else
            {
                _runState = Mos6502CpuRunState.Fetch;
            }
        }
        else if (_cycleCount == 1)
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            byte tmp = (byte)(PC >> 8);
            PC = (ushort)(PC + (sbyte)_wv0);
            if (tmp != (byte)(PC >> 8))
            {
                _pageCrossed = true;
                return;
            }
            _pageCrossed = false;
            _runState = Mos6502CpuRunState.Fetch;
        }
        else
        {
            if ((sbyte)_wv0 > 0)
                Read((ushort)(PC - 0x100), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            else
                Read((ushort)(PC + 0x100), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            _runState = Mos6502CpuRunState.Fetch;
        }
    }

    private void CLOrSFlag(Mos6502CpuFlags flag, bool set)
    {
        Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        SetFlag(flag, set);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void CMP(ref byte reg)
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        p = (byte)(reg - p);
        UpdateNZFlag(p);
        SetFlag(Mos6502CpuFlags.C, reg >= p);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void DEC(ref byte reg0)
    {
        reg0--;
        UpdateNZFlag(reg0);
        Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void INC(ref byte reg0)
    {
        reg0++;
        UpdateNZFlag(reg0);
        Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void LD(ref byte reg)
    {
        byte p = Read((ushort)((_wv1 << 8) | _wv0), _pendingExecuteReadKind);
        reg = p;
        UpdateNZFlag(reg);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private protected void ST(ref byte reg)
    {
        _av = (ushort)((_wv1 << 8) | _wv0);
        if (_cycleCount == 0 && !_pageCrossed && (_addressingMode == Mos6502AddressingMode.IndirectY || _addressingMode == Mos6502AddressingMode.AbsoluteY || _addressingMode == Mos6502AddressingMode.AbsoluteX))
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }
        Write(_av, reg, Mos6502MemoryBusAccessKind.ExecuteWrite);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void TR(ref byte dst, byte src)
    {
        dst = src;
        UpdateNZFlag(dst);
        Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void PHA()
    {
        if (_cycleCount == 0)
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }
        Push(A, Mos6502MemoryBusAccessKind.PushRegisterA);
        _runState = Mos6502CpuRunState.Fetch;
    }

    private void PLA()
    {
        if (_cycleCount == 0)
        {
            Read(_av, Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }
        if (_cycleCount == 1)
        {
            Read((ushort)(0x100 + S), Mos6502MemoryBusAccessKind.ExecuteDummyRead);
            return;
        }
        A = Pop(Mos6502MemoryBusAccessKind.PopRegisterA);
        UpdateNZFlag(A);
        _runState = Mos6502CpuRunState.Fetch;
    }
    
    private protected virtual void JAM()
    {
        // To be overridden by subclasses
    }

    // Memory helpers
    private protected byte Read(ushort address, Mos6502MemoryBusAccessKind kind)
    {
        var bus = _bus;
        if (bus is not null)
        {
            bus.Trace(kind);
            return bus.Read(address);
        }
        else
        {
            return CpuDefaultReadValue;
        }
    }

    private protected void Write(ushort address, byte value, Mos6502MemoryBusAccessKind kind)
    {
        var bus = _bus;
        if (bus is not null)
        {
            bus.Trace(kind);
            bus.Write(address, value);
        }
    }

    private void Push(byte value, Mos6502MemoryBusAccessKind kind) => Write((ushort)(0x100 + S--), value, kind);

    private byte Pop(Mos6502MemoryBusAccessKind kind) => Read((ushort)(0x100 + (++S)), kind);

    private protected void UpdateNZFlag(byte v)
    {
        SetFlag(Mos6502CpuFlags.N, (v & 0x80) != 0);
        SetFlag(Mos6502CpuFlags.Z, v == 0);
    }

    private bool PageCross(ref byte v1, byte v2)
    {
        byte before = v1;
        v1 = (byte)(v1 + v2);
        if (v1 < before)
        {
            _pageCrossed = true;
            return true;
        }
        _pageCrossed = false;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool GetFlag(Mos6502CpuFlags flag) => (SR & flag) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected void SetFlag(Mos6502CpuFlags flag, bool value) => SR = value ? SR | flag : (SR & ~flag);

    // Stopwatch helpers
    private static long TicksToNanoseconds(long ticks) => (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));

    private static long NanosecondsToTicks(long ns) => (long)(ns * (Stopwatch.Frequency / 1_000_000_000.0));

    // Enums
    private enum InterruptFlags { Nmi, Reset, Irq, None }
}