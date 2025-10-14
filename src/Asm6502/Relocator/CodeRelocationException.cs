// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Represents errors that occur during code relocation operations.
/// </summary>
public class CodeRelocationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeRelocationException"/> class with a specified diagnostic identifier and error message.
    /// </summary>
    /// <param name="id">The diagnostic identifier for the relocation error.</param>
    /// <param name="message">The message that describes the error.</param>
    public CodeRelocationException(CodeRelocationDiagnosticId id, string message)
        : base(message)
    {
        Id = id;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeRelocationException"/> class with a specified diagnostic identifier, error message, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="id">The diagnostic identifier for the relocation error.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public CodeRelocationException(CodeRelocationDiagnosticId id, string message, Exception inner)
        : base(message, inner)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the diagnostic identifier associated with this relocation exception.
    /// </summary>
    public CodeRelocationDiagnosticId Id { get; }

    /// <inheritdoc />
    public override string Message => $"CR{(int)Id:0000}: {base.Message}";
}