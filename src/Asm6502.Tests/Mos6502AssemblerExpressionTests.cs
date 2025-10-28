// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502.Expressions;

namespace Asm6502.Tests;

[TestClass]
public class Mos6502AssemblerExpressionTests : VerifyMos6502Base
{
    [TestMethod]
    public async Task TestLowHighBytes()
    {
        using var asm = CreateAsm();

        asm
            .Begin()
            .LabelForward(out var forwardLabel)
            .LDA_Imm(forwardLabel.LowByte())
            .STA(0x1000)
            .LDA_Imm(forwardLabel.HighByte())
            .STA(0x1001)
            .RTS()
            .Label(forwardLabel)
            .End();

        await VerifyAsm(asm);
    }

    [TestMethod]
    public async Task TestSubtract()
    {
        using var asm = CreateAsm();

        asm
            .Begin()
            .Label("start", out var startLabel)
            .LabelForward("end", out var endLabel)
            .LDA_Imm((endLabel - startLabel).LowByte()) // Store the size of this code
            .STA(0x1000)
            .LDA_Imm(((endLabel - startLabel) + 1).LowByte()) // Store the size of this code
            .STA(0x1001)
            .RTS()
            .Label(endLabel)
            .End();

        await VerifyAsm(asm);
    }


    [TestMethod]
    public async Task TestFunctions()
    {
        using var asm = CreateAsm();

        // Dynamic functions
        asm
            .Begin()
            .LDA_Imm((Mos6502ExpressionU8)(() => (byte)10))
            .STA(0x1000)
            .LDA((Mos6502ExpressionU16)(() => (ushort)0x1234))
            .End();

        await VerifyAsm(asm);
    }

    [TestMethod]
    public async Task TestZeroPage()
    {
        using var asm = CreateAsm();

        var zp1 = new Mos6502Label("test1", 0x01);
        var zp2 = new Mos6502Label("test2", 0x02);

        asm
            .Begin()
            .LDA(zp1)
            .STA(zp2)
            .RTS();

        var labels = new HashSet<Mos6502Label>();
        asm.CollectLabels(labels);
        CollectionAssert.AreEqual(new [] { zp1, zp2 }, labels.OrderBy(x => x.Address).ToArray());

        asm.End();

        await VerifyAsm(asm);
    }
}