using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public enum WalRecordType : ushort { Begin = 1, PageChange = 2, Commit = 3, Rollback = 4, Checkpoint = 5, Compensation = 6 }
