// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

[Flags]
public enum RamByteFlags
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    NoReloc = 1 << 2,
    Reloc = 1 << 3,
    UsedInZp = 1 << 4,
    UsedInMsb = 1 << 5,
}