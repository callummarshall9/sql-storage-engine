// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed class RandomSequence
{
    private IReadOnlyList<int>? values;
    public int Seed { get; init; }
    public int Maximum { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<int> Values { get => values!; init => values = value; }
}