// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.ObjectManifestCodecs;

namespace sql_storage_engine.Brokers.ObjectManifestCodecs;
internal sealed class ObjectManifestCodecBroker : IObjectManifestCodecBroker
{
    public byte[] Hash(ReadOnlySpan<byte> bytes)
    {
        using var dependency = new ObjectManifestCodecDependency();
        return dependency.Hash(bytes: bytes);
    }

    public byte[] EncodeUtf8(string text)
    {
        using var dependency = new ObjectManifestCodecDependency();
        return dependency.EncodeUtf8(text: text);
    }

    public string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        using var dependency = new ObjectManifestCodecDependency();
        return dependency.DecodeUtf8(bytes: bytes);
    }
}