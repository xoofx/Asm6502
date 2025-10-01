// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace Asm6502.CodeGen;

internal class GeneratorApp
{
    private static readonly string GeneratedFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Asm6502", "generated"));
    private static readonly string GeneratedTestsFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Asm6502.Tests", "generated"));

    private readonly Dictionary<string, JsonAsm6502AddressingMode> _mapAddressingMode = new();

    public void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!Directory.Exists(GeneratedFolderPath))
        {
            throw new DirectoryNotFoundException($"The directory '{GeneratedFolderPath}' does not exist. Please ensure the path is correct.");
        }

        if (!Directory.Exists(GeneratedTestsFolderPath))
        {
            throw new DirectoryNotFoundException($"The test directory '{GeneratedTestsFolderPath}' does not exist. Please ensure the path is correct.");
        }
        
        var model = JsonAsm6502Instructions.ReadJson("6502.json");
        
        var opcodes6502 = model.Opcodes.Where(x => !x.W65c02 && !x.Illegal).OrderBy(x => x.Opcode)
            .OrderBy(x => x.Name).ThenBy(x => x.AddressingMode).ToList();
        opcodes6502.ForEach(x =>
        {
            x.UniqueName = x.Name;
            if (x.AddressingMode == "Immediate")
            {
                x.UniqueName = $"{x.Name}_Imm";
            }
            x.OpcodeUniqueName = $"{x.Name}_{x.AddressingMode}";
        });

        var illegalOpcodes = model.Opcodes.Where(x => !x.W65c02 && x.Illegal).OrderBy(x => x.Opcode)
            .OrderBy(x => x.Name).ThenBy(x => x.AddressingMode).ToList();

        // Append a unique suffix to illegal opcodes that have the same name and addressing mode
        for (var i = 0; i < illegalOpcodes.Count; i++)
        {
            var illegal = illegalOpcodes[i];

            illegal.UniqueName = illegal.Name;
            illegal.OpcodeUniqueName = $"{illegal.Name}_{illegal.AddressingMode}";
            bool opcodeNameIsAlreadyUsed = opcodes6502.Any(x => x.OpcodeUniqueName == illegal.OpcodeUniqueName);
            for (int j = 0; j < i; j++)
            {
                var previous = illegalOpcodes[j];
                if (previous.OpcodeUniqueName == illegal.OpcodeUniqueName)
                {
                    opcodeNameIsAlreadyUsed = true;
                    break;
                }
            }

            if (opcodeNameIsAlreadyUsed)
            {
                illegal.UniqueName = $"{illegal.Name}_{illegal.Opcode:X2}";
                if (illegal.AddressingMode == "Immediate")
                {
                    illegal.UniqueName = $"{illegal.UniqueName}_Imm";
                }
                illegal.OpcodeUniqueName = $"{illegal.Name}_{illegal.Opcode:X2}_{illegal.AddressingMode}";
            }
            else
            {
                if (illegal.AddressingMode == "Immediate")
                {
                    illegal.UniqueName = $"{illegal.Name}_Imm";
                }
            }
        }
        
        var opcodes6510 = opcodes6502.Concat(illegalOpcodes).ToList();
        
        var modes = model.Modes;
        modes.RemoveAt(modes.Count - 1); // Remove the W65c02 modes
        modes.RemoveAt(modes.Count - 1);

        Dictionary<string, JsonAsm6502AddressingMode> modeMapping = new();
        _mapAddressingMode.Clear();
        for (var i = 0; i < modes.Count; i++)
        {
            var mode = modes[i];
            _mapAddressingMode[mode.Kind] = mode;
            mode.Id = i + 1; // 0 is unknown
            modeMapping[mode.Kind] = mode;
        }

        GenerateAddressingModes(modes);
        var mnemonics6502Names = opcodes6502.Select(op => op.Name).Distinct().OrderBy(name => name).ToList();
        List<Mnemonic> mnemonics6502 = new();
        for (var i = 0; i < mnemonics6502Names.Count; i++)
        {
            var name = mnemonics6502Names[i];
            mnemonics6502.Add(new(mnemonics6502.Count + 1, name, false, false));
        }

        var illegalMnemonicsNames = illegalOpcodes.Where(op => mnemonics6502.All(x => x.Name != op.Name)).OrderBy(op => op.Name).ToList();
        List<Mnemonic> illegalMnemonics = new();
        for (var i = 0; i < illegalMnemonicsNames.Count; i++)
        {
            var op = illegalMnemonicsNames[i];
            var name = op.Name;
            if (illegalMnemonics.All(x => x.Name != name))
            {
                illegalMnemonics.Add(new(mnemonics6502.Count + illegalMnemonics.Count + 1, name, true, op.Unstable));
            }
        }
        
        var mnemonics6510 = mnemonics6502.Concat(illegalMnemonics).ToList();

        GenerateOpCodes(opcodes6502, "Mos6502OpCode", "6502 opcodes.");
        GenerateOpCodes(opcodes6510, "Mos6510OpCode", "6510 opcodes (6502 + illegal opcodes).");

        GenerateMnemonics(mnemonics6502, "Mos6502Mnemonic", "6502 mnemonics.");
        GenerateMnemonics(mnemonics6510, "Mos6510Mnemonic", "6510 mnemonics (6502 + illegals).");
        
        GenerateTables("Mos6502", opcodes6502, modes, modeMapping, mnemonics6502);
        GenerateTables("Mos6510", opcodes6510, modes, modeMapping, mnemonics6510);

        GenerateInstructionFactory("Mos6502", opcodes6502);
        GenerateInstructionFactory("Mos6510", opcodes6510);

        GenerateAssemblerFactory("Mos6502", opcodes6502);
        GenerateAssemblerFactory("Mos6510", opcodes6510);

        GenerateAssemblerFactoryWithEnums("Mos6502", opcodes6502);
        GenerateAssemblerFactoryWithEnums("Mos6510", opcodes6510);

        GenerateAssemblerFactoryWithExpressions("Mos6502", opcodes6502);
        GenerateAssemblerFactoryWithExpressions("Mos6510", opcodes6510);

        GenerateAssemblyTests("Mos6502", opcodes6502);
        GenerateAssemblyTests("Mos6510", opcodes6510);


        Console.WriteLine("### 6502 Instructions");
        Console.WriteLine();
        Console.WriteLine("The following instructions are supported by the `Mos6502Assembler` and `Mos6510Assembler` classes:");
        Console.WriteLine();
        Console.WriteLine("| Byte | Instruction | C# Syntax    | Description |");
        Console.WriteLine("|------|-------------|--------------|-------------|");
        foreach (var opcode in opcodes6510.Where(x => !x.Illegal))
        {
            Console.WriteLine($"| `{opcode.OpcodeHex}` | {OpcodeWithLink(opcode)} | `asm.{opcode.UniqueName}{AddressingModeToSyntaxCSharp(opcode.AddressingMode)};` | {opcode.NameLong} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 6510 Additional Illegal Instructions");
        Console.WriteLine();
        Console.WriteLine("The following instructions are supported by the `Mos6510Assembler` class:");
        Console.WriteLine();
        Console.WriteLine("| Byte | Instruction | C# Syntax    | Aliases | Description | Unstable |");
        Console.WriteLine("|------|-------------|--------------|---------|-------------|----------|");
        foreach (var opcode in opcodes6510.Where(x => x.Illegal))
        {
            Console.WriteLine($"| `{opcode.OpcodeHex}` | {OpcodeWithLink(opcode)} | `asm.{opcode.UniqueName}{AddressingModeToSyntaxCSharp(opcode.AddressingMode)};` | {string.Join(", ", opcode.AllNames.Select(x => $"`{x}`"))} | {opcode.NameLong} | {(opcode.Unstable?"❌" : "")} |");
        }
    }

    private static string AddressingModeToSyntax(string mode)
    {
        return mode switch
        {
            "Implied" => "",
            "Accumulator" => " A",
            "Immediate" => " #value",
            "ZeroPage" => " zp",
            "ZeroPageX" => " zp, X",
            "ZeroPageY" => " zp, Y",
            "Absolute" => " address",
            "AbsoluteX" => " address, X",
            "AbsoluteY" => " address, Y",
            "Indirect" => " (address)",
            "IndirectX" => " (zp, X)",
            "IndirectY" => " (zp), Y",
            "Relative" => " label",
            _ => throw new NotSupportedException($"Addressing mode '{mode}' is not supported."),
        };
    }

    private static string OpcodeWithLink(JsonAsm6502Opcode opcode)
    {
        var operand = opcode.AddressingMode == "Implied" ? string.Empty : $" `{AddressingModeToSyntax(opcode.AddressingMode)}`";
        return $"[`{opcode.Name}`]({opcode.OnlineDocumentation[^1]}){operand}";
    }

    private static string AddressingModeToSyntaxCSharp(string mode)
    {
        return mode switch
        {
            "Implied" => "()",
            "Accumulator" => "(A)",
            "Immediate" => "(value)",
            "ZeroPage" => "(zp)",
            "ZeroPageX" => "(zp, X)",
            "ZeroPageY" => "(zp, Y)",
            "Absolute" => "(address)",
            "AbsoluteX" => "(address, X)",
            "AbsoluteY" => "(address, Y)",
            "Indirect" => "(_[address])",
            "IndirectX" => "(_[zp, X])",
            "IndirectY" => "(_[zp], Y)",
            "Relative" => "(label)",
            _ => throw new NotSupportedException($"Addressing mode '{mode}' is not supported."),
        };
    }

    // 6510
    // Mos6510Mnemonic.gen
    // Mos6510OpCode.gen
    // Mos6510Tables.gen
    // Mos6510Assembler.gen => Inherit from Mos6502Assembler
    // Mos6510Assembler_WithExpressions.gen

    private void GenerateAddressingModes(List<JsonAsm6502AddressingMode> addressingModes)
    {
        var filePath = Path.Combine(GeneratedFolderPath, "Mos6502AddressingMode.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteSummary("Operand addressing modes.");
        writer.WriteLine("public enum Mos6502AddressingMode : byte");
        writer.OpenBraceBlock();
        writer.WriteSummary("Undefined mode");
        writer.WriteLine("Unknown = 0,");
        for (var i = 0; i < addressingModes.Count; i++)
        {
            var mode = addressingModes[i];
            writer.WriteSummary($"{mode.Kind}");
            writer.WriteDoc([$"<remarks>Size: {BytesText(mode.SizeBytes)}, Cycles: {mode.Cycles}</remarks>"]);
            writer.WriteLine($"{mode.Kind} = {i + 1},");
        }

        writer.CloseBraceBlock();
    }

    private void GenerateOpCodes(List<JsonAsm6502Opcode> opcodes, string className, string comment)
    {
        var filePath = Path.Combine(GeneratedFolderPath, $"{className}.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteSummary(comment);
        writer.WriteLine($"public enum {className} : byte");
        writer.OpenBraceBlock();
        for (var i = 0; i < opcodes.Count; i++)
        {
            var opcode = opcodes[i];
            writer.WriteSummary($"{opcode.NameLong} - {opcode.Name}");
            if (opcode.Illegal)
            {
                var unstable = opcode.Unstable ? " (unstable)" : string.Empty;
                writer.WriteDoc([$"<remarks>AddressingMode: {opcode.AddressingMode}. This is an illegal{unstable} opcode.</remarks>"]);
            }
            else
            {
                writer.WriteDoc([$"<remarks>AddressingMode: {opcode.AddressingMode}</remarks>"]);
            }
            writer.WriteLine($"{opcode.OpcodeUniqueName} = {opcode.OpcodeHex},");
        }

        writer.CloseBraceBlock();
    }
    
    private void GenerateTables(string className, List<JsonAsm6502Opcode> opcodes, List<JsonAsm6502AddressingMode> modes, Dictionary<string, JsonAsm6502AddressingMode> modeMapping, List<Mnemonic> mnemonics)
    {
        var filePath = Path.Combine(GeneratedFolderPath, $"{className}Tables.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteSummary($"Internal tables to help decoding <see cref=\"{className}OpCode\"/>.");
        writer.WriteLine($"internal static partial class {className}Tables");
        writer.OpenBraceBlock();

        {
            writer.WriteLine("private static ReadOnlySpan<byte> MapOpCodeToAddressingMode => new byte[256]");
            writer.OpenBraceBlock();
            for (var i = 0; i < 0x100; i++)
            {
                var opcode = opcodes.FirstOrDefault(x => x.Opcode == i);
                writer.Write(opcode is null ? "0x00, " : $"0x{modeMapping[opcode.AddressingMode].Id:X2}, ");
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
                    var mnemonicIndex = mnemonics.First(x => x.Name == opcode.Name).Id;
                    writer.WriteLine($"{mnemonicIndex,-2}, // [0x{i:X2}] {opcode.OpcodeUniqueName} ");
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
                writer.WriteLine($"\"{mnemonic.Name.ToUpperInvariant()}\", // {mnemonic.Id,2} - {mnemonic.Name}");
            }
            writer.CloseBraceBlockStatement();

            writer.WriteLine();
        }

        {
            writer.WriteLine($"private static readonly string[] MapMnemonicToTextLowercase = new string[{mnemonics.Count + 1}]");
            writer.OpenBraceBlock();
            writer.WriteLine("\"???\", // Unknown mnemonic");
            for (var i = 0; i < mnemonics.Count; i++)
            {
                var mnemonic = mnemonics[i];
                writer.WriteLine($"\"{mnemonic.Name.ToLowerInvariant()}\", // {mnemonic.Id,2} - {mnemonic.Name}");
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

        if (className == "Mos6502")
        {
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
        }


        writer.CloseBraceBlock();
    }
    
    private void GenerateMnemonics(List<Mnemonic> mnemonics, string className, string comment)
    {
        var filePath = Path.Combine(GeneratedFolderPath, $"{className}.gen.cs");
        using var writer = CreateCodeWriter(filePath);
        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteSummary(comment);
        writer.WriteLine($"public enum {className} : byte");
        writer.OpenBraceBlock();
        writer.WriteSummary("Undefined mnemonic.");
        writer.WriteLine("Unknown = 0,");
        for (var i = 0; i < mnemonics.Count; i++)
        {
            var mnemonic = mnemonics[i];
            if (mnemonic.Illegal)
            {
                var unstable = mnemonic.Unstable ? " (and unstable)" : string.Empty;
                writer.WriteSummary($"{mnemonic.Name}. This mnemonic is part of the illegal{unstable} instructions.");
            }
            else
            {
                writer.WriteSummary($"{mnemonic.Name}.");
            }
                
            writer.WriteLine($"{mnemonic.Name} = {mnemonic.Id},");
        }
        writer.CloseBraceBlock();
    }

    private const string Mos6502RegisterA = nameof(Mos6502RegisterA);
    private const string Mos6502RegisterX = nameof(Mos6502RegisterX);
    private const string Mos6502RegisterY = nameof(Mos6502RegisterY);


    private record Operand6502(string Name, string Type, string? DefaultValue = null, string? Attributes = null, bool IsDebug = false)
    {
        public string? ArgumentPath { get; set; }

        public bool IsArgumentOnly { get; init; }

        public bool IsParameterOnly { get; init; }

        public string? Comment { get; set; }

        public bool IsOut { get; init; }
        
        public string ParameterDeclaration()
        {
            var builder = new StringBuilder();

            if (IsOut)
            {
                builder.Append("out ");
            }

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
        public List<string> Summary { get; } = new();

        public List<string> GenericParameters { get; } = new();

        public List<string> GenericConstraints { get; } = new();

        public List<string> Remarks { get; } = new();

        public void AddDebugAttributes()
        {
            Arguments.Add(new Operand6502("debugFilePath", "string", "\"\"", "[CallerFilePath]"));
            Arguments.Add(new Operand6502("debugLineNumber", "int", "0", "[CallerLineNumber]"));
        }

        public string Signature
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append(Name);
                if (GenericParameters.Count > 0)
                {
                    builder.Append("<");
                    builder.Append(string.Join(", ", GenericParameters));
                    builder.Append(">");
                }
                builder.Append("(");
                builder.Append(string.Join(", ", Arguments.Where(x => !x.IsArgumentOnly).Select(arg => arg.ParameterDeclaration())));
                builder.Append(")");
                if (GenericConstraints.Count > 0)
                {
                    foreach (var constraint in GenericConstraints)
                    {
                        builder.Append(" where ");
                        builder.Append(constraint);
                    }
                }

                return builder.ToString();
            }
        }
    }
    
    private OpcodeSignature GetOpcodeSignature(JsonAsm6502Opcode opcode)
    {
        var operandKind = OperandValueKind.None;
        List<Operand6502> argumentTypes;
        switch (opcode.AddressingMode)
        {
            case "Implied":
                argumentTypes = new List<Operand6502>();
                operandKind = OperandValueKind.Implied;
                break;
            case "Relative":
                argumentTypes = [new("relativeAddress", "sbyte") { Comment = "Relative Address."}];
                operandKind = OperandValueKind.Relative;
                break;
            case "Accumulator":
                argumentTypes = [new("accumulator", Mos6502RegisterA, "Mos6502RegisterA.A") { Comment = "Accumulator Register."}];
                operandKind = OperandValueKind.Accumulator;
                break;
            case "Immediate":
                argumentTypes = ( [new("immediate", "byte") { Comment = "Immediate value."}]);
                operandKind = OperandValueKind.Immediate;
                break;
            case "ZeroPage":
                argumentTypes = ( [new("zeroPage", "byte") { Comment = "Zero Page address." }]);
                operandKind = OperandValueKind.Zp;
                break;
            case "ZeroPageX":
                argumentTypes = ( [new("zeroPage", "byte") { Comment = "Zero Page address." } , new("x", Mos6502RegisterX) { Comment = "Register X for Zero Page X-Indexed." }]);
                operandKind = OperandValueKind.ZpX;
                break;
            case "ZeroPageY":
                argumentTypes = ( [new("zeroPage", "byte") { Comment = "Zero Page address." }, new("y", Mos6502RegisterY) { Comment = "Register Y for Zero Page Y-Indexed." }]);
                operandKind = OperandValueKind.ZpY;
                break;
            case "Absolute":
                argumentTypes = ( [new("address", "ushort") {Comment = "Absolute address." }]);
                operandKind = OperandValueKind.Address;
                break;
            case "AbsoluteX":
                argumentTypes = ( [new("address", "ushort") { Comment = "Absolute address." }, new("x", Mos6502RegisterX) { Comment = "Register X for Address X-Indexed." }]);
                operandKind = OperandValueKind.AddressX;
                break;
            case "AbsoluteY":
                argumentTypes = ( [new("address", "ushort") { Comment = "Absolute address." }, new("y", Mos6502RegisterY) { Comment = "Register Y for Address Y-Indexed." }]);
                operandKind = OperandValueKind.AddressY;
                break;
            case "Indirect":
                argumentTypes = ( [new("indirect", "Mos6502Indirect") { Comment = "Indirect Absolute address." }]);
                operandKind = OperandValueKind.Indirect;
                break;
            case "IndirectX":
                argumentTypes = ( [new("indirect", "Mos6502IndirectX") { Comment = "Indirect Zero Page address." }]);
                operandKind = OperandValueKind.IndirectX;
                break;
            case "IndirectY":
                argumentTypes = ( [new("indirect", "Mos6502IndirectY") { Comment = "Indirect Zero Page address." }, new("y", Mos6502RegisterY) { Comment = "Register Y for Indirect Zero-Page Y-Indexed." }]);
                operandKind = OperandValueKind.IndirectY;
                break;
            default:
                throw new NotSupportedException($"Addressing mode '{opcode.AddressingMode}' is not supported for opcode '{opcode.Name}'");
        }

        var operandCount = argumentTypes.Count;
        return new(opcode, opcode.UniqueName, operandCount, argumentTypes, operandKind);
    }

    
    private void GenerateInstructionFactory(string className, List<JsonAsm6502Opcode> opcodes)
    {

        var filePath = Path.Combine(GeneratedFolderPath, $"{className}InstructionFactory.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteSummary($"Factory for all {className} instructions.");
        writer.WriteLine($"public static partial class {className}InstructionFactory");
        writer.OpenBraceBlock();

        foreach (var opcode in opcodes)
        {
            var opcodeSignature = GetOpcodeSignature(opcode);
            var mode = _mapAddressingMode[opcode.AddressingMode];
            writer.WriteSummary($"Creates the {opcode.Name} instruction ({opcode.OpcodeHex}) instruction with addressing mode {opcode.AddressingMode}.");
            string special = string.Empty;
            if (opcode.Illegal)
            {
                special = opcode.Unstable ? " This is an illegal and unstable instruction." : " This is an illegal instruction.";
            }
            writer.WriteDoc([$"<remarks>{opcode.NameLong}. Cycles: {opcode.Cycles}, Size: {BytesText(mode.SizeBytes)}.{special}</remarks>"]);
            writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            if (opcode.Unstable)
            {
                writer.WriteLine("[Obsolete(\"This instruction is unstable and may not behave as expected.\", false)]");
            }
            writer.Write($"public static {className}Instruction {opcodeSignature.Signature} => new ({className}OpCode.{opcode.OpcodeUniqueName}");

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

    private void GenerateAssemblerFactory(string className, List<JsonAsm6502Opcode> opcodes)
    {
        // Generate all assembler instruction
        GenerateAssemblerFactoryGeneric(className, $"{className}Assembler",
            opcodes,
            (
            opcodeSignature => true,
            opcodeSignature =>
            {
                opcodeSignature.AddDebugAttributes();
            }
            )
        );
    }

    //private void GenerateAssemblerFactoryWithLabel(List<JsonAsm6502Opcode> opcodes)
    //{
    //    GenerateAssemblerFactoryGeneric("Mos6502Assembler_WithLabels",
    //        opcodes,
    //        opcodeSignature => opcodeSignature.OperandKind == OperandValueKind.Address ||
    //                           opcodeSignature.OperandKind == OperandValueKind.AddressX ||
    //                           opcodeSignature.OperandKind == OperandValueKind.AddressY ||
    //                           opcodeSignature.OperandKind == OperandValueKind.Relative ||
    //                           opcodeSignature.OperandKind == OperandValueKind.Indirect ||
    //                           opcodeSignature.OperandKind == OperandValueKind.Zp ||
    //                           opcodeSignature.OperandKind == OperandValueKind.ZpX ||
    //                           opcodeSignature.OperandKind == OperandValueKind.ZpY,
    //        opcodeSignature =>
    //        {
    //            var originalOperand = opcodeSignature.Arguments[0];
    //            bool isZp = opcodeSignature.OperandKind == OperandValueKind.Zp || opcodeSignature.OperandKind == OperandValueKind.ZpX || opcodeSignature.OperandKind == OperandValueKind.ZpY;
    //            var operand0 = new Operand6502("address", opcodeSignature.OperandKind == OperandValueKind.Indirect ? opcodeSignature.Arguments[0].Type.Replace("Mos6502Indirect", "Mos6502IndirectLabel") : isZp ? "Mos6502LabelZp" : "Mos6502Label");
    //            operand0.ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"new {originalOperand.Type}({operand0.Name}.ZpLabel.Address)" : $"({originalOperand.Type}){operand0.Name}.Address";

    //            opcodeSignature.Arguments[0] = operand0;

    //            opcodeSignature.Arguments.Add(new Operand6502(operand0.Name, operand0.Type)
    //            {
    //                ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"{operand0.Name}.ZpLabel" : null,
    //                IsArgumentOnly = true
    //            } ); // Pass the label as an additional argument to the assembler AddInstruction
    //            opcodeSignature.AddDebugAttributes();
    //        });
    //}


    private void GenerateAssemblerFactoryWithEnums(string className, List<JsonAsm6502Opcode> opcodes)
    {
        GenerateAssemblerFactoryGeneric(className, $"{className}Assembler_WithEnums",
            opcodes,
            (
                opcodeSignature =>
                    opcodeSignature.OperandKind == OperandValueKind.Immediate,
                opcodeSignature =>
                {
                    opcodeSignature.GenericParameters.Add("TEnum");
                    opcodeSignature.GenericConstraints.Add("TEnum : struct, Enum");
                    
                    // public Mos6510Assembler ORA_Imm<TEnum>(TEnum value) where TEnum : struct, Enum => asm.ORA_Imm(Unsafe.As<TEnum, byte>(ref value));
                    var type = "TEnum";
                    var operand0 = new Operand6502(opcodeSignature.Arguments[0].Name, type);
                    opcodeSignature.Arguments[0] = operand0;
                    operand0.ArgumentPath = $"Unsafe.As<TEnum, byte>(ref {opcodeSignature.Arguments[0].Name})";
                    opcodeSignature.AddDebugAttributes();
                }
            )
        );
    }


    private void GenerateAssemblerFactoryWithExpressions(string className, List<JsonAsm6502Opcode> opcodes)
    {
        GenerateAssemblerFactoryGeneric(className,$"{className}Assembler_WithExpressions",
            opcodes,
            (
            opcodeSignature =>
                                opcodeSignature.OperandKind == OperandValueKind.Relative ||
                                opcodeSignature.OperandKind == OperandValueKind.Immediate ||
                                opcodeSignature.OperandKind == OperandValueKind.Address ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressX ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressY ||
                               opcodeSignature.OperandKind == OperandValueKind.Indirect ||
                               opcodeSignature.OperandKind == OperandValueKind.IndirectX ||
                               opcodeSignature.OperandKind == OperandValueKind.IndirectY ||
                               opcodeSignature.OperandKind == OperandValueKind.Zp ||
                               opcodeSignature.OperandKind == OperandValueKind.ZpX ||
                               opcodeSignature.OperandKind == OperandValueKind.ZpY,
            opcodeSignature =>
            {
                var originalOperand = opcodeSignature.Arguments[0];
                bool isZp = opcodeSignature.OperandKind == OperandValueKind.Zp || opcodeSignature.OperandKind == OperandValueKind.ZpX || opcodeSignature.OperandKind == OperandValueKind.ZpY;
                string type;
                switch (opcodeSignature.OperandKind)
                {
                    case OperandValueKind.Immediate:
                    case OperandValueKind.Zp:
                    case OperandValueKind.ZpX:
                    case OperandValueKind.ZpY:
                        type = "Expressions.Mos6502ExpressionU8";
                        break;
                    case OperandValueKind.Relative:
                    case OperandValueKind.Address:
                    case OperandValueKind.AddressX:
                    case OperandValueKind.AddressY:
                        type = "Expressions.Mos6502ExpressionU16";
                        break;
                    case OperandValueKind.Indirect:
                        type = "Expressions.Mos6502ExpressionIndirectU16";
                        break;
                    case OperandValueKind.IndirectX:
                        type = "Expressions.Mos6502ExpressionIndirectX";
                        break;
                    case OperandValueKind.IndirectY:
                        type = "Expressions.Mos6502ExpressionIndirectY";
                        break;
                    default:
                        throw new NotSupportedException($"Operand kind '{opcodeSignature.OperandKind}' is not supported for opcode '{opcodeSignature.Opcode.Name}'");
                }
                
                var operand0 = new Operand6502(opcodeSignature.Arguments[0].Name, type);
                opcodeSignature.Arguments[0] = operand0;
                operand0.ArgumentPath = opcodeSignature.OperandKind == OperandValueKind.Indirect ? $"new {originalOperand.Type}(0)" : $"({originalOperand.Type})0";

                opcodeSignature.Arguments.Add(new Operand6502(operand0.Name, operand0.Type) { IsArgumentOnly = true }); // Pass the label as an additional argument to the assembler AddInstruction
                opcodeSignature.AddDebugAttributes();
            }),
            (
            opcodeSignature =>
                                opcodeSignature.OperandKind == OperandValueKind.Relative ||
                                opcodeSignature.OperandKind == OperandValueKind.Address ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressX ||
                               opcodeSignature.OperandKind == OperandValueKind.AddressY
            ,
            opcodeSignature =>
            {
                var originalOperand = opcodeSignature.Arguments[0];
                var operand0 = new Operand6502(opcodeSignature.Arguments[0].Name, "Mos6502Label")
                {
                    IsOut = true
                };
                opcodeSignature.Arguments[0] = operand0;
                operand0.ArgumentPath = $"({originalOperand.Type})0";

                opcodeSignature.Summary.Add($"The output <paramref name=\"{operand0.Name}\"/> is declaring a forward label at the same time.");

                opcodeSignature.Arguments.Add(new Operand6502(operand0.Name, operand0.Type) { IsArgumentOnly = true, IsOut = true }); // Pass the label as an additional argument to the assembler AddInstruction
                opcodeSignature.AddDebugAttributes();
                opcodeSignature.Arguments.Add(new Operand6502("addressExpression", "string?", "null", $"[CallerArgumentExpression(nameof({operand0.Name}))]") { IsParameterOnly  = true });
            }
        )
            );
    }

    private void GenerateAssemblerFactoryGeneric(string className, string fileName, List<JsonAsm6502Opcode> opcodes, params (Func<OpcodeSignature, bool> filter, Action<OpcodeSignature> modify)[] filterAndModifiers)
    {

        var filePath = Path.Combine(GeneratedFolderPath, $"{fileName}.gen.cs");
        using var writer = CreateCodeWriter(filePath);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine();

        writer.WriteLine("namespace Asm6502;");
        writer.WriteLine();
        writer.WriteLine($"partial class {className}Assembler");
        writer.OpenBraceBlock();

        var mnemonicToSignatureToOpcodes = new Dictionary<string, Dictionary<string, OpcodeSignature>>();

        foreach (var opcode in opcodes)
        {
            var opcodeSignature = GetOpcodeSignature(opcode);

            foreach (var filterAndModifier in filterAndModifiers)
            {
                var filter = filterAndModifier.filter;
                var modify = filterAndModifier.modify;

                if (filter(opcodeSignature))
                {
                    modify(opcodeSignature);

                    if (!mnemonicToSignatureToOpcodes.TryGetValue(opcode.UniqueName, out var opcodeWithAddress))
                    {
                        opcodeWithAddress = new Dictionary<string, OpcodeSignature>();
                        mnemonicToSignatureToOpcodes[opcode.UniqueName] = opcodeWithAddress;
                    }

                    opcodeWithAddress[opcodeSignature.Signature] = opcodeSignature;

                    // Create new copy for new modification
                    opcodeSignature = GetOpcodeSignature(opcode);
                }
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
                var opcode = opcodeSignature.Opcode;
                var mode = _mapAddressingMode[opcodeSignature.Opcode.AddressingMode];

                string special = string.Empty;
                if (opcode.Illegal)
                {
                    special = opcode.Unstable ? " This is an illegal and unstable instruction." : " This is an illegal instruction.";
                }

                List<string> summaryList =
                [
                    $"{opcodeSignature.Opcode.Summary}. <see href=\"{opcode.OnlineDocumentation.FirstOrDefault(x => x.Contains("masswerk", StringComparison.Ordinal))}\">{mnemonic}</see> instruction ({opcodeSignature.Opcode.OpcodeHex}) with addressing mode {opcodeSignature.Opcode.AddressingMode}."
                    , .. opcodeSignature.Summary
                ];

                writer.WriteSummary(summaryList);

                // Write remarks
                var lines = new List<string>();

                // Add arguments
                for (int i = 0; i < opcodeSignature.OperandCount; i++)
                {
                    var arg = opcodeSignature.Arguments[i];
                    lines.Add($"<param name=\"{arg.Name}\">{arg.Comment}</param>");
                }

                StringBuilder syntax = new StringBuilder();
                syntax.Append(mnemonic);
                switch (opcode.AddressingMode)
                {
                    case "Implied":
                        break;
                    case "Relative":
                        syntax.Append(" $BB");
                        break;
                    case "Accumulator":
                        syntax.Append(" A");
                        break;
                    case "Immediate":
                        syntax.Append(" #$BB");
                        break;
                    case "ZeroPage":
                        syntax.Append(" $LL");
                        break;
                    case "ZeroPageX":
                        syntax.Append(" $LL,X");
                        break;
                    case "ZeroPageY":
                        syntax.Append(" $LL,X");
                        break;
                    case "Absolute":
                        syntax.Append(" $LLHH");
                        break;
                    case "AbsoluteX":
                        syntax.Append(" $LLHH,X");
                        break;
                    case "AbsoluteY":
                        syntax.Append(" $LLHH,Y");
                        break;
                    case "Indirect":
                        syntax.Append(" ($LLHH)");
                        break;
                    case "IndirectX":
                        syntax.Append(" ($LL,X)");
                        break;
                    case "IndirectY":
                        syntax.Append(" ($LL),Y");
                        break;
                    default:
                        throw new NotSupportedException($"Addressing mode '{opcode.AddressingMode}' is not supported for opcode '{opcode.Name}'");
                }

                lines.AddRange([
                
                    "<remarks>",
                    $"{opcode.Synopsis.Replace("&", "&amp;").Replace("<", "&lt;")}",
                    "<code>",
                    $"Syntax: {syntax}",
                    $"OpCode: {opcode.OpcodeHex}",
                    $"Cycles: {opcodeSignature.Opcode.Cycles}",
                    $"  Size: {mode.SizeBytes}"
                ]);

                {
                    lines.Add($" Flags: N V - B D I Z C");
                    string flagLine = "        ";
                    bool isFirst = true;
                    foreach (var flag in "NV-BDIZC")
                    {
                        if (!isFirst) { flagLine += " "; }
                        isFirst = false;
                        var found = opcode.StatusFlags.FirstOrDefault(x => x.StartsWith(flag));
                        if (found is not null)
                        {
                            if (found.Length > 1)
                            {
                                found = found.Substring(2);
                            }
                            else
                            {
                                found = "+";
                            }

                            flagLine += found;
                            if (found.Length > 1)
                            {
                                isFirst = true; // Remove space after multi-char flags
                            }
                        }
                        else
                        {
                            flagLine += "-";
                        }
                    }
                    lines.Add(flagLine);
                }
                lines.Add("</code>");
                if (!string.IsNullOrEmpty(special))
                {
                    lines.Add(special);
                }
                lines.Add("</remarks>");
                writer.WriteDoc(lines.ToArray());
                if (opcode.Unstable)
                {
                    writer.WriteLine("[Obsolete(\"This instruction is unstable and may not behave as expected.\", false)]");
                }
                writer.WriteLine($"public {className}Assembler {signature}");
                writer.Indent();

                writer.Write($"=> AddInstruction({className}InstructionFactory.{opcodeSignature.Name}(");

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
                    if (arg.IsParameterOnly)
                    {
                        continue;
                    }
                    writer.Write($", {arg.ArgumentPath ?? arg.Name}");

                    if (arg.IsOut)
                    {
                        writer.Write(" = new Mos6502Label(Mos6502Label.ParseCSharpExpression(addressExpression))");
                    }
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
    
    private void GenerateAssemblyTests(string className, List<JsonAsm6502Opcode> opcodes)
    {
        using var writer = CreateCodeWriter($"{className}AssemblerTests.gen.cs", true);

        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine("using static Asm6502.Mos6502Factory;");
        writer.WriteLine("#pragma warning disable CS0618 // We are still validating Unstable API in our tests even if it's marked as obsolete.");

        writer.WriteLine("namespace Asm6502.Tests;");
        writer.WriteLine();
        writer.WriteLine("[TestClass]");
        writer.WriteLine($"public partial class {className}AssemblerTests");
        writer.OpenBraceBlock();

        foreach (var opcode in opcodes)
        {
            writer.WriteLine("[TestMethod]");
            writer.WriteLine($"public async Task {opcode.OpcodeUniqueName}()");
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

            writer.WriteLine($"await VerifyAsm(asm);");

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
        writer.WriteLine("// This code was generated by Asm6502.CodeGen.");
        writer.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        writer.WriteLine("//     the code is regenerated.");
        writer.WriteLine("// ------------------------------------------------------------------------------");
        writer.WriteLine("// ReSharper disable All");
        writer.WriteLine("// ------------------------------------------------------------------------------");
        return writer;
    }

    private static string BytesText(int value) => value > 1 ? $"{value} bytes" : $"{value} byte";

    private record Mnemonic(int Id, string Name, bool Illegal, bool Unstable);
}
