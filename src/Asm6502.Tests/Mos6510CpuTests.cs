// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Asm6502.Tests;

/// <summary>
/// Contains unit tests for the <see cref="Mos6510Cpu"/> implementation, verifying instruction behavior and memory interactions
/// using test cases from the 65x02 test suite.
/// </summary>
/// <remarks>These tests utilize JSON-based test vectors from the https://github.com/SingleStepTests/65x02
/// repository to validate CPU correctness at the instruction and cycle level. To run the tests, ensure the 65x02
/// repository is cloned at the appropriate directory level as described in the test class. The tests check CPU state,
/// memory actions, and cycle counts for each instruction, providing comprehensive coverage of the MOS 6510 CPU's
/// expected behavior.
///
/// Checkout at the same level https://github.com/SingleStepTests/65x02 of this repository, for example:
/// <code>
/// C:\code\65x02
/// C:\code\Asm6502
/// </code>
///
/// This class is also extracting a representative subset of tests (<see cref="TestSubsetCountPerOpCode"/> per opcode) to a separate JSON file cpu_6502_tests_subset.json
/// to allow testing during CI and in case the full Thomas Harte tests haven't been cloned.
/// </remarks>
[TestClass]
public class Mos6510CpuTests
{
    private const string TestSubsetName = "cpu_6502_tests_subset";
    private const int TestSubsetCountPerOpCode = 40; // The number of tests to select per opcode for the subset from Thomas Harte tests.
    private static List<TestCase6502> SelectedSubsetTestCases = new();
    private static bool HasErrors = false;

