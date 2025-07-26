// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502.Tests;

public abstract class VerifyAsmBase : VerifyBase
{
    protected async Task VerifyAsm(Mos6502Assembler asm)
    {
        var dis = new Mos6502Disassembler(new Mos6502DisassemblerOptions()
        {
            PrintLabelBeforeFirstInstruction = false,
            PrintAddress = true,
            PrintAssemblyBytes = true,
        });

        var asmText = dis.Disassemble(asm.Buffer);
        var allBytes = $"; {string.Join(" ", asm.Buffer.ToArray().Select(x => $"{x:X2}"))}";
        var text = $"{asmText}{Environment.NewLine}{allBytes}";
        
        await Verify(text);
    }
}