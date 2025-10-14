// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public class CodeRelocationException : Exception
{
    public CodeRelocationException(CodeRelocationDiagnosticId id, string message)
        : base(message)
    {
        Id = id;
    }
    public CodeRelocationException(CodeRelocationDiagnosticId id, string message, Exception inner)
        : base(message, inner)
    {
        Id = id;
    }

    public CodeRelocationDiagnosticId Id { get; }
}