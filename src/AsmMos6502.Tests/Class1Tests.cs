// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using static AsmMos6502.Mos6502Factory;

namespace AsmMos6502.Tests;

[TestClass]
public class Class1Test
{
    [TestMethod]
    public void TestSimple()
    {
        using var asm = new Mos6502Assembler();

        var forwardLabel = new Mos6502Label();

        asm
            .LDA(0x5)
            .STA(0x1000)
            .Label(out var label)
            .LDA(_[0x1, X])
            .LDA(_[0x2], Y)
            .BEQ(label)
            .BCC(forwardLabel)
            .RTS()
            .Label(forwardLabel)
            .End();

        Console.WriteLine(string.Join(" ", asm.Buffer.ToArray().Select(x => $"{x:X2}")));

        var dis = new Mos6502Disassembler(new Mos6502DisassemblerOptions()
        {
            PrintAddress = true,
            PrintAssemblyBytes = true,
        });
        var text = dis.Disassemble(asm.Buffer);
        Console.WriteLine(text);
    }
}
