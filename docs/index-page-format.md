# Index page format version 1

Index keys are immutable non-empty byte sequences ordered by unsigned lexicographic comparison. Persistent integers are little-endian and every index page uses the common page header and whole-page CRC-32.

## Internal page

| Offset | Width | Field |
|---:|---:|---|
| 32 | 9 | Nullable parent page ID |
| 41 | 2 | Separator count, at least one |
| 43 | 2 | Child count, exactly separators + 1 |
| 45 | 4 | Slot-directory end |
| 49 | 4 | Backward-growing key-data start |
| 53 | 3 | Reserved zero bytes |
| 56 | 8 | First child page ID |

Each forward-growing 16-byte separator slot contains a four-byte key offset, two-byte nonzero key length, two reserved zero bytes, and the eight-byte right-child page ID. Keys are contiguously packed backward from the page end and nondecreasing. Child IDs are unique, nonzero, and not self-referential. An internal page with no separator is invalid; an internal root with only one child is represented only transiently during root contraction and is not persisted.

## Leaf page

The 72-byte leaf header stores nullable parent, previous, and next IDs at offsets 32, 41, and 50; entry count at 59; slot-directory end at 61; key-data start at 65; and three reserved zero bytes at 69. Each 24-byte slot stores key offset/length, two reserved bytes, row page ID, row slot ID, two reserved bytes, and row generation. Keys are contiguously packed backward and nondecreasing, allowing duplicate keys. Row page zero is invalid. Empty leaf pages are valid, including an empty tree root.

## Deletion and page retirement

Deletion targets an exact key and row-ID pair. Underfilled nodes first borrow from a sibling with spare entries; otherwise they merge, update leaf links and parent separators, and contract an internal root that has only one remaining child. A delete result reports every page made unreachable by these operations. Retired pages intentionally remain allocated: callers must defer reuse until a future transaction-aware reclamation layer proves that no active reader can still reference them.

## Unique keys and nulls

Uniqueness is index metadata supplied when the tree is opened; it does not alter the page encoding. A unique tree performs an exact logical-key lookup before insertion and rejects an existing key regardless of row ID. Transactional protection for concurrent check/insert races is deferred to the locking layer. `IndexKey` is an opaque, non-empty byte sequence and therefore has no implicit null value. A key encoder that supports SQL null must emit an explicit canonical null encoding; that encoding participates in uniqueness like any other key, so this version permits at most one encoded null in a unique index. Non-unique indexes accept repeated null encodings and all other duplicate keys.
