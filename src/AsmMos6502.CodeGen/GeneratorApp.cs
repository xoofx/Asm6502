// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;

namespace AsmMos6502.CodeGen;

internal class GeneratorApp
{
    private static readonly string GeneratedFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AsmMos6502", "generated"));

    public void Run()
    {
        if (!Directory.Exists(GeneratedFolderPath))
        {
            throw new DirectoryNotFoundException($"The directory '{GeneratedFolderPath}' does not exist. Please ensure the path is correct.");
        }

        var model = JsonAsm6502Instructions.ReadJson("6502.json");

        var opcodes = model.Opcodes.Where(op => !op.Illegal).OrderBy(x => x.Opcode)
            .OrderBy(x => x.Name).ThenBy(x => x.AddressingMode).ToList();

        var modes = model.Modes;

        var modeMapping = GenerateAddressingModes(modes);
        GenerateOpCodes(opcodes);
        var mnemonics = opcodes.Select(op => op.Name).Distinct().OrderBy(name => name).ToList();
        GenerateMnemonics(mnemonics);
        GenerateTables(opcodes, modes, modeMapping, mnemonics);
        GenerateInstructionFactory(opcodes, modes);
        GenerateAssemblerFactory(opcodes, modes);
        GenerateAssemblerFactoryWithLabel(opcodes, modes);
    }

