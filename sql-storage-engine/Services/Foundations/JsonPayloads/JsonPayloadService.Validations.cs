// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.JsonPayloads;
internal sealed partial class JsonPayloadService
{
    private static void ValidateJsonPayload(string json) =>
        ArgumentNullException.ThrowIfNull(argument: json);

    private static void ValidateJsonPayloadSource(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || source[0] != 1)
        {
            throw new StorageFormatException(message: "Unsupported native JSON format version.");
        }
    }
}