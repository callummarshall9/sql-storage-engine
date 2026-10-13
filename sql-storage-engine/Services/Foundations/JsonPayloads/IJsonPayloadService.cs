// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.JsonPayloads;
internal interface IJsonPayloadService
{
    byte[] EncodeJsonPayload(string json);

    string DecodeJsonPayload(ReadOnlySpan<byte> source);
}