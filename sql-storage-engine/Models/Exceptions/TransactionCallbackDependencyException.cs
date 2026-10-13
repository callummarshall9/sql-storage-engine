// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Retains an I/O callback failure and its original cause.</summary>
public sealed class TransactionCallbackDependencyException : IOException
{
    public TransactionCallbackDependencyException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}