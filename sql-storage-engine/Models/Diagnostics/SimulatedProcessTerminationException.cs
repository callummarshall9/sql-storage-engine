// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Diagnostics;
public sealed class SimulatedProcessTerminationException : Exception
{
    public SimulatedProcessTerminationException(CrashTermination crashTermination, Exception? innerException = null) : base($"Simulated process termination at {crashTermination.Boundary} boundary {crashTermination.Ordinal}.", innerException)
    {
        Termination = crashTermination;
        Boundary = crashTermination.Boundary;
        Ordinal = crashTermination.Ordinal;
    }

    public CrashTermination Termination { get; }
    public CrashBoundary Boundary { get; }
    public int Ordinal { get; }
}