    /// <summary>
    /// Tests https://github.com/SingleStepTests/65x02
    /// Checkout at the same level of this repository, for example:
    /// <code>
    /// C:\code\65x02
    /// C:\code\Asm6502
    /// </code>
    /// </summary>
    private static readonly string Tests6502Folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "65x02", "6502", "v1"));

    public Mos6510CpuTests()
    {
    }
    

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TestSimple()
    {
        // Create memory and a tiny program at $C000: LDA #$01; ADC #$01;
        var mem = new byte[65536];
        mem[0xC000] = 0xA9;
        mem[0xC001] = 0x01; // LDA #$01
        mem[0xC002] = 0x69;
        mem[0xC003] = 0x01; // ADC #$01

        // Set Reset vector to $C000
        mem[0xFFFC] = 0x00;
        mem[0xFFFD] = 0xC0;

        var cpu = new Mos6510Cpu(new RamBus(mem));
        cpu.Reset(); // fetch reset vector and begin executing
        cpu.Steps(2); // Run three instructions
        Assert.AreEqual(2, cpu.A); // 2
        Assert.AreEqual(0xC004, cpu.PC);
    }

    // Minimal 64 KiB RAM bus
    public sealed class RamBus : IMos6502CpuMemoryBus
    {
        private readonly byte[] _ram;
        public RamBus(byte[] ram) => _ram = ram;
        public byte Read(ushort address) => _ram[address];
        public void Write(ushort address, byte value) => _ram[address] = value;
    }


    [TestMethod]
    [CustomDataSource]
    public void TestJson(string file)
    {
        if (!File.Exists(file))
        {
            Assert.Inconclusive($"Test file `{file}` does not exist. Please clone https://github.com/SingleStepTests/65x02");
            return;
        }
        
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        options.Converters.Add(new JsonMemoryActionConverter());

        var testCases = JsonSerializer.Deserialize<List<TestCase6502>>(File.ReadAllText(file), options)!;
        
        var bus = new MemoryBusTracker();
        var cpu = new Mos6510Cpu(bus);
        cpu.Steps(8);
        bus.Reset();

        Dictionary<TestCaseClusterKey, List<TestCase6502>> clusteredTests = new();
        
        int failed = 0;
        foreach (var test in testCases)
        {
            if (cpu.IsJammed)
            {
                cpu.Reset();
            }
            
            var exception = TestSingle(file, test, cpu, bus, clusteredTests);
            if (exception != null)
            {
                Console.WriteLine($"Test `{test.Name}` in file `{file}` failed: {exception}");
                failed++;
            }
        }
        if (failed > 0)
        {
            HasErrors = true;
            Assert.Fail($"{failed}/{testCases.Count} tests failed in file `{file}`");
        }

        var fileName = Path.GetFileNameWithoutExtension(file);
        if (fileName == TestSubsetName) return; // Do not cluster the selected subset file

        UpdateSelectedTestsFromSampling(clusteredTests);
    }

    private AssertFailedException? TestSingle(string file, TestCase6502 testCase, Mos6502Cpu cpu, MemoryBusTracker bus, Dictionary<TestCaseClusterKey, List<TestCase6502>> clusteredTests)
    {
        try
        {
            var initial = testCase.Initial!;
            cpu.PC = initial.PC;
            cpu.S = initial.S;
            cpu.A = initial.A;
            cpu.X = initial.X;
            cpu.Y = initial.Y;
            cpu.SR = (Mos6502CpuFlags)initial.P;

            bus.Reset();

            foreach (var ramBlock in initial.Ram)
            {
                var address = ramBlock[0];
                var value = (byte)ramBlock[1];
                bus.Ram[address] = value;
            }

            var collectedCycles = new List<List<MemoryAction>>();


            // Collect all cycles and associated memory actions for one CPU step/instruction
            
            cpu.Step(() =>
            {
                var collectedActions = new List<MemoryAction>();
                collectedActions.AddRange(bus.Actions);
                bus.Actions.Clear();
                collectedCycles.Add(collectedActions);
                return collectedCycles.Count <= 10;
            });

            var sb = new StringBuilder();
            for (var i = 0; i < collectedCycles.Count; i++)
            {
                var cycle = collectedCycles[i];
                foreach (var action in cycle)
                {
                    sb.AppendLine($"[{i}] => {action}");
                }
            }
            var generatedActionsAsText = sb.ToString();
            sb.Clear();
            for (var i = 0; i < testCase.Cycles.Count; i++)
            {
                var action = testCase.Cycles[i];
                sb.AppendLine($"[{i}] => {action}");
            }
            var expectedActionsAsText = sb.ToString();

            var actions = $"\n--- Expected Actions ---\n{expectedActionsAsText}\n--- Generated Actions ---\n{generatedActionsAsText}\n------------------------\n";
            
            var minCycles = Math.Min(collectedCycles.Count, testCase.Cycles.Count);

            for (var i = 0; i < minCycles; i++)
            {
                var expectedMemoryAction = testCase.Cycles[i];
                var actualMemoryActions = collectedCycles[i];

                Assert.AreEqual(1, actualMemoryActions.Count, $"Expecting a single memory operation per cycle at cycle #{i} for test `{testCase.Name}`. {actions}");

                var action = actualMemoryActions[^1];
                Assert.AreEqual(action, expectedMemoryAction, $"Memory operation not matching at cycle #{i} for test `{testCase.Name}`. {actions}");
            }

            if (collectedCycles.Count != testCase.Cycles.Count)
            {
                Assert.Fail($"Number of cycles not matching. Expected {testCase.Cycles.Count} but got {collectedCycles.Count} for test `{testCase.Name}`. {actions}");
            }
            
            var final = testCase.Final!;
            Assert.AreEqual(final.PC, cpu.PC, $"Final PC not matching for test `{testCase.Name}`");
            Assert.AreEqual(final.S, cpu.S, $"Final S not matching for test `{testCase.Name}`");
            Assert.AreEqual(final.A, cpu.A, $"Final A not matching for test `{testCase.Name}`. Initial SR: {(Mos6502CpuFlags)initial.P}");
            Assert.AreEqual(final.X, cpu.X, $"Final X not matching for test `{testCase.Name}`. Initial SR: {(Mos6502CpuFlags)initial.P}");
            Assert.AreEqual(final.Y, cpu.Y, $"Final Y not matching for test `{testCase.Name}`. Initial SR: {(Mos6502CpuFlags)initial.P}");
            Assert.AreEqual((Mos6502CpuFlags)final.P, cpu.SR, $"Final SR not matching for test `{testCase.Name}`");

            foreach (var ramBlock in final.Ram)
            {
                var address = ramBlock[0];
                var expectedValue = (byte)ramBlock[1];
                var actualValue = bus.Ram[address];
                Assert.AreEqual(expectedValue, actualValue, $"Final RAM value not matching at address {address:X4} for test `{testCase.Name}`");
            }

            // We verify that the number of cycles emitted is matching the CPU.InstructionCycles
            Assert.AreEqual((uint)testCase.Cycles.Count, cpu.InstructionCycles, $"Number of cycles not matching CPU.InstructionCycles for test `{testCase.Name}`");

            // Extract cluster key from tests
            // The way it is working is that we group tests with the:
            // - same number of cycles / read/write sequence
            // - If the instruction is BCD and if the decimal mode is on
            // - If there is a carry in
            // - If there is a carry out
            // - If there is an overflow out
            //
            // Then later, we will randomly select TestSubsetCountPerOpCode from each cluster
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Length == 2) // opcode
            {
                HashCode hash = new();
                hash.Add(testCase.Cycles.Count);
                foreach (var memoryAction in testCase.Cycles)
                {
                    hash.Add(memoryAction.Kind);
                }
                var cycleHash = hash.ToHashCode();

                var opcode = (Mos6510OpCode)byte.Parse(fileName, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var mnemonic = opcode.ToMnemonic();
                bool isBCDInstructionWithBCDModeOn = false;
                var initialSR = (Mos6502CpuFlags)initial.P;
                switch (mnemonic)
                {
                    case Mos6510Mnemonic.ADC:
                    case Mos6510Mnemonic.SBC:
                    case Mos6510Mnemonic.ARR:
                        isBCDInstructionWithBCDModeOn = ((initialSR & Mos6502CpuFlags.D) != 0);
                        break;
                }
                var carryIn = (initialSR & Mos6502CpuFlags.C) != 0;
                var overflowOut = (cpu.SR & Mos6502CpuFlags.V) != 0;
                var carryOut = (cpu.SR & Mos6502CpuFlags.C) != 0;
                var key = new TestCaseClusterKey(cycleHash, isBCDInstructionWithBCDModeOn, carryIn, overflowOut, carryOut);

                if (!clusteredTests.TryGetValue(key, out var tests))
                {
                    tests = new();
                    clusteredTests.Add(key, tests);
                }
                tests.Add(testCase);
            }
        }
        catch (AssertFailedException ex)
        {
            return ex;
        }

        return null;
    }
    

    [ClassCleanup()]
    public static void SerializeBackExtractedTestsSubset()
    {
        if (SelectedSubsetTestCases.Count == 0 || HasErrors) return;
        var outputFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        if (string.IsNullOrEmpty(outputFolder)) throw new InvalidOperationException($"test output folder cannot be null");

        // Keep it stable/sorted
        var selectedSubsetTestCases = SelectedSubsetTestCases.OrderBy(x => x.Name).ToList();

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonMemoryActionConverter());
        var outputFile = Path.Combine(outputFolder, $"{TestSubsetName}.json");

        // Serialize each line as indented but on each line
        {
            using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
            writer.WriteLine("[");
            for (var i = 0; i < selectedSubsetTestCases.Count; i++)
            {
                var testCase = selectedSubsetTestCases[i];
                var line = JsonSerializer.Serialize(testCase, options).Trim();
                writer.Write(line);
                if (i < selectedSubsetTestCases.Count - 1)
                {
                    writer.WriteLine(",");
                }
                else
                {
                    writer.WriteLine();
                }
            }

            writer.WriteLine("]");
        }
        Console.WriteLine($"Wrote {selectedSubsetTestCases.Count} selected tests to {outputFile}");
    }



    private void UpdateSelectedTestsFromSampling(Dictionary<TestCaseClusterKey, List<TestCase6502>> clusteredTests)
    {
        var checkCount = clusteredTests.Values.Sum(x => x.Count);
        if (checkCount < TestSubsetCountPerOpCode) Assert.Inconclusive($"Invalid number of clustered tests {checkCount} < {TestSubsetCountPerOpCode}");

        var rnd = new Random(1234);
        var selectedTests = new List<TestCase6502>();
        while (selectedTests.Count < TestSubsetCountPerOpCode)
        {
            foreach (var kv in clusteredTests)
            {
                var tests = kv.Value;
                var selectedTest = tests[rnd.Next(tests.Count)];
                if (!selectedTests.Contains(selectedTest))
                {
                    selectedTests.Add(selectedTest);
                }
                if (selectedTests.Count >= TestSubsetCountPerOpCode) break;
            }
        }

        SelectedSubsetTestCases.AddRange(selectedTests);
    }
    
    private record struct TestCaseClusterKey(int CycleHash, bool IsBCDInstructionWithBCDModeOn, bool CarryIn, bool OverflowOut, bool CarryOut);
    

    public class MemoryBusTracker : IMos6502CpuMemoryBus
    {
        public byte[] Ram { get; } = new byte[65536];
        
        public List<MemoryAction> Actions { get; } = new List<MemoryAction>();

        public void Reset()
        {
            Ram.AsSpan().Clear();
            Actions.Clear();
        }

        public byte Read(ushort address)
        {
            var value = Ram[address];
            Actions.Add(new MemoryAction(MemoryActionKind.Read, address, value));
            return value;
        }

        public void Write(ushort address, byte value)
        {
            Ram[address] = value;
            Actions.Add(new MemoryAction(MemoryActionKind.Write, address, value));
        }
    }

    internal class JsonMemoryActionConverter : JsonConverter<MemoryAction>
    {
        public override MemoryAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // [address(ushort), value(byte), "read"|"write"]
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected start of array");
            reader.Read();
            var address = (ushort)reader.GetInt32();
            reader.Read();
            var value = (byte)reader.GetInt32();
            reader.Read();
            var op = reader.GetString();
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Expected end of array");
            return new MemoryAction(op == "read" ? MemoryActionKind.Read : MemoryActionKind.Write, address, value);
        }

        public override void Write(Utf8JsonWriter writer, MemoryAction value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.Address);
            writer.WriteNumberValue(value.Value);
            writer.WriteStringValue(value.Kind == MemoryActionKind.Read ? "read" : "write");
            writer.WriteEndArray();
        }
    }

    public class CustomDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            // Always add first the selected subset
            yield return [Path.Combine(AppContext.BaseDirectory, $"{TestSubsetName}.json")];

            for (int i = 0; i < 256; i++)
            {
                var file = Path.Combine(Tests6502Folder, $"{i:X2}.json");
                yield return [file];
            }
        }

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        {
            if (data != null)
            {
                var file = (string)data[0]!;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (fileNameWithoutExt == TestSubsetName)
                {
                    return $"Primary_{fileNameWithoutExt}";
                }
                else
                {
                    var b = byte.Parse(fileNameWithoutExt, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var opcode = (Mos6510OpCode)b;
                    return $"Test_{b:X2}_{opcode}";
                }
            }

            return null;
        }
    }

    public class TestCase6502
    {
        public string Name { get; set; } = string.Empty;

        public CpuAndRamState? Initial { get; set; }

        public CpuAndRamState? Final { get; set; }

        public List<MemoryAction> Cycles { get; set; } = new();
    }

    public readonly record struct MemoryAction(MemoryActionKind Kind, ushort Address, byte Value)
    {
        public override string ToString()
        {
            var dir = Kind == MemoryActionKind.Read ? "-->" : "<--";

            return $"Addr: {Address,5} (0x{Address:x4}) {dir} {Value,3} (0x{Value:x2}), Kind: {Kind.ToString().ToLowerInvariant(),5}";
        }
    }

    public enum MemoryActionKind
    {
        Read,
        Write
    }

    public class CpuAndRamState
    {
        public ushort PC { get; set; }

        public byte S { get; set; }

        public byte A { get; set; }

        public byte X { get; set; }

        public byte Y { get; set; }

        public byte P { get; set; }

        public List<List<ushort>> Ram { get; set; } = new List<List<ushort>>();
    }
}
