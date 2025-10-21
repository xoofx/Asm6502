// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace Asm6502.Relocator;

/// <summary>
/// Diagnostic identifiers for code relocation operations for <see cref="CodeRelocator"/>.
/// </summary>
public enum CodeRelocationDiagnosticId
{
    /// <summary>
    /// Trace: Solver is backtracking during the relocation process.
    /// </summary>
    TRC_SolverBackTracking = 100,
    
    /// <summary>
    /// Trace: Solver constraint being applied.
    /// </summary>
    TRC_SolverConstraint = 101,
    
    /// <summary>
    /// Trace: Code section should be relocated.
    /// </summary>
    TRC_SolverShouldBeRelocated = 102,
    
    /// <summary>
    /// Trace: Code section should not be relocated.
    /// </summary>
    TRC_SolverShouldNotBeRelocated = 103,

    /// <summary>
    /// Represents the trace event identifier for solver timing information.
    /// </summary>
    TRC_SolverTime = 104,

    // Debug 200
    // Info 300

    /// <summary>
    /// Warning: Byte with multiple contributors cannot be relocated.
    /// </summary>
    WRN_ByteMultipleContributeCannotBeRelocated = 400,
    
    /// <summary>
    /// Warning: Relocation address is out of bounds.
    /// </summary>
    WRN_OutOfBounds = 401,
    
    /// <summary>
    /// Warning: Zero-page read without prior write detected.
    /// </summary>
    WRN_ZeroPageReadWithoutWriting = 402,

    /// <summary>
    /// Error: No valid solution found for code relocation.
    /// </summary>
    ERR_NoSolutionFound = 501,
    
    /// <summary>
    /// Error: Relocation inconsistency detected.
    /// </summary>
    ERR_RelocationInconsistency = 502,
    
    /// <summary>
    /// Indicates that an unexpected error has occurred during the operation.
    /// </summary>
    ERR_UnexpectedError = 503,

    /// <summary>
    /// Represents an error code indicating that the state of the SID registers is inconsistent with the expected state.
    /// </summary>
    ERR_InvalidSIDState = 504,

    /// <summary>
    /// Indicates that the maximum number of allowed cycles has been reached.
    /// </summary>
    ERR_MaxCyclesReached = 505,
}