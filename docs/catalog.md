# Catalog model

The catalog owns stable numeric identities for tables, columns, and indexes. Object names are ordinally compared attributes; renaming an object does not change its identity or physical page references.

Each table record stores its `TableId`, name, schema version, first heap `PageId`, and ordered columns. Each column stores its `ColumnId`, name, SQL type, and nullability. Each index stores its `IndexId`, owning `TableId`, root `PageId`, uniqueness flag, and one or more ordered column references. Every indexed column specifies ascending or descending direction, NULL placement, and an optional collation name.

Catalog validation is performed across the complete record set. Table IDs and names are globally unique. Column IDs and names are unique within a table. Index IDs are globally unique and index names are unique within their table. Every index must reference an existing table and every indexed column must belong to that table. Public collections are immutable snapshots.

## Bootstrap binary format

Catalog format version 1 starts with the four bytes `43 41 54 31` (`CAT1`), a little-endian 16-bit version, two zero reserved bytes, and little-endian 32-bit table and index counts. Table records follow, then index records. Integers are explicitly little-endian; strings are strict UTF-8 prefixed by a 16-bit byte length. Records contain only typed numeric references and fixed enum values, so decoding never depends on a user-table schema. Counts are bounded to 65,535 and strings to 65,535 bytes before allocation. Unknown versions, invalid values, truncation, trailing bytes, and nonzero reserved bytes are rejected. Invalid relationships between otherwise well-formed records are reported as storage corruption.

The encoded record stream is split across catalog pages. After the 32-byte common page header, each page stores a one-byte next-page presence flag, three reserved zero bytes, an eight-byte next `PageId`, and a four-byte payload length. A terminal page has a clear presence flag and zero link bytes. Readers validate each page checksum, identity, type, reserved bytes, payload bound, traversal bound, and absence of link cycles before decoding records.

Table creation validates names, schema versions, columns, and scoped uniqueness before allocating storage. It then creates the initial heap page and writes a replacement catalog chain. The in-memory name/ID cache and catalog root are published only after the heap and catalog pages flush successfully. A failed publication discards and frees the unpublished heap root. Reopening traverses the persisted catalog once and rebuilds the immutable lookup cache.

Secondary-index creation validates the definition, allocates an empty leaf root, scans every live heap row, constructs each composite key from the catalog ordering configuration, and inserts the `(key, RowId)` pair. Unique-key violations or other build failures leave the index unpublished and raise an `IndexBuildException` containing all allocated and unreclaimed page IDs. Successful builds flush before publishing their final (possibly split) root in the catalog and can be reopened for lookup.
