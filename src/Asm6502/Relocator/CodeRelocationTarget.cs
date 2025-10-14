// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public readonly record struct CodeRelocationTarget
{
    public required ushort Address { get; init; }

    public required RamZpRange ZpRange { get; init; }
}