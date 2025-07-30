// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AsmMos6502.CodeGen;

internal class GeneratorApp
{
    private static readonly string GeneratedFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AsmMos6502", "generated"));
    private static readonly string GeneratedTestsFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AsmMos6502.Tests", "generated"));

    private readonly Dictionary<string, JsonAsm6502AddressingMode> _mapAddressingMode = new();

    public void Run()
    {
        if (!Directory.Exists(GeneratedFolderPath))
        {
            throw new DirectoryNotFoundException($"The directory '{GeneratedFolderPath}' does not exist. Please ensure the path is correct.");
        }

        if (!Directory.Exists(GeneratedTestsFolderPath))
        {
            throw new DirectoryNotFoundException($"The test directory '{GeneratedTestsFolderPath}' does not exist. Please ensure the path is correct.");
        }
        
        var model = JsonAsm6502Instructions.ReadJson("6502.json");

        var opcodes = model.Opcodes.Where(op => !op.Illegal).OrderBy(x => x.Opcode)
            .OrderBy(x => x.Name).ThenBy(x => x.AddressingMode).ToList();

        var modes = model.Modes;
        _mapAddressingMode.Clear();
        foreach (var mode in modes)
        {
            _mapAddressingMode[mode.Kind] = mode;
        }

        var modeMapping = GenerateAddressingModes(modes);
        GenerateOpCodes(opcodes);
        var mnemonics = opcodes.Select(op => op.Name).Distinct().OrderBy(name => name).ToList();
        GenerateMnemonics(mnemonics);
        GenerateTables(opcodes, modes, modeMapping, mnemonics);
        GenerateInstructionFactory(opcodes);
        GenerateAssemblerFactory(opcodes);
        GenerateAssemblerFactoryWithLabel(opcodes);
        GenerateAssemblerFactoryWithExpressions(opcodes);
        GenerateAssemblyTests(opcodes);
    }

    private Dictionary<string, int> GenerateAddressingModes(List<JsonAsm6502AddressingMode> addressingModes)
    {
        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502AddressingMode.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteSummary("Operand addressing modes.");
        writer.WriteLine("public enum Mos6502AddressingMode : byte");
        writer.OpenBraceBlock();
        writer.WriteSummary("Undefined mode");
        writer.WriteLine("Unknown = 0,");
        var mapNameToValue = new Dictionary<string, int>();
        for (var i = 0; i < addressingModes.Count; i++)
        {
            var mode = addressingModes[i];
            writer.WriteSummary($"{mode.Kind}");
            writer.WriteDoc([$"<remarks>Size: {BytesText(mode.SizeBytes)}, Cycles: {mode.Cycles}</remarks>"]);
            writer.WriteLine($"{mode.Kind} = {i + 1},");
            mapNameToValue[mode.Kind] = i + 1;
        }

        writer.CloseBraceBlock();
        return mapNameToValue;
    }

    private void GenerateOpCodes(List<JsonAsm6502Opcode> opcodes)
    {
        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502OpCode.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteSummary("6502 opcodes.");
        writer.WriteLine("public enum Mos6502OpCode : byte");
        writer.OpenBraceBlock();
        for (var i = 0; i < opcodes.Count; i++)
        {
            var opcode = opcodes[i];
            writer.WriteSummary($"{opcode.NameLong} - {opcode.Name}");
            writer.WriteDoc([$"<remarks>AddressingMode: {opcode.AddressingMode}</remarks>"]);
            writer.WriteLine($"{opcode.Name}_{opcode.AddressingMode} = {opcode.OpcodeHex},");
        }

        writer.CloseBraceBlock();
    }
    
