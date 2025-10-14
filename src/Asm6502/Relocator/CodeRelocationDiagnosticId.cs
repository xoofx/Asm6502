// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace Asm6502.Relocator;

public enum CodeRelocationDiagnosticId
{
    WRN_OutOfBounds,
    WRN_ByteMultipleContributeCannotBeRelocated,
    ERR_RelocationInconsistency,
    TRC_SolverShouldNotBeRelocated,
    TRC_SolverBackTracking,
    TRC_SolverShouldBeRelocated,
    ERR_NoSolutionFound
}