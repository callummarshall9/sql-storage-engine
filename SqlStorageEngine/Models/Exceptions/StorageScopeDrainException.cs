// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class StorageScopeDrainException : AggregateException
{
    public StorageScopeDrainException(IEnumerable<Exception> failures) : base(message: "Storage scope cleanup failed.", innerExceptions: failures)
    {
    }
}