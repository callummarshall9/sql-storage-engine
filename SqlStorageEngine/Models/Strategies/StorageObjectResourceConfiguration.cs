// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Strategies;
internal sealed class StorageObjectResourceConfiguration
{
    public object? Source { get; set; }
    public Func<string, int, CancellationToken, ValueTask<(byte[] Bytes, string Version)?>>? Read { get; set; }
    public Func<string, Stream, CancellationToken, ValueTask>? Upload { get; set; }
    public Func<string, byte[], string?, CancellationToken, ValueTask<string>>? Publish { get; set; }
}