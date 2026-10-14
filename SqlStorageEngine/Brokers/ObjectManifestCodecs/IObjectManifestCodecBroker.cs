// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.ObjectManifestCodecs;
internal interface IObjectManifestCodecBroker
{
    byte[] Hash(ReadOnlySpan<byte> bytes);
    byte[] EncodeUtf8(string text);
    string DecodeUtf8(ReadOnlySpan<byte> bytes);
}