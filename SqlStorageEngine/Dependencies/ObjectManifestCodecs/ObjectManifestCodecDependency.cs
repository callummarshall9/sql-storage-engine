// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using System.Security.Cryptography;

namespace sql_storage_engine.Dependencies.ObjectManifestCodecs;
internal sealed class ObjectManifestCodecDependency : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(hashAlgorithm: HashAlgorithmName.SHA256);
    public byte[] Hash(ReadOnlySpan<byte> bytes)
    {
        hash.AppendData(data: bytes);
        return hash.GetHashAndReset();
    }

    public byte[] EncodeUtf8(string text) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(s: text);

    public string DecodeUtf8(ReadOnlySpan<byte> bytes) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes: bytes);

    public void Dispose() =>
        hash.Dispose();
}