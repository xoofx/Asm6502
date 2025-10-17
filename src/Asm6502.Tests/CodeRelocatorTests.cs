// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502.Relocator;
using static Asm6502.Mos6502Factory;

namespace Asm6502.Tests;

/// <summary>
/// Tests for <see cref="CodeRelocator"/>
/// </summary>
[TestClass]
public class CodeRelocatorTests : VerifyBase
{
    [TestMethod]
    public async Task TestAddressingAbsolute()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var data)
                .LDA(data)
                .RTS()

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .End()
        ));

    [TestMethod]
    public async Task TestAddressingAbsoluteX()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var data)
                .LDX_Imm(0x02)
                .LDA(data, X)
                .RTS()

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .Append((byte)Mos6510OpCode.CLC_Implied)
                .Append((byte)Mos6510OpCode.INX_Implied)
                .End()
        ));

    [TestMethod]
    public async Task TestAddressingAbsoluteY()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var data)
                .LDY_Imm(0x01)
                .LDA(data, Y)
                .RTS()

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .Append((byte)Mos6510OpCode.CLC_Implied)
                .End()
        ));

    [TestMethod]
    public async Task TestAddressingIndirect()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var data)
                .LabelForward(out var indirectDataLo)
                .LabelForward(out var indirectDataHi)
                .LDA(_[indirectDataLo])
                .RTS()

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .Label(indirectDataLo)
                .Append(data.LowByte())
                .Append(data.HighByte())
                .End()
        ));

    [TestMethod]
    public async Task TestAddressingIndirectX()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var dataPtrLo)
                .LabelForward(out var dataPtrHi)
                .LabelForward(out var data)
                .LDA(dataPtrLo)
                .STA(0x10)
                .LDA(dataPtrHi)
                .STA(0x11)
                .LDX_Imm(0x00)
                .LDA(_[0x10, X])
                .RTS()

                // Store pointer at ZP $10
                .Label(dataPtrLo)
                .Append(data.LowByte())
                .Label(dataPtrHi)
                .Append(data.HighByte())

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .End()
        ));

    [TestMethod]
    public async Task TestAddressingIndirectY()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var dataPtrLo)
                .LabelForward(out var dataPtrHi)
                .LabelForward(out var data)
                .LDA(dataPtrLo)
                .STA(0x10)
                .LDA(dataPtrHi)
                .STA(0x11)
                .LDY_Imm(0x00)
                .LDA(_[0x10], Y)
                .RTS()

                // Store pointer at ZP $10
                .Label(dataPtrLo)
                .Append(data.LowByte())
                .Label(dataPtrHi)
                .Append(data.HighByte())

                .Label(data)
                .Append((byte)Mos6510OpCode.NOP_Implied)
                .End()
        ));


    [TestMethod]
    public async Task TestJMP()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .JMP(out var target)
            .LDA_Imm(0xFF)
            .Label(target)
            .RTS()
            .End()
        ));

    [TestMethod]
    public async Task TestJSR()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .JSR(out var subroutine)
            .RTS()

            .Label(subroutine)
            .LDA_Imm(0x42)
            .RTS()
            .End()
        ));


  
    [TestMethod]
    public async Task TestLAX()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .LabelForward(out var data)
            .LabelForward(out var dataHi)
            .LAX(dataHi)
            .LabelForward(out var modLDA)
            .STX(modLDA + 2)
            .Label(modLDA)
            .LDA(data.LowByte().ToAbsolute()) // LDA $00cc, high byte is modified above
            .RTS()

            .Label(data)
            .Append((byte)Mos6510OpCode.NOP_Implied)
            .Label(dataHi)
            .Append(data.HighByte())
            .NOP()
            .NOP()
            .NOP()
            .End()
        ));

    // Self-modifying code tests

    [TestMethod]
    public async Task TestSelfModifyingCode_StoreAndLoad()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .LabelForward(out var addrLo)
            .LabelForward(out var addrHi)
            .LabelForward(out var data)
            
            // Store low byte of data address
            .LDA(out var loadDataLo)
            .STA(addrLo)
            .LDA(out var loadDataHi)
            .STA(addrHi)
            
            // Load from the stored address using ZP _
            .LDA(addrLo)
            .STA(0x02)  // Store low byte to ZP
            .LDA(addrHi)
            .STA(0x03)  // Store high byte to ZP
            .LDY_Imm(0x00)
            .LDA(_[0x02], Y)  // Load using _ addressing
            .RTS()

            .Label(loadDataLo)
            .Append(data.LowByte())
            .Label(loadDataHi)
            .Append(data.HighByte())
            
            .Label(addrLo)
            .Append(0x00)
            .Label(addrHi)
            .Append(0x00)
            .Label(data)
            .Append((byte)Mos6510OpCode.NOP_Implied)
            .End()
        ));

    [TestMethod]
    public async Task TestSelfModifyingCode_ModifyInstruction()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .LabelForward(out var target)
            .LabelForward(out var modifiedInstruction)
            
            // Modify the high byte of a LDA instruction
            .LDA(out var loadTargetHi)
            .STA(modifiedInstruction + 2)
            
            .Label(modifiedInstruction)
            .LDA(target.LowByte().ToAbsolute())  // LDA $00cc, high byte is modified above
            .RTS()

            .Label(loadTargetHi)
            .Append(target.HighByte())
           
            .Label(target)
            .Append((byte)Mos6510OpCode.NOP_Implied)
            .End()
        ));

    [TestMethod]
    public async Task TestSelfModifyingCode_DynamicJump()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .LabelForward(out var jmpTarget)
            .LabelForward(out var jmpAddrLo)
            .LabelForward(out var jmpAddrHi)
            
            // Store jump target address
            .LDA(out var loadTargetLo)
            .STA(jmpAddrLo)
            .LDA(out var loadTargetHi)
            .STA(jmpAddrHi)
            
            // _ jump through modified address
            .JMP(_[jmpAddrLo])

            .Label(loadTargetLo)
            .Append(jmpTarget.LowByte())
            .Label(loadTargetHi)
            .Append(jmpTarget.HighByte())
            
            .Label(jmpAddrLo)
            .Append(0x00)
            .Label(jmpAddrHi)
            .Append(0x00)
            
            .Label(jmpTarget)
            .LDA_Imm(0xAA)
            .RTS()
            .End()
        ));

    [TestMethod]
    public async Task TestStackOperations()
        => await Verify(RelocToString(
            new Mos6510Assembler()
            .LabelForward(out var data)
            .LabelForward(out var dataHi)
            .LDA(dataHi)
            .PHA()
            .PLA()
            .LabelForward(out var modLDA)
            .STA(modLDA + 2)
            .Label(modLDA)
            .LDA(data.LowByte().ToAbsolute()) // LDA $00cc, high byte is modified above
            .RTS()
            .Label(data)
            .NOP()
            .Label(dataHi)
            .Append(data.HighByte())
            .NOP()
            .NOP()
            .NOP()
            .End()
        ));

    [TestMethod]
    public async Task TestRegisterTransfers()
        => await Verify(RelocToString(
            new Mos6510Assembler()
                .LabelForward(out var data)
                .LabelForward(out var dataHi)
                .LDA(dataHi)
                .TAX()
                .LDA_Imm(0)
                .TXA()
                .TAY()
                .LDA_Imm(0)
                .TYA()
                .LabelForward(out var modLDA)
                .STA(modLDA + 2)
                .Label(modLDA)
                .LDA(data.LowByte().ToAbsolute()) // LDA $00cc, high byte is modified above
                .RTS()
                .Label(data)
                .NOP()
                .Label(dataHi)
                .Append(data.HighByte())
                .NOP()
                .NOP()
                .NOP()
                .End()
        ));

    private string RelocToString(Mos6510Assembler asm, bool expectException = false)
    {
        var writer = new StringWriter();
        ushort targetRelocAddr = 0x2000;
        var targetZpRange = new RamZpRange(0x80, 0x10);


        var config = new CodeRelocationConfig()
        {
            ProgramAddress = asm.BaseAddress,
            ProgramBytes = asm.Buffer.ToArray()
        };

        var relocator = new CodeRelocator(config)
        {
            Diagnostics =
            {
                LogLevel = CodeRelocationDiagnosticKind.Trace
            },
            Testing = true
        };

        byte[]? relocatedBytes = null;
        try
        {
            relocator.RunSubroutineAt(asm.BaseAddress, 1000);

            relocatedBytes = relocator.Relocate(new CodeRelocationTarget()
            {
                Address = targetRelocAddr,
                ZpRange = targetZpRange
            });
        }
        catch (CodeRelocationException ex)
        {
            if (!expectException)
            {
                writer.WriteLine("Unexpected relocation exception:");
                writer.WriteLine(ex.ToString());
            }
            else
            {
                writer.WriteLine($"Expected relocation exception: {ex.Message}");
            }
        }

        if (relocatedBytes is not null)
        {
            relocator.PrintRelocationMap(writer);

            writer.WriteLine();
        }

        if (relocator.Diagnostics.Messages.Count > 0)
        {
            writer.WriteLine("Diagnostics:");
            foreach (var message in relocator.Diagnostics.Messages)
            {
                writer.WriteLine(message.ToString());
            }
        }
        else
        {
            writer.WriteLine("Diagnostics: (none)");
        }

        writer.WriteLine();

        var disasm = new Mos6510Disassembler(new Mos6510DisassemblerOptions()
        {
            BaseAddress = asm.BaseAddress,
            PrintAddress = true,
            PrintAssemblyBytes = true,
        });
        writer.WriteLine("Original:");
        var originalText = disasm.Disassemble(asm.Buffer);
        writer.WriteLine(originalText);

        if (relocatedBytes is not null)
        {
            disasm = new Mos6510Disassembler(new Mos6510DisassemblerOptions()
            {
                BaseAddress = targetRelocAddr,
                PrintAddress = true,
                PrintAssemblyBytes = true,
            });
            writer.WriteLine("Relocated:");
            var text = disasm.Disassemble(relocatedBytes);
            writer.WriteLine(text);
        }

        return writer.ToString();
    }
}