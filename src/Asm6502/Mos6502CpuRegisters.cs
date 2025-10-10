// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

#pragma warning disable CS1591

namespace Asm6502;

/// <summary>
/// Represents a compact, mutable snapshot of the <see cref="Mos6502Cpu"/> register file.
/// </summary>
/// <param name="PC">Program Counter (16-bit), address of the next instruction byte to fetch.</param>
/// <param name="A">Accumulator (A) register.</param>
/// <param name="X">Index register X.</param>
/// <param name="Y">Index register Y.</param>
/// <param name="S">
/// Stack Pointer (SP), 8-bit offset within the hardware stack page $0100–$01FF (effective stack address is 0x0100 + S).
/// </param>
/// <param name="SR">Processor Status (P) flags as a bitfield of <see cref="Mos6502CpuFlags"/>.</param>
public record struct Mos6502CpuRegisters(ushort PC, byte A, byte X, byte Y, byte S, Mos6502CpuFlags SR);