// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.JsonPayloads;

namespace sql_storage_engine.Exposures.Rows;
public static class JsonPayloadExposure
{
    public static byte[] EncodeJsonPayload(string json) =>
        new JsonPayloadService().EncodeJsonPayload(json: json);

    public static string DecodeJsonPayload(ReadOnlySpan<byte> source) =>
        new JsonPayloadService().DecodeJsonPayload(source: source);
}