// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Rows;
public interface ISqlExpressionRuntime
{
    SqlValue Evaluate(string expression, IReadOnlyDictionary<string, SqlValue> columns);
}