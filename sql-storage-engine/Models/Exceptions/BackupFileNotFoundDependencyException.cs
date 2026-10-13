// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class BackupFileNotFoundDependencyException : FileNotFoundException
{
    public BackupFileNotFoundDependencyException(string message, string? fileName) : base(message: message, fileName: fileName)
    {
    }

    public BackupFileNotFoundDependencyException(string message, string? fileName, Exception innerException) : base(message: message, fileName: fileName, innerException: innerException)
    {
    }
}