    private void GenerateTables(List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes, Dictionary<string, int> mapAddressingModeToValue, List<string> mnemonics)
    {
        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502Tables.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteSummary("Internal tables to help decoding <see cref=\"Mos6502OpCode\"/>.");
        writer.WriteLine("internal static partial class Mos6502Tables");
        writer.OpenBraceBlock();

        {
            writer.WriteLine("private static ReadOnlySpan<byte> MapOpCodeToAddressingMode => new byte[256]");
            writer.OpenBraceBlock();
            for (var i = 0; i < 0x100; i++)
            {
                var opcode = opcodes.FirstOrDefault(x => x.Opcode == i);
                writer.Write(opcode is null ? "0x00, " : $"0x{mapAddressingModeToValue[opcode.AddressingMode]:X2}, ");
                if ((i + 1) % 16 == 0)
                {
                    writer.WriteLine();
                }
            }

            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }


        {
            writer.WriteLine($"private static ReadOnlySpan<byte> MapOpCodeToMnemonic => new byte[256]");
            writer.OpenBraceBlock();
            for (var i = 0; i < 0x100; i++)
            {
                var opcode = opcodes.FirstOrDefault(x => x.Opcode == i);
                if (opcode is null)
                {
                    writer.WriteLine($"{0,-2}, // [0x{i:X2}] No opcodes for this instruction");
                }
                else
                {
                    var mnemonicIndex = mnemonics.FindIndex(x => x == opcode.Name);
                    writer.WriteLine($"{mnemonicIndex + 1,-2}, // [0x{i:X2}] {opcode.Name}_{opcode.AddressingMode} ");
                }
            }
            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }

        {
            writer.WriteLine($"private static readonly string[] MapMnemonicToTextUppercase = new string[{mnemonics.Count + 1}]");
            writer.OpenBraceBlock();
            writer.WriteLine("\"???\", // Unknown mnemonic");
            foreach (var mnemonic in mnemonics)
            {
                writer.WriteLine($"\"{mnemonic.ToUpperInvariant()}\", // {mnemonic}");
            }
            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }

        {
            writer.WriteLine($"private static readonly string[] MapMnemonicToTextLowercase = new string[{mnemonics.Count + 1}]");
            writer.OpenBraceBlock();
            writer.WriteLine("\"???\", // Unknown mnemonic");
            foreach (var mnemonic in mnemonics)
            {
                writer.WriteLine($"\"{mnemonic.ToLowerInvariant()}\", // {mnemonic}");
            }
            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }
        
        {

            writer.WriteLine("private static ReadOnlySpan<byte> MapOpCodeToCycles => new byte[256]");
            writer.OpenBraceBlock();
            for (var i = 0; i < 0x100; i++)
            {
                var opcode = opcodes.FirstOrDefault(x => x.Opcode == i);
                writer.Write(opcode is null ? "0, " : $"{opcode.Cycles}, ");
                if ((i + 1) % 16 == 0)
                {
                    writer.WriteLine();
                }
            }

            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }

        {
            writer.WriteLine($"private static ReadOnlySpan<byte> MapAddressingModeToBytes => new byte[16]");
            writer.OpenBraceBlock();
            writer.WriteLine("0, // Undefined");
            Debug.Assert(modes.Count == 13);
            foreach (var mode in modes)
            {
                writer.WriteLine($"{mode.SizeBytes}, // {mode.Kind}");
            }

            writer.WriteLine("0, // Undefined");
            writer.WriteLine("0, // Undefined");
            writer.CloseBraceBlockStatement();
        }

        {
            writer.WriteLine($"private static ReadOnlySpan<byte> MapAddressingModeToCycles => new byte[16]");
            writer.OpenBraceBlock();
            writer.WriteLine("0, // Undefined");
            foreach (var mode in modes)
            {
                writer.WriteLine($"{mode.Cycles}, // {mode.Kind}");
            }

            writer.WriteLine("0, // Undefined");
            writer.WriteLine("0, // Undefined");
            writer.CloseBraceBlockStatement();
        }


        writer.CloseBraceBlock();
    }
    
    private void GenerateMnemonics(List<string> mnemonics)
    {
        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502Mnemonic.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteSummary("6502 mnemonics.");
        writer.WriteLine("public enum Mos6502Mnemonic : byte");
        writer.OpenBraceBlock();
        writer.WriteSummary("Undefined mnemonic.");
        writer.WriteLine("Unknown = 0,");
        for (var i = 0; i < mnemonics.Count; i++)
        {
            var mnemonic = mnemonics[i];
            writer.WriteSummary($"{mnemonic}.");
            writer.WriteLine($"{mnemonic} = {i + 1},");
        }
        writer.CloseBraceBlock();
    }

    private const string Mos6502RegisterA = nameof(Mos6502RegisterA);
    private const string Mos6502RegisterX = nameof(Mos6502RegisterX);
    private const string Mos6502RegisterY = nameof(Mos6502RegisterY);


