// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace Asm6502.Relocator;

/// <summary>
/// Provides configuration settings for code relocation analysis, including program location, memory boundaries,
/// and zero-page relocation parameters used by <see cref="CodeRelocator"/>.
/// </summary>
public class CodeRelocationConfig
{
    /// <summary>
    /// Gets or sets the memory address where the program is loaded.
    /// </summary>
    public required ushort ProgramAddress { get; set; }

    /// <summary>
    /// Gets or sets the byte array containing the program machine code to be analyzed and relocated.
    /// </summary>
    public required byte[] ProgramBytes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether zero-page relocation is enabled.
    /// Default is true.
    /// </summary>
    public bool ZpRelocate { get; set; } = true;

    /// <summary>
    /// Gets or sets the starting address for relocation analysis.
    /// If null, defaults to <see cref="ProgramAddress"/>.
    /// </summary>
    public ushort? RelocationAnalysisStart { get; set; }

    /// <summary>
    /// Gets or sets the ending address for relocation analysis.
    /// If null, defaults to <see cref="ProgramAddress"/> + <see cref="ProgramBytes"/> length - 1.
    /// </summary>
    public ushort? RelocationAnalysisEnd { get; set; }

    /// <summary>
    /// Returns a string representation of the configuration settings.
    /// </summary>
    /// <returns>A formatted string containing all configuration values.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ProgramAddress: ${ProgramAddress:x4}");
        sb.AppendLine($"ProgramBytes: {ProgramBytes.Length} bytes");
        sb.AppendLine($"ZpRelocate: {ZpRelocate}");
        sb.AppendLine($"RelocationAnalysisStart: {(RelocationAnalysisStart.HasValue ? $"${RelocationAnalysisStart.Value:x4}" : "null")}");
        sb.AppendLine($"RelocationAnalysisEnd: {(RelocationAnalysisEnd.HasValue ? $"${RelocationAnalysisEnd.Value:x4}" : "null")}");
        return sb.ToString();
    }
}