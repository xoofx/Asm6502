// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace Asm6502.Relocator;

/// <summary>
/// Collects and manages diagnostic messages related to code relocation, supporting multiple severity levels and
/// configurable reporting options.
/// </summary>
[DebuggerDisplay("Count = {" + nameof(Messages) + ".Count}, HasErrors = {" + nameof(HasErrors) + "}")]
public class CodeRelocationDiagnosticBag
{
    private readonly List<CodeRelocationDiagnostic> _messages;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeRelocationDiagnosticBag"/> class with default settings.
    /// </summary>
    public CodeRelocationDiagnosticBag()
    {
        _messages = new List<CodeRelocationDiagnostic>();
        LogLevel = CodeRelocationDiagnosticKind.Warning;
    }

    /// <summary>
    /// List of messages.
    /// </summary>
    public List<CodeRelocationDiagnostic> Messages => _messages;

    /// <summary>
    /// If this instance contains error messages.
    /// </summary>
    public bool HasErrors { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable stack trace for each message.
    /// </summary>
    public bool EnableStackTrace { get; set; }

    /// <summary>
    /// Gets or sets the minimum diagnostic level to report for code relocation issues.
    /// </summary>
    public CodeRelocationDiagnosticKind LogLevel { get; set; }

    /// <summary>
    /// Clear all messages.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        HasErrors = false;
    }

    /// <summary>
    /// Copy all the <see cref="Messages"/> in this bag to another bag.
    /// </summary>
    /// <param name="diagnostics">The diagnostics receiving the copy of the <see cref="Messages"/></param>
    public void CopyTo(CodeRelocationDiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        foreach (var diagnosticMessage in Messages)
        {
            diagnostics.Log(diagnosticMessage);
        }
    }

    /// <summary>
    /// Logs the specified <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="message">The diagnostic message</param>
    public void Log(CodeRelocationDiagnostic message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // Always log errors
        if (message.Kind < CodeRelocationDiagnosticKind.Error && message.Kind < LogLevel) return;
        _messages.Add(message);
        if (message.Kind >= CodeRelocationDiagnosticKind.Error)
        {
            HasErrors = true;
        }
    }

    /// <summary>
    /// Log a trace <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Trace(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Trace, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }

    /// <summary>
    /// Log a debug <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Debug(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Debug, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }
    
    /// <summary>
    /// Log an info <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Info(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Info, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }

    /// <summary>
    /// Log an error <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Warning(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (LogLevel > CodeRelocationDiagnosticKind.Warning) return;
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Warning, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }

    /// <summary>
    /// Log an error <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Error(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Error, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }

    /// <summary>
    /// Log an critical <see cref="CodeRelocationDiagnostic"/>.
    /// </summary>
    /// <param name="id">The identifier of the diagnostic.</param>
    /// <param name="message">The text of the message</param>
    public void Critical(CodeRelocationDiagnosticId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log(new CodeRelocationDiagnostic(CodeRelocationDiagnosticKind.Critical, id, message)
        {
            StackTrace = EnableStackTrace ? new StackTrace(1, true) : null
        });
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var diagnosticMessage in Messages)
        {
            builder.AppendLine(diagnosticMessage.ToString());
        }

        return builder.ToString();
    }
}