    private record Operand6502(string Name, string Type, string? DefaultValue = null, string? Attributes = null, bool IsDebug = false)
    {
        public string? ArgumentPath { get; set; }

        public bool IsArgumentOnly { get; set; }
        
        public string ParameterDeclaration()
        {
            var builder = new StringBuilder();
            if (Attributes is not null)
            {
                builder.Append(Attributes);
                builder.Append(' ');
            }

            builder.Append(Type);

            builder.Append(' ');

            builder.Append(Name);

            if (DefaultValue is not null)
            {
                builder.Append(" = ");
                builder.Append(DefaultValue);
            }

            return builder.ToString();
        }
    }

    private enum OperandValueKind
    {
        None,
        Immediate,
        Relative,
        Zp,
        Address,
        Indirect,
        Implied,
        Accumulator,
        ZpX,
        ZpY,
        AddressX,
        AddressY,
        IndirectX,
        IndirectY
    }

    private record OpcodeSignature(JsonAsm6502Opcode Opcode, string Name, int OperandCount, List<Operand6502> Arguments, OperandValueKind OperandKind)
    {
        public void AddDebugAttributes()
        {
            Arguments.Add(new Operand6502("debugFilePath", "string", "\"\"", "[CallerFilePath]"));
            Arguments.Add(new Operand6502("debugLineNumber", "int", "0", "[CallerLineNumber]"));
        }

        public string Signature => $"{Name}({string.Join(", ", Arguments.Where(x => !x.IsArgumentOnly).Select(arg => arg.ParameterDeclaration()))})";
    }
    