    private static Dictionary<string, int> GenerateAddressingModes(List<JsonAsm6502AddressingMode> addressingModes)
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
            writer.WriteDoc([$"<remarks>Size: {mode.SizeBytes} bytes, Cycles: {mode.Cycles}</remarks>"]);
            writer.WriteLine($"{mode.Kind} = {i + 1},");
            mapNameToValue[mode.Kind] = i + 1;
        }

        writer.CloseBraceBlock();
        return mapNameToValue;
    }

    private static void GenerateOpCodes(List<JsonAsm6502Opcode> opcodes)
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


    private static void GenerateTables(List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes, Dictionary<string, int> mapAddressingModeToValue, List<string> mnemonics)
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
    
    private static void GenerateMnemonics(List<string> mnemonics)
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


    private record Operand6502(string Name, string Type, string? DefaultValue = null);

    private enum OperandValueKind
    {
        None,
        Relative,
        Zp,
        Address,
        Indirect,
    }
    
    private static (string Name, List<Operand6502> Arguments, string Signature, OperandValueKind OperandKind) GetInstructionSignature(JsonAsm6502Opcode opcode)
    {
        var opName = opcode.Name;
        var operandKind = OperandValueKind.None;
        List<Operand6502> argumentTypes;
        switch (opcode.AddressingMode)
        {
            case "Implied":
                argumentTypes = new List<Operand6502>();
                break;
            case "Relative":
                argumentTypes = [new("relativeAddress", "sbyte")];
                operandKind = OperandValueKind.Relative;
                break;
            case "Accumulator":
                argumentTypes = [new("accumulator", Mos6502RegisterA, "Mos6502RegisterA.A")];
                break;
            case "Immediate":
                argumentTypes = ( [new("immediate", "byte")]);
                opName = $"{opName}_Imm";
                break;
            case "ZeroPage":
                argumentTypes = ( [new("zeroPage", "byte")]);
                operandKind = OperandValueKind.Zp;
                break;
            case "ZeroPageX":
                argumentTypes = ( [new("zeroPage", "byte"), new("x", Mos6502RegisterX)]);
                operandKind = OperandValueKind.Zp;
                break;
            case "ZeroPageY":
                argumentTypes = ( [new("zeroPage", "byte"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.Zp;
                break;
            case "Absolute":
                argumentTypes = ( [new("address", "ushort")]);
                operandKind = OperandValueKind.Address;
                break;
            case "AbsoluteX":
                argumentTypes = ( [new("address", "ushort"), new("x", Mos6502RegisterX)]);
                operandKind = OperandValueKind.Address;
                break;
            case "AbsoluteY":
                argumentTypes = ( [new("address", "ushort"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.Address;
                break;
            case "Indirect":
                argumentTypes = ( [new("indirect", "Mos6502Indirect")]);
                operandKind = OperandValueKind.Indirect;
                break;
            case "IndirectX":
                argumentTypes = ( [new("indirect", "Mos6502IndirectX")]);
                operandKind = OperandValueKind.Indirect;
                break;
            case "IndirectY":
                argumentTypes = ( [new("indirect", "Mos6502IndirectY"), new("y", Mos6502RegisterY)]);
                operandKind = OperandValueKind.Indirect;
                break;
            default:
                throw new NotSupportedException($"Addressing mode '{opcode.AddressingMode}' is not supported for opcode '{opcode.Name}'");
        }

        var signature = $"{opName}({string.Join(", ", argumentTypes.Select(arg => $"{arg.Type} {arg.Name}{(arg.DefaultValue != null?$" = {arg.DefaultValue}":"")}"))})";
        return (opName, argumentTypes, signature, operandKind);
    }

    
    private static void GenerateInstructionFactory(List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes)
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
            var (name, arguments, signature, operandKind) = GetInstructionSignature(opcode);
            var mode = modes.First(x => x.Kind == opcode.AddressingMode);
            writer.WriteSummary($"Creates the {opcode.Name} instruction ({opcode.OpcodeHex}) instruction with addressing mode {opcode.AddressingMode}.");
            writer.WriteDoc([$"<remarks>{opcode.NameLong}. Cycles: {opcode.Cycles}, Size: {mode.SizeBytes} bytes</remarks>"]);
            writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            writer.Write($"public static Mos6502Instruction {signature} => new (Mos6502OpCode.{opcode.Name}_{opcode.AddressingMode}");

            switch (operandKind)
            {
                case OperandValueKind.None:
                    break;
                case OperandValueKind.Indirect:
                    writer.Write($", {arguments[0].Name}.Address");
                    break;
                default:
                    writer.Write($", {arguments[0].Name}");
                    break;
            }

            writer.WriteLine($");");

            writer.WriteLine();
        }

        writer.CloseBraceBlock();
    }

    private static void GenerateAssemblerFactory(List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes)
    {

        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502Assembler.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteLine("partial class Mos6502Assembler");
        writer.OpenBraceBlock();
        
        foreach (var opcode in opcodes)
        {
            var (name, arguments, signature, operandKind) = GetInstructionSignature(opcode);
            var mode = modes.First(x => x.Kind == opcode.AddressingMode);
            writer.WriteSummary($"{opcode.NameLong}. {opcode.Name} instruction ({opcode.OpcodeHex}) with addressing mode {opcode.AddressingMode}.");
            writer.WriteDoc([$"<remarks>Cycles: {opcode.Cycles}, Size: {mode.SizeBytes} bytes</remarks>"]);
            writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            writer.Write($"public Mos6502Assembler {signature} => AddInstruction(Mos6502InstructionFactory.{name}(");

            switch (operandKind)
            {
                case OperandValueKind.Indirect:
                    writer.Write($"{arguments[0].Name}");
                    break;
                default:
                    if (arguments.Count > 0)
                    {
                        writer.Write($"{arguments[0].Name}");
                    }

                    break;
            }
            if (arguments.Count == 2)
            {
                writer.Write($", {arguments[1].Name}");
            }

            writer.WriteLine($"));");

            writer.WriteLine();
        }

        writer.CloseBraceBlock();
    }

    private static void GenerateAssemblerFactoryWithLabel(List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes)
    {

        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502Assembler_WithLabels.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace AsmMos6502;");
        writer.WriteLine();
        writer.WriteLine("partial class Mos6502Assembler");
        writer.OpenBraceBlock();

        var mnemonicToSignatureToOpcodes = new Dictionary<string, Dictionary<string, (JsonAsm6502Opcode, List<Operand6502>)>>();

        foreach (var opcode in opcodes)
        {
            var (name, arguments, _, operandKind) = GetInstructionSignature(opcode);
            if (operandKind != OperandValueKind.None && operandKind != OperandValueKind.Zp && opcode.AddressingMode != "IndirectX" && opcode.AddressingMode != "IndirectY")
            {
                arguments[0] = new Operand6502("address", operandKind == OperandValueKind.Indirect ? arguments[0].Type.Replace("Mos6502Indirect", "Mos6502IndirectLabel") : "Mos6502Label");
                var signature = $"{name}({string.Join(", ", arguments.Select(arg => $"{arg.Type} {arg.Name}{(arg.DefaultValue != null ? $" = {arg.DefaultValue}" : "")}"))})";

                if (!mnemonicToSignatureToOpcodes.TryGetValue(opcode.Name, out var opcodeWithAddress))
                {
                    opcodeWithAddress = new Dictionary<string, (JsonAsm6502Opcode, List<Operand6502>)>();
                    mnemonicToSignatureToOpcodes[opcode.Name] = opcodeWithAddress;
                }

                opcodeWithAddress[signature] = (opcode, arguments);
            }
        }

        foreach (var mnemonicPair in mnemonicToSignatureToOpcodes.OrderBy(x => x.Key))
        {
            var mnemonic = mnemonicPair.Key;
            var signatureToOpcodes = mnemonicPair.Value;
            foreach (var signaturePair in signatureToOpcodes.OrderBy(x => x.Key))
            {
                var signature = signaturePair.Key;
                var (opcode, operands) = signaturePair.Value;

                writer.WriteSummary($"{opcode.NameLong}. {mnemonic} instruction ({opcode.OpcodeHex}) with addressing mode {opcode.AddressingMode}.");
                writer.WriteLine($"public Mos6502Assembler {signature}");
                writer.Indent();

                var (_, originalParameterTypes, _, operandKind) = GetInstructionSignature(opcode);
                AppendInstructionWithLabel(opcode.Name, operands, originalParameterTypes[0], operandKind);
                writer.UnIndent();
            }
        }

        writer.CloseBraceBlock();


        void AppendInstructionWithLabel(string instructionName, List<Operand6502> arguments, Operand6502 originalAddressType, OperandValueKind operandKind)
        {
            writer.Write($"=> AddInstruction(Mos6502InstructionFactory.{instructionName}(");

            switch (operandKind)
            {
                case OperandValueKind.Indirect:
                    writer.Write($"new {originalAddressType.Type}((byte){arguments[0].Name}.ZpLabel.Address)");
                    break;
                default:
                    writer.Write($"({originalAddressType.Type}){arguments[0].Name}.Address");

                    break;
            }
            if (arguments.Count == 2)
            {
                writer.Write($", {arguments[1].Name}");
            }

            writer.WriteLine(operandKind == OperandValueKind.Indirect ? $"), {arguments[0].Name}.ZpLabel);" : $"), {arguments[0].Name});");
        }
    }

    private static CodeWriter CreateCodeWriter(string fileName)
    {
        var filePath = Path.Combine(GeneratedFolderPath, fileName);
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
}
