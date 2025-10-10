// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502;

/// <summary>
/// Defines the external 64 KiB memory bus that <see cref="Mos6502Cpu"/> uses to access memory and devices.
/// </summary>
/// <remarks>
/// <para>
/// The CPU presents a 16-bit address (0x0000–0xFFFF) and performs a read or write once per bus cycle.
/// Implementations should map this flat address space to RAM/ROM and memory‑mapped I/O as appropriate.
/// </para>
/// <para>
/// The CPU will call <see cref="Read(ushort)"/> and <see cref="Write(ushort, byte)"/> for all memory activity:
/// opcode fetches, operand reads, dummy reads, stack pushes/pops (0x0100–0x01FF), vector fetches
/// (<see cref="Mos6502Cpu.CpuVectorNmi"/>, <see cref="Mos6502Cpu.CpuVectorReset"/>, <see cref="Mos6502Cpu.CpuVectorIrq"/>),
/// and read‑modify‑write sequences (which perform a read followed by two writes).
/// </para>
/// <para>
/// Timing: the CPU's cycle timing is driven internally; the bus is a simple byte‑wide storage/IO abstraction.
/// If you need to account for cycles, measure calls to these methods or hook <see cref="Mos6502Cpu.TimestampCounter"/>.
/// </para>
/// <para>
/// Threading: the CPU expects the bus to be synchronous and fast; typical usage is single‑threaded.
/// </para>
/// </remarks>
/// <seealso cref="Mos6502Cpu"/>
public interface IMos6502CpuMemoryBus
{
    /// <summary>
    /// Reads a byte from the specified 16-bit address presented by <see cref="Mos6502Cpu"/>.
    /// </summary>
    /// <param name="address">The absolute address in the 0x0000–0xFFFF range.</param>
    /// <returns>The byte at the given address. For unmapped regions, return an implementation-defined value (e.g., open-bus).</returns>
    /// <remarks>
    /// <para>
    /// This method is invoked for all CPU read cycles, including opcode fetches, operand reads, dummy reads,
    /// stack pops, and interrupt/vector fetches.
    /// </para>
    /// <para>
    /// Do not throw from this method during normal emulation. If a bus is not attached, the CPU uses
    /// <see cref="Mos6502Cpu.CpuDefaultReadValue"/> (0xEA, NOP) internally.
    /// </para>
    /// </remarks>
    byte Read(ushort address);

    /// <summary>
    /// Writes a byte to the specified 16-bit address presented by <see cref="Mos6502Cpu"/>.
    /// </summary>
    /// <param name="address">The absolute address in the 0x0000–0xFFFF range.</param>
    /// <param name="value">The byte to write.</param>
    /// <remarks>
    /// <para>
    /// This method is invoked for all CPU write cycles, including stores and stack pushes.
    /// For read‑modify‑write instructions (ASL/LSR/ROL/ROR/INC/DEC on memory), the CPU performs a read then
    /// two writes: first the unmodified value, then the modified value.
    /// </para>
    /// <para>
    /// Writes to ROM regions may be ignored by the implementation. Use this hook to implement memory‑mapped I/O side effects.
    /// </para>
    /// </remarks>
    void Write(ushort address, byte value);
}