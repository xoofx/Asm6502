// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace AsmMos6502.Tests;

public abstract class VerifyAsmMos6502Base : VerifyBase
{
    protected Mos6502Assembler CreateAsm(ushort address = 0xc000)
    {
        return new Mos6502Assembler()
        {
            DebugMap = new Mos6502AssemblerDebugMap()
            {
                Name = TestContext.TestName
            }
        }.Begin(address);
    }
    
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

        // Extract the debug map as a string
        var debugMapText = asm.DebugMap is not null ? $"{Environment.NewLine}{Environment.NewLine}{asm.DebugMap!.ToString()!.Replace("\\", "/")}" : string.Empty;
        var text = $"{asmText}{Environment.NewLine}{allBytes}{debugMapText}";
        
        await Verify(text);
    }
}

public abstract class VerifyAsmMos6510Base : VerifyBase
{
    protected Mos6510Assembler CreateAsm(ushort address = 0xc000)
    {
        return new Mos6510Assembler()
        {
            DebugMap = new Mos6502AssemblerDebugMap()
            {
                Name = TestContext.TestName
            }
        }.Begin(address);
    }
    
    protected async Task VerifyAsm(Mos6510Assembler asm)
    {
        var dis = new Mos6510Disassembler(new Mos6510DisassemblerOptions()
        {
            PrintLabelBeforeFirstInstruction = false,
            PrintAddress = true,
            PrintAssemblyBytes = true,
        });

       
        var asmText = dis.Disassemble(asm.Buffer);
        var allBytes = $"; {string.Join(" ", asm.Buffer.ToArray().Select(x => $"{x:X2}"))}";

        // Extract the debug map as a string
        var debugMapText = asm.DebugMap is not null ? $"{Environment.NewLine}{Environment.NewLine}{asm.DebugMap!.ToString()!.Replace("\\", "/")}" : string.Empty;
        var text = $"{asmText}{Environment.NewLine}{allBytes}{debugMapText}";
        
        await Verify(text);
    }
}
