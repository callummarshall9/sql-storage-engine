// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Graph;
public sealed record GraphIdentity
{
    public byte Kind { get; init; }
    public Guid DatabaseId { get; init; }
    public ulong TableId { get; init; }
    public ulong SchemaVersion { get; init; }
    public Guid Value { get; init; }
    public byte[] Payload { get => payload!; init => payload = value; }
    public string ParameterName { get => parameterName!; init => parameterName = value; }

    private byte[]? payload;
    private string? parameterName;
}