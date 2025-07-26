// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;
using static AsmMos6502.Mos6502InstructionFactory;
using static AsmMos6502.Mos6502Factory;

namespace AsmMos6502.Tests;

[TestClass]
public class Mos6502InstructionTests : VerifyAsmBase
{
    [TestMethod]
    public void TestFactory()
    {
        var inst = LDA(0x42);
        // Don't need to test more Mos6502AddressingMode as it is used in all other tests implicitly
        Assert.AreEqual(Mos6502AddressingMode.ZeroPage, inst.AddressingMode);
        Assert.AreEqual(2, inst.SizeInBytes);
        Assert.AreEqual(0x42, inst.Operand);
    }
    
    [TestMethod]
    public async Task TestToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{LDA(0x42)}");
        builder.AppendLine($"{LDA(0x1234)}");
        builder.AppendLine($"{LDA_Imm(25)}");
        builder.AppendLine($"{LDA(25, X)}");
        builder.AppendLine($"{LDA(25, Y)}");
        builder.AppendLine($"{ADC(_[25, X])}");
        builder.AppendLine($"{ADC(_[25], Y)}");
        builder.AppendLine($"{JMP(0x1234)}");
        builder.AppendLine($"{JMP(_[0x5678])}");
        builder.AppendLine($"{BEQ(-5)}");
        var text = builder.ToString();

        await Verify(text);
    }

    [TestMethod]
    public async Task TestToStringToLower()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{LDA(0x42).ToString("l", null)}");
        builder.AppendLine($"{LDA(25, X).ToString("l", null)}");
        var text = builder.ToString();

        await Verify(text);
    }
}