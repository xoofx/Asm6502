// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Specifies the kind of memory access performed during CPU instruction execution, distinguishing between opcode
/// fetches, operand reads and writes, addressing mode cycles, stack operations, and interrupt handling.
///
/// This is used for tracing and debugging memory access patterns via <see cref="IMos6502CpuMemoryBus.Trace"/>
/// when executing CPU instructions with <see cref="Mos6502Cpu"/> or its derivatives.
/// </summary>
/// <remarks>Use this enumeration to identify the purpose and context of each memory access cycle in a CPU
/// emulation or tracing scenario. The values correspond to specific phases of instruction fetch, operand resolution,
/// execution, stack manipulation, and interrupt processing, enabling detailed analysis or instrumentation of memory
/// operations. This can be useful for debugging, logging, or implementing memory access policies that depend on the
/// type of access being performed.</remarks>
public enum Mos6502MemoryBusAccessKind
{
    /// <summary>
    /// An undefined or uninitialized state. This value should not occur during normal operation.
    /// </summary>
    Undefined,
    /// <summary>
    /// The opcode byte being fetched from PC.
    /// </summary>
    OpCode,
    /// <summary>
    /// The immediate operand byte being fetched from PC.
    /// </summary>
    OperandImmediate,
    /// <summary>
    /// The low byte of an absolute address being fetched from PC.
    /// </summary>
    OperandAbsoluteLow,
    /// <summary>
    /// The high byte of an absolute address being fetched from PC.
    /// </summary>
    OperandAbsoluteHigh,
    /// <summary>
    /// The low byte of the absolute X-indexed addressing operand from PC.
    /// </summary>
    OperandAbsoluteXLow,
    /// <summary>
    /// The high byte of the absolute X-indexed addressing operand from PC.
    /// </summary>
    OperandAbsoluteXHigh,
    /// <summary>
    /// The low byte of the absolute Y-indexed addressing operand from PC.
    /// </summary>
    OperandAbsoluteYLow,
    /// <summary>
    /// The high byte of the absolute Y-indexed addressing operand from PC.
    /// </summary>
    OperandAbsoluteYHigh,
    /// <summary>
    /// The zero page address being fetched from PC.
    /// </summary>
    OperandZeroPage,
    /// <summary>
    /// The zero page X-indexed address being fetched from PC.
    /// </summary>
    OperandZeroPageX,
    /// <summary>
    /// The zero page Y-indexed address being fetched from PC.
    /// </summary>
    OperandZeroPageY,
    /// <summary>
    /// An unused read from the address bus, typically during an operand fetch cycle.
    /// </summary>
    OperandDummyRead,
    /// <summary>
    /// The low byte of the indirect address being fetched from PC.
    /// </summary>
    OperandIndirectLow,
    /// <summary>
    /// The high byte of the indirect address being fetched from PC.
    /// </summary>
    OperandIndirectHigh,
    /// <summary>
    /// The low byte of the resolved indirect address.
    /// </summary>
    OperandIndirectResolveLow,
    /// <summary>
    /// The high byte of the resolved indirect address.
    /// </summary>
    OperandIndirectResolveHigh,
    /// <summary>
    /// The zero-page address fetched from PC, to be indexed by the X register for indirect addressing.
    /// </summary>
    OperandIndirectX,
    /// <summary>
    /// The low byte of the resolved indirect X-indexed address.
    /// </summary>
    OperandIndirectXResolveLow,
    /// <summary>
    /// The high byte of the resolved indirect X-indexed address.
    /// </summary>
    OperandIndirectXResolveHigh,
    /// <summary>
    /// The zero-page address fetched from PC, to be indexed by the Y register for indirect addressing.
    /// </summary>
    OperandIndirectY,
    /// <summary>
    /// The low byte of the resolved indirect Y-indexed address.
    /// </summary>
    OperandIndirectYResolveLow,
    /// <summary>
    /// The high byte of the resolved indirect Y-indexed address.
    /// </summary>
    OperandIndirectYResolveHigh,
    /// <summary>
    /// The low byte of the subroutine address being fetched from PC during a JSR instruction.
    /// </summary>
    OperandJsrAbsoluteLow,
    /// <summary>
    /// The high byte of the subroutine address being fetched from PC during a JSR instruction.
    /// </summary>
    OperandJsrAbsoluteHigh,
    /// <summary>
    /// The branch offset byte being fetched from PC during a branch instruction.
    /// </summary>
    OperandBranchOffset,
    /// <summary>
    /// A byte read during the execution phase of an instruction.
    /// </summary>
    ExecuteRead,
    /// <summary>
    /// A dummy read during the execution phase of an instruction.
    /// </summary>
    ExecuteDummyRead,
    /// <summary>
    /// A dummy write during the execution phase of an instruction.
    /// </summary>
    ExecuteDummyWrite,
    /// <summary>
    /// A byte write during the execution phase of an instruction.
    /// </summary>
    ExecuteWrite,
    /// <summary>
    /// The low byte of the interrupt vector address being fetched during an interrupt/reset.
    /// </summary>
    VectorInterruptLow,
    /// <summary>
    /// The high byte of the interrupt vector address being fetched during an interrupt/reset.
    /// </summary>
    VectorInterruptHigh,
    /// <summary>
    /// The status register byte being pushed onto the stack during a PHP instruction.
    /// </summary>
    PushSR,
    /// <summary>
    /// The status register byte being popped from the stack during a PLP instruction.
    /// </summary>
    PopSR,
    /// <summary>
    /// A register value being pushed onto the stack during a PHA instruction.
    /// </summary>
    PushRegisterA,
    /// <summary>
    /// A register value being popped from the stack during a PLA instruction.
    /// </summary>
    PopRegisterA,
    /// <summary>
    /// The high byte of the return address being pushed onto the stack during an interrupt.
    /// </summary>
    PushInterruptReturnAddressHigh,
    /// <summary>
    /// The low byte of the return address being pushed onto the stack during an interrupt.
    /// </summary>
    PushInterruptReturnAddressLow,
    /// <summary>
    /// The status register byte being pushed onto the stack during an interrupt.
    /// </summary>
    PushInterruptSR,
    /// <summary>
    /// The low byte of the return address being pushed onto the stack during a JSR instruction.
    /// </summary>
    PushJsrTargetLow,
    /// <summary>
    /// The high byte of the return address being pushed onto the stack during a JSR instruction.
    /// </summary>
    PushJsrTargetHigh,
    /// <summary>
    /// The low byte of the return address being popped from the stack during an RTI instruction.
    /// </summary>
    PopRtiLow,
    /// <summary>
    /// The high byte of the return address being popped from the stack during an RTI instruction.
    /// </summary>
    PopRtiHigh,
    /// <summary>
    /// The low byte of the return address being popped from the stack during an RTS instruction.
    /// </summary>
    PopRtsLow,
    /// <summary>
    /// The high byte of the return address being popped from the stack during an RTS instruction.
    /// </summary>
    PopRtsHigh,
}
