// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Tests;

[TestClass]
public class Mos6502LabelTests
{

    [TestMethod]
    public void TestParseLabelExpression()
    {
        // Test that we capture the name of the variable via expression caller argument info
        var asm = new Mos6502Assembler();

        asm.Label(out var myLabel);
        Assert.AreEqual(nameof(myLabel), myLabel.Name);

        Mos6502Label localVarLabel;
        asm.Label(out localVarLabel);
        Assert.AreEqual(nameof(localVarLabel), localVarLabel.Name);

        asm.LabelForward(out var myLabel2);
        Assert.AreEqual(nameof(myLabel2), myLabel2.Name);

        // Expression with arrays are not supported
        Mos6502Label[] labels = new Mos6502Label[2];
        asm.Label(out labels[0]);
        Assert.AreEqual(null, labels[0].Name);
    }
}