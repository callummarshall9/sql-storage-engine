// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Diagnostics;
public sealed record FuzzFailure
{
    public string Target { get => _target!; init => _target = value; }
    public byte[] Input { get => _input!; init => _input = value; }
    public string FailureType { get => _failureType!; init => _failureType = value; }
    public string Message { get => _message!; init => _message = value; }

    private string? _target;
    private byte[]? _input;
    private string? _failureType;
    private string? _message;
}