    private OpcodeSignature GetOpcodeSignature(JsonAsm6502Opcode opcode)
    {
        var opName = opcode.Name;
        var operandKind = OperandValueKind.None;
        List<Operand6502> argumentTypes;
        switch (opcode.AddressingMode)
        {
            case "Implied":
                argumentTypes = new List<Operand6502>();
                operandKind = OperandValueKind.Implied;
                break;
            case "Relative":
                argumentTypes = [new("relativeAddress", "sbyte")];
                operandKind = OperandValueKind.Relative;
                break;
            case "Accumulator":
                argumentTypes = [new("accumulator", Mos6502RegisterA, "Mos6502RegisterA.A")];
                operandKind = OperandValueKind.Accumulator;
                break;
            case "Immediate":
                argumentTypes = ( [new("immediate", "byte")]);
                opName = $"{opName}_Imm";
                operandKind = OperandValueKind.Immediate;
                break;
            case "ZeroPage":
                argumentTypes = ( [new("zeroPage", "byte")]);
                operandKind = OperandValueKind.Zp;
                break;
            case "ZeroPageX":
                argumentTypes = ( [new("zeroPage", "byte"), new("x", Mos6502RegisterX)]);
                operandKind = OperandValueKind.ZpX;
                break;
            case "ZeroPageY":
                argumentTypes = ( [new("zeroPage", "byte"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.ZpY;
                break;
            case "Absolute":
                argumentTypes = ( [new("address", "ushort")]);
                operandKind = OperandValueKind.Address;
                break;
            case "AbsoluteX":
                argumentTypes = ( [new("address", "ushort"), new("x", Mos6502RegisterX)]);
                operandKind = OperandValueKind.AddressX;
                break;
            case "AbsoluteY":
                argumentTypes = ( [new("address", "ushort"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.AddressY;
                break;
            case "Indirect":
                argumentTypes = ( [new("indirect", "Mos6502Indirect")]);
                operandKind = OperandValueKind.Indirect;
                break;
            case "IndirectX":
                argumentTypes = ( [new("indirect", "Mos6502IndirectX")]);
                operandKind = OperandValueKind.IndirectX;
                break;
            case "IndirectY":
                argumentTypes = ( [new("indirect", "Mos6502IndirectY"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.IndirectY;
                break;
            default:
                throw new NotSupportedException($"Addressing mode '{opcode.AddressingMode}' is not supported for opcode '{opcode.Name}'");
        }

        var operandCount = argumentTypes.Count;
        return new(opcode, opName, operandCount, argumentTypes, operandKind);
    }

    
    private void GenerateInstructionFactory(List<JsonAsm6502Opcode> opcodes)
    {

        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502InstructionFactory.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteSummary("Factory for all 6502 instructions.");
        writer.WriteLine("public static partial class Mos6502InstructionFactory");
        writer.OpenBraceBlock();

        foreach (var opcode in opcodes)
        {
            var opcodeSignature = GetOpcodeSignature(opcode);
            var mode = _mapAddressingMode[opcode.AddressingMode];
            writer.WriteSummary($"Creates the {opcode.Name} instruction ({opcode.OpcodeHex}) instruction with addressing mode {opcode.AddressingMode}.");
            writer.WriteDoc([$"<remarks>{opcode.NameLong}. Cycles: {opcode.Cycles}, Size: {BytesText(mode.SizeBytes)}</remarks>"]);
            writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            writer.Write($"public static Mos6502Instruction {opcodeSignature.Signature} => new (Mos6502OpCode.{opcode.Name}_{opcode.AddressingMode}");

            switch (opcodeSignature.OperandKind)
            {
                case OperandValueKind.None:
                    break;
                case OperandValueKind.Accumulator:
                    break;
                case OperandValueKind.Indirect:
                case OperandValueKind.IndirectX:
                case OperandValueKind.IndirectY:
                    writer.Write($", {opcodeSignature.Arguments[0].Name}.Address");
                    break;
                default:
                    if (opcodeSignature.Arguments.Count > 0)
                    {
                        writer.Write($", {opcodeSignature.Arguments[0].Name}");
                    }
                    break;
            }

            writer.WriteLine($");");

            writer.WriteLine();
        }

        writer.CloseBraceBlock();
    }

    private void GenerateAssemblerFactory(List<JsonAsm6502Opcode> opcodes)
    {
        // Generate all assembler instruction
        GenerateAssemblerFactoryGeneric("Mos6502Assembler",
            opcodes,
            opcodeSignature => true,
            opcodeSignature =>
            {
                opcodeSignature.AddDebugAttributes();
            }
        );
    }

    private void GenerateAssemblerFactoryWithLabel(List<JsonAsm6502Opcode> opcodes)
    {
        GenerateAssemblerFactoryGeneric("Mos6502Assembler_WithLabels",
            opcodes,
            opcodeSignature => opcodeSignature.OperandKind == OperandValueKind.Address ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressX ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressY ||
                               opcodeSignature.OperandKind == OperandValueKind.Relative ||
                               opcodeSignature.OperandKind == OperandValueKind.Indirect,
            opcodeSignature =>
            {
                var originalOperand = opcodeSignature.Arguments[0];
                var operand0 = new Operand6502("address", opcodeSignature.OperandKind == OperandValueKind.Indirect ? opcodeSignature.Arguments[0].Type.Replace("Mos6502Indirect", "Mos6502IndirectLabel") : "Mos6502Label");
                operand0.ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"new {originalOperand.Type}({operand0.Name}.ZpLabel.Address)" : $"({originalOperand.Type}){operand0.Name}.Address";

                opcodeSignature.Arguments[0] = operand0;

                opcodeSignature.Arguments.Add(new Operand6502(operand0.Name, operand0.Type)
                {
                    ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"{operand0.Name}.ZpLabel" : null,
                    IsArgumentOnly = true
                } ); // Pass the label as an additional argument to the assembler AddInstruction
                opcodeSignature.AddDebugAttributes();
            });
    }

    private void GenerateAssemblerFactoryWithExpressions(List<JsonAsm6502Opcode> opcodes)
    {
        GenerateAssemblerFactoryGeneric("Mos6502Assembler_WithExpressions",
            opcodes,
            opcodeSignature =>
                                opcodeSignature.OperandKind == OperandValueKind.Immediate ||
                                opcodeSignature.OperandKind == OperandValueKind.Address ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressX ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressY ||
                               opcodeSignature.OperandKind == OperandValueKind.Indirect,
            opcodeSignature =>
            {
                var originalOperand = opcodeSignature.Arguments[0];
                var operand0 = new Operand6502(opcodeSignature.Arguments[0].Name, opcodeSignature.OperandKind == OperandValueKind.Indirect ? "Expressions.Mos6502ExpressionIndirect" : opcodeSignature.OperandKind == OperandValueKind.Immediate ? "Expressions.Mos6502ExpressionU8" : "Expressions.Mos6502ExpressionU16");
                opcodeSignature.Arguments[0] = operand0;
                operand0.ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"new {originalOperand.Type}(0)" : $"({originalOperand.Type})0";

                opcodeSignature.Arguments.Add(new Operand6502(operand0.Name, operand0.Type) { IsArgumentOnly = true }); // Pass the label as an additional argument to the assembler AddInstruction
                opcodeSignature.AddDebugAttributes();
            });
    }

    private void GenerateAssemblerFactoryGeneric(string fileName, List<JsonAsm6502Opcode> opcodes, Func<OpcodeSignature, bool> filter, Action<OpcodeSignature> modify)
    {

        var filePath = Path.Combine(GeneratedFolderPath, $"{fileName}.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteLine("partial class Mos6502Assembler");
        writer.OpenBraceBlock();

        var mnemonicToSignatureToOpcodes = new Dictionary<string, Dictionary<string, OpcodeSignature>>();

        foreach (var opcode in opcodes)
        {
            var opcodeSignature = GetOpcodeSignature(opcode);
            if (filter(opcodeSignature))
            {
                modify(opcodeSignature);

                if (!mnemonicToSignatureToOpcodes.TryGetValue(opcode.Name, out var opcodeWithAddress))
                {
                    opcodeWithAddress = new Dictionary<string, OpcodeSignature>();
                    mnemonicToSignatureToOpcodes[opcode.Name] = opcodeWithAddress;
                }

                opcodeWithAddress[opcodeSignature.Signature] = opcodeSignature;
            }
        }

        foreach (var mnemonicPair in mnemonicToSignatureToOpcodes.OrderBy(x => x.Key))
        {
            var mnemonic = mnemonicPair.Key;
            var signatureToOpcodes = mnemonicPair.Value;
            foreach (var signaturePair in signatureToOpcodes.OrderBy(x => x.Key))
            {
                var signature = signaturePair.Key;
                var opcodeSignature = signaturePair.Value;
                var mode = _mapAddressingMode[opcodeSignature.Opcode.AddressingMode];
                
                writer.WriteSummary($"{opcodeSignature.Opcode.NameLong}. {mnemonic} instruction ({opcodeSignature.Opcode.OpcodeHex}) with addressing mode {opcodeSignature.Opcode.AddressingMode}.");
                writer.WriteDoc([$"<remarks>Cycles: {opcodeSignature.Opcode.Cycles}, Size: {BytesText(mode.SizeBytes)}</remarks>"]);
                writer.WriteLine($"public Mos6502Assembler {signature}");
                writer.Indent();

                writer.Write($"=> AddInstruction(Mos6502InstructionFactory.{opcodeSignature.Name}(");

                if (opcodeSignature.OperandKind != OperandValueKind.Accumulator)
                {
                    for (int i = 0; i < opcodeSignature.OperandCount; i++)
                    {
                        var arg = opcodeSignature.Arguments[i];
                        if (i > 0)
                        {
                            writer.Write(", ");
                        }

                        writer.Write(arg.ArgumentPath ?? arg.Name);
                    }
                }

                writer.Write(")");

                for (int i = opcodeSignature.OperandCount; i < opcodeSignature.Arguments.Count; i++)
                {
                    var arg = opcodeSignature.Arguments[i];
                    writer.Write($", {arg.ArgumentPath ?? arg.Name}");
                }

                writer.WriteLine(");");

                writer.UnIndent();
            }
        }

        writer.CloseBraceBlock();
    }

    private static IEnumerable<string> GetTestVariations(JsonAsm6502Opcode opcode)
    {
        switch (opcode.AddressingMode)
        {
            case "Implied":
                yield return "";
                break;
            case "Relative":
                yield return "0x10"; // Relative address for test
                yield return "-37";
                break;
            case "Accumulator":
                yield return $"A";
                yield return $"";
                break;
            case "Immediate":
                yield return "0x01"; // Immediate value for test
                yield return "0x42";
                yield return "0xFF";
                break;
            case "ZeroPage":
                yield return "0x03"; // Zero page address for test
                yield return "0x20";
                yield return "0xFE";
                break;
            case "ZeroPageX":
                yield return "0x02, X"; // Zero page X address for test
                yield return "0x30, X";
                yield return "0xFB, X";
                break;
            case "ZeroPageY":
                yield return "0x01, Y"; // Zero page Y address for test
                yield return "0x40, Y";
                yield return "0xFC, Y";
                break;
            case "Absolute":
                yield return "0x1234"; // Absolute address for test
                yield return "0xFF01";
                break;
            case "AbsoluteX":
                yield return "0x1234, X"; // Absolute X address for test
                yield return "0xFF02, X";
                break;
            case "AbsoluteY":
                yield return "0x1234, Y"; // Absolute Y address for test
                yield return "0xFF03, Y";
                break;
            case "Indirect":
                yield return $"_[0x1234]"; // Indirect address for test
                yield return $"_[0xFF04]";
                break;
            case "IndirectX":
                yield return $"_[0x05, X]"; // Indirect X address for test
                yield return $"_[0x20, X]";
                yield return $"_[0xFF, X]";
                break;
            case "IndirectY":
                yield return $"_[0x06], Y"; // Indirect Y address for test
                yield return $"_[0x30], Y";
                yield return $"_[0xFE], Y";
                break;
            default:
                throw new NotSupportedException($"Addressing mode '{opcode.AddressingMode}' is not supported for opcode '{opcode.Name}'");
        }
    }
    
    private void GenerateAssemblyTests(List<JsonAsm6502Opcode> opcodes)
    {
        using var writer = CreateCodeWriter("Mos6502AssemblerTests.gen.cs", true);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine("using static AsmMos6502.Mos6502Factory;");
        writer.WriteLine();

        writer.WriteLine("namespace AsmMos6502.Tests;");
        writer.WriteLine();
        writer.WriteLine("[TestClass]");
        writer.WriteLine("public partial class Mos6502AssemblerTests");
        writer.OpenBraceBlock();

        foreach (var opcode in opcodes)
        {
            writer.WriteLine("[TestMethod]");
            writer.WriteLine($"public async Task {opcode.Name}_{opcode.AddressingMode}()");
            writer.OpenBraceBlock();

            writer.WriteLine("using var asm = CreateAsm()");
            writer.Indent();

            var opcodeSignature = GetOpcodeSignature(opcode);
            foreach (var testItem in GetTestVariations(opcode))
            {
                writer.WriteLine($".{opcodeSignature.Name}({testItem})");
            }

            writer.WriteLine(".End();");
            writer.UnIndent();

            writer.WriteLine("await VerifyAsm(asm);");

            writer.CloseBraceBlock();

            writer.WriteLine();
        }

        {
            // All tests in a single method for simplicity and checking with existing assemblers
            writer.WriteLine("[TestMethod]");
            writer.WriteLine($"public async Task AllInstructions()");
            writer.OpenBraceBlock();
            writer.WriteLine("using var asm = CreateAsm()");
            writer.Indent();
            foreach (var opcode in opcodes)
            {
                var opcodeSignature = GetOpcodeSignature(opcode);
                foreach (var testItem in GetTestVariations(opcode))
                {
                    writer.WriteLine($".{opcodeSignature.Name}({testItem})");
                }
            }

            writer.WriteLine(".End();");
            writer.UnIndent();

            writer.WriteLine("await VerifyAsm(asm);");

            writer.CloseBraceBlock();

            writer.WriteLine();
        }

        writer.CloseBraceBlock();
    }
    
    private CodeWriter CreateCodeWriter(string fileName, bool test = false)
    {
        var filePath = Path.Combine(test ? GeneratedTestsFolderPath : GeneratedFolderPath, fileName);
        var writer = new CodeWriter(new StreamWriter(filePath), autoDispose: true);
        writer.WriteLine("// Copyright (c) Alexandre Mutel. All rights reserved.");
        writer.WriteLine("// Licensed under the BSD-Clause 2 license.");
        writer.WriteLine("// See license.txt file in the project root for full license information.");
        writer.WriteLine("// ------------------------------------------------------------------------------");
        writer.WriteLine("// This code was generated by AsmMos6502.CodeGen.");
        writer.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        writer.WriteLine("//     the code is regenerated.");
        writer.WriteLine("// ------------------------------------------------------------------------------");
        writer.WriteLine("// ReSharper disable All");
        writer.WriteLine("// ------------------------------------------------------------------------------");
        return writer;
    }

    private static string BytesText(int value) => value > 1 ? $"{value} bytes" : $"{value} byte";
}
