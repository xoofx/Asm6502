// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;

namespace Asm6502;

/// <summary>
/// Default implementation of <see cref="IMos6502AssemblerDebugMap"/> that stores debug line info into a list.
/// </summary>
[DebuggerDisplay("Count={Items.Count}")]
public class Mos6502AssemblerDebugMap : IMos6502AssemblerDebugMap
{
    /// <summary>
    /// Gets the list of debug line information collected during assembly.
    /// </summary>
    public List<Mos6502AssemblerDebugInfo> Items { get; } = new();
    
    /// <inheritdoc />
    public void LogDebugInfo(Mos6502AssemblerDebugInfo debugInfo) => Items.Add(debugInfo);

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
        foreach (var info in Items)
        {
            writer.WriteLine(info.ToString());
        }
    }
}