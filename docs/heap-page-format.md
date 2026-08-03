# Heap page format version 1

Heap pages use the common 32-byte page header documented in `database-file-format.md`. All integers are little-endian. The slot directory grows forward from offset 64; row bytes grow backward from the page end. A page is valid only when `slot directory end <= row data start <= page size` and every live row range lies between the row-data boundary and page end.

## Heap header

| Offset | Width | Field | Validation |
|---:|---:|---|---|
| 32 | 9 | Previous page | Presence byte followed by a `ulong` page ID |
| 41 | 9 | Next page | Presence byte followed by a `ulong` page ID |
| 50 | 2 | Slot count | At most 65,535; must agree with directory end |
| 52 | 4 | Slot directory end | Exactly `64 + slot count * 16` |
| 56 | 4 | Row data start | Between directory end and page size |
| 60 | 4 | Reserved | Zero |

## Slot entry

Each 16-byte entry starts at `64 + slot ID * 16`.

| Relative offset | Width | Field | Validation |
|---:|---:|---|---|
| 0 | 2 | State | `0` unused, `1` live, `2` deleted |
| 2 | 2 | Reserved | Zero |
| 4 | 4 | Row offset | Live slots only; within row region |
| 8 | 4 | Row length | Live slots only; greater than zero and in bounds |
| 12 | 4 | Generation | Incremented before a deleted slot is reused |

The maximum raw row size is the page size minus the 64-byte heap header and one 16-byte slot. Zero-length raw records are not valid. Decoders validate the entire directory before returning any row bytes. Compaction may change live offsets but never slot IDs or generations.

Deletion retains the current generation in a deleted slot. Reuse increments it before publishing the replacement row, so stale `RowId` values cannot resolve to new data. A deleted slot at generation `uint.MaxValue` is permanently retired and a new slot is allocated instead; generations never wrap.
