// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class CheckpointPublicationDependencyException : IOException
{
    public CheckpointPublicationDependencyException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }

    public CheckpointPublicationDependencyException(string message) : base(message: message)
    {
    }
}