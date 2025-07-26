// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsmMos6502.CodeGen;


public class JsonAsm6502Instructions
{
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<JsonAsm6502AddressingMode> Modes { get; } = new();

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<JsonAsm6502Opcode> Opcodes { get; } = new();


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // To allow <, >...etc in strings
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        IncludeFields = true,
        WriteIndented = true,
    };

    public static JsonAsm6502Instructions ReadJson(string filename)
    {
        using var stream = File.OpenRead(filename);
        return JsonSerializer.Deserialize<JsonAsm6502Instructions>(stream, JsonOptions)!;
    }
}

public class JsonAsm6502AddressingMode
{
    public string Kind { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    public int Cycles { get; set; }

    public string DummyReads { get; set; } = string.Empty;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> OnlineDocumentation { get; set; } = new();
}



public class JsonAsm6502Opcode
{
    public string Name { get; set; } = string.Empty;
    public string NameLong { get; set; } = string.Empty;
    public int Opcode { get; set; }
    public string OpcodeHex { get; set; } = string.Empty;
    public string AddressingMode { get; set; } = string.Empty;
    public int Cycles { get; set; }
    public bool DummyReads { get; set; }
    public bool DummyWrites { get; set; }
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> AllNames { get; } = new();
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> StatusFlags { get; } = new();
    public bool Illegal { get; set; }
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> OnlineDocumentation { get; } = new();
}