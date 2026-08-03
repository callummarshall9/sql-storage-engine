# Write-ahead log format

WAL version 1 uses a checksummed 48-byte segment header containing canonical big-endian UUID bytes for the database identity, a nonzero timeline, and segment number. Records are length-delimited and contain version, type, LSN, previous transaction LSN, transaction ID, reserved bytes, checksum, and payload. All numeric fields except canonical UUID bytes are little-endian. Payloads are bounded to 16 MiB.

Record types are begin, physical page change, commit, rollback, and checkpoint. CRC-32 protects each complete header and payload. A declared record extending beyond EOF is an incomplete tail; malformed lengths, unknown values, or checksum failures in a complete envelope are corruption.

The appender serializes concurrent writes, assigns LSNs from global byte positions, loops until every record byte is accepted, and advances the logical end only after completion. `FlushThrough` advances the durable LSN only after device flush succeeds. Reopen scans complete records and truncates an incomplete tail to the last validated boundary. Segment rollover occurs before a record that would exceed the configured segment capacity and never changes global record order.

Buffer frames retain the LSN of their newest change. Before writing a dirty page with a nonzero LSN, the WAL flush guard makes the log durable through that LSN. A failed WAL flush prevents the data write and leaves the frame dirty. Clean frames and pages carrying the zero/no-record LSN require no WAL flush.
