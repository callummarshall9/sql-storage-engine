# Database file format version 1

All integers are little-endian. Every page is the page size recorded below. The common header checksum is IEEE CRC-32 over the entire page with bytes 28–31 treated as zero. UUID bytes use RFC 4122/network field order.

| Offset | Width | Field | Validation |
|---:|---:|---|---|
| 0 | 8 | Page ID | Zero for the database header |
| 8 | 2 | Page type | `1` (database header) |
| 10 | 2 | Page format | `1` |
| 12 | 8 | Page LSN | Zero before WAL exists |
| 20 | 2 | Checksum algorithm | `1` (IEEE CRC-32) |
| 22 | 6 | Reserved | All zero |
| 28 | 4 | Checksum | CRC-32 as described above |
| 32 | 8 | Magic | ASCII `SQLSTORE` |
| 40 | 16 | Database UUID | Canonical RFC 4122 bytes |
| 56 | 2 | Database format | `1` |
| 58 | 1 | Clean shutdown | `0` or `1` |
| 59 | 1 | Reserved | Zero |
| 60 | 4 | Page size | Power of two, 4096–65536 |
| 64 | 9 | Catalog root | Presence byte, then `ulong` page ID |
| 73 | 9 | Free-list head | Presence byte, then `ulong` page ID |
| 82 | 8 | Next table ID | Unsigned counter |
| 90 | 8 | Next index ID | Unsigned counter |
| 98 | 8 | Next transaction ID | Unsigned counter |
| 106 | 8 | Next page ID | At least 1 |
| 114 | remainder | Reserved | Written as zero |

Database creation writes and flushes a uniquely named sibling temporary file, publishes it with a no-overwrite rename, then opens the published file. POSIX directory durability requires a filesystem/platform offering directory `fsync`; callers must qualify that guarantee for their deployment.
