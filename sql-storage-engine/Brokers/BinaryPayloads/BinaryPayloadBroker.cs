// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using cCoder.CodeAnalysis.Exposures;

namespace sql_storage_engine.Brokers.BinaryPayloads;
internal sealed class BinaryPayloadBroker : IUtilityBroker
{
    public static string ReadBinaryContent(ReadOnlySpan<byte> bytes) =>
        Encoding.Latin1.GetString(bytes: bytes);
}