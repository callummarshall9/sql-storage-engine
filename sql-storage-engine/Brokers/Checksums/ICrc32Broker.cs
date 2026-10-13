// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.Checksums;
internal interface ICrc32Broker
{
    uint CalculateChecksum(ReadOnlySpan<byte> bytes);

    uint CalculateChecksumWithZeroedField(ReadOnlySpan<byte> bytes, int zeroOffset);
}