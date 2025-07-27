// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;

namespace AsmMos6502;

/// <summary>
/// Default implementation of <see cref="IMos6502AssemblerDebugMap"/> that stores debug line info into a list.
/// </summary>
[DebuggerDisplay("BeginAddress={BeginAddress}, EndAddress={EndAddress}, DebugLinesCount={DebugLines.Count}")]
public class Mos6502AssemblerDebugMap : IMos6502AssemblerDebugMap
{
    /// <summary>
    /// Gets or sets the name of the program (to be used in debug output via <see cref="DumpTo"/> ).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the starting address of the program.
    /// </summary>
    public ushort BeginAddress { get; private set; }

    /// <summary>
    /// Gets the ending address of the program.
    /// </summary>
    public ushort EndAddress { get; private set; }

    /// <summary>
    /// Gets the list of debug line information collected during assembly.
    /// </summary>
    public List<Mos6502AssemblerDebugLineInfo> DebugLines { get; } = new ();
    
    /// <inheritdoc />
    public void BeginProgram(ushort address) => BeginAddress = address;

    /// <inheritdoc />
    public void LogDebugLineInfo(Mos6502AssemblerDebugLineInfo debugLineInfo) => DebugLines.Add(debugLineInfo);

    /// <inheritdoc />
    public void EndProgram(ushort address) => EndAddress = address;

    /// <inheritdoc />
    public override string ToString()
    {
        var stringWriter = new StringWriter();
        DumpTo(stringWriter);
        return stringWriter.ToString();
    }
    
    /// <summary>
    /// Writes a detailed representation of the program's debug information to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <remarks>The output includes the program's start and end addresses, the total number of debug lines, 
    /// and a line-by-line representation of the debug information.</remarks>
    /// <param name="writer">The <see cref="TextWriter"/> to which the debug information will be written.</param>
    public void DumpTo(TextWriter writer)
    {
        writer.WriteLine($"Debug Info (Program: {Name ?? "???"})");
        writer.WriteLine($"- Program Start Address: {BeginAddress:X4}");
        writer.WriteLine($"- Program End Address: {EndAddress:X4}");
        writer.WriteLine($"- Debug Line Count: {DebugLines.Count}");
        writer.WriteLine();
        foreach (var line in DebugLines)
        {
            writer.WriteLine(line.ToString());
        }
    }
}