// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

/// <summary>
/// Represents the severity level of a code relocation diagnostic message.
/// </summary>
public enum CodeRelocationDiagnosticKind
{
    /// <summary>
    /// Detailed tracing information for debugging purposes.
    /// </summary>
    Trace,

    /// <summary>
    /// Debug-level diagnostic information.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning message indicating a potential issue.
    /// </summary>
    Warning,

    /// <summary>
    /// Error message indicating a failure.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error message indicating a severe failure.
    /// </summary>
    Critical,
}