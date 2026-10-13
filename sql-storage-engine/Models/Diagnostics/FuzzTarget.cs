// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record FuzzTarget
{
    public string Name { get => _name!; init => _name = value; }

    private string? _name;
    public int MaximumInputBytes { get; init; }
    public Action<ReadOnlyMemory<byte>> Decode { get => _decode!; init => _decode = value; }
    public IReadOnlyList<ReadOnlyMemory<byte>> GoldenSeeds { get => _goldenSeeds!; init => _goldenSeeds = value; }

    private Action<ReadOnlyMemory<byte>>? _decode;
    private IReadOnlyList<ReadOnlyMemory<byte>>? _goldenSeeds;
}