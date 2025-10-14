// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;

namespace Asm6502.Relocator;

/// <summary>
/// Represents a diagnostic message related to <see cref="CodeRelocator"/>, including its kind, identifier, and descriptive message.
/// </summary>
/// <param name="Kind">The category or severity of the code relocation diagnostic.</param>
/// <param name="Id">The unique identifier for the specific code relocation diagnostic.</param>
/// <param name="Message">A descriptive message providing details about the code relocation diagnostic.</param>
public record CodeRelocationDiagnostic(CodeRelocationDiagnosticKind Kind, CodeRelocationDiagnosticId Id, string Message)
{
    /// <summary>
    /// Gets the stack trace information associated with the current object, if available.
    /// </summary>
    public StackTrace? StackTrace { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        if (StackTrace is not null)
        {
            return $"{Kind} CR{(uint)Id:0000}: {Message}\n{StackTrace}";
        }
        else
        {
            return $"{Kind} CR{(uint)Id:0000}: {Message}";
        }
    }
}