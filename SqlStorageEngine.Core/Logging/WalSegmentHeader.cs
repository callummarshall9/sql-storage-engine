using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record WalSegmentHeader(DatabaseId DatabaseId, ulong Timeline, ulong SegmentNumber);
