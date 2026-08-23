# Catalog model

The catalog owns stable numeric identities for tables, columns, and indexes. Ordinary table names carry database,
schema, and object components and are compared ordinally as a tuple. Unqualified lookup succeeds only when exactly one
table has that object name, so an executor cannot silently bind an ambiguous table. Renaming an object does not change
its identity or physical page references.

Each table record stores its `TableId`, qualified name, schema version, first heap `PageId`, checks, durable identity allocation state, optional system-versioning relationship, and ordered columns. System-versioning metadata identifies the history `TableId` and exact period-start/period-end `ColumnId` values; the paired history table must retain the same ordered names, IDs, types, and nullability. Each column stores its `ColumnId`, name, complete SQL Server type declaration, nullability, defaults/identity/computed metadata, sparse/column-set/FILESTREAM flags, masking and Always Encrypted metadata, and generated-always/hidden facets. A type declaration retains its system type name and all applicable precision, scale, length/`max`, collation, vector, typed-XML, alias, and CLR-serialization facets. Each index stores its `IndexId`, owning `TableId`, root `PageId`, uniqueness, clustered/primary/duplicate-key options, included columns, and one or more ordered column references. Every indexed column specifies ascending or descending direction, NULL placement, and an optional collation override.

Database-scoped records include XML schema collections, CLR assemblies, scalar alias/CLR types, and table types. XML collections are shared dependency targets rather than XSD documents duplicated in every column. CLR assembly records retain identity, permission set, optional image, and type bindings. Native CLR serialization omits `MaxByteSize`; user-defined serialization retains its finite or LOB limit. Table types retain defaults, identity, ROWGUIDCOL, computed/PERSISTED columns, checks, memory optimization, and complete clustered/nonclustered/hash index options.

Catalog validation is performed across the complete record set. Table IDs and names are globally unique. Column IDs and names are unique within a table. Index IDs are globally unique and index names are unique within their table. Every index must reference an existing table and every indexed column must belong to that table. Defaults cannot reference table columns; computed-column dependencies must be acyclic and are evaluated in topological rather than physical column order. User-defined scalar and table types share a schema-qualified namespace; every column UDT reference must match a registered scalar type, and one CLR assembly/class can bind only one UDT. Public collections are immutable snapshots.

Spatial indexes may optionally declare one exact admitted SRID. An exact-SRID index stores only non-NULL spatial values
with that SRID, while the parameterless spatial-index option retains the existing all-SRID behavior. The immutable option
is persisted so query planners can reject incompatible nearest-search access before opening the index.

Cosine vector indexes admit only non-NULL vectors with a nonzero norm, and cosine nearest queries likewise require a
nonzero query vector. This makes the persisted metric a usable exact-error capability for query planners. A legacy index
containing a zero vector raises `NonFiniteVectorDistanceException` instead of silently omitting that candidate.

## Bootstrap binary format

Catalog format version 9 starts with the four bytes `43 41 54 39` (`CAT9`), a little-endian 16-bit version, two zero reserved bytes, and 32-bit table, index, scalar-type, table-type, XML-collection, and assembly counts. XML collections and assemblies precede table/index/type records so typed XML and CLR declarations resolve shared identities while decoding. Table records encode database and schema before object name and end with optional system-versioning metadata. Versions 6 and 7 remain readable, and version 8 catalogs decode spatial indexes with the all-SRID policy used by that release. The next catalog publication upgrades an older form to version 9. Typed XML type records store only collection identity. Index records retain B-tree, JSON-path, namespace-bound XML, spatial SRID, or vector method metadata. An exact-SRID spatial index rejects incompatible non-NULL values during build and mutation, so its published SRID is a table-value capability rather than a lossy row filter.

Integers are explicitly little-endian; strings are strict UTF-8 prefixed by a 32-bit byte length. Counts are bounded to 65,535; total catalog size is bounded by the 65,536-page catalog traversal limit. Unknown versions or type names, invalid facets, malformed XML schemas, truncation, trailing bytes, and nonzero reserved bytes are rejected. Invalid relationships between otherwise well-formed records are reported as storage corruption. Earlier catalog versions are intentionally not decoded because no released database depends on them.

The encoded record stream is split across catalog pages. After the 32-byte common page header, each page stores a one-byte next-page presence flag, three reserved zero bytes, an eight-byte next `PageId`, and a four-byte payload length. A terminal page has a clear presence flag and zero link bytes. Readers validate each page checksum, identity, type, reserved bytes, payload bound, traversal bound, and absence of link cycles before decoding records.

Table creation validates names, schema versions, columns, and scoped uniqueness before allocating storage. It then creates the initial heap page and writes a replacement catalog chain. The in-memory name/ID cache and catalog root are published only after the heap and catalog pages flush successfully. A failed publication discards and frees the unpublished heap root. Reopening traverses the persisted catalog once and rebuilds the immutable lookup cache.

The public catalog and engine expose the database header's persisted `DatabaseId`. Consumers can include this
incarnation in prepared bindings so an identically shaped table from another database does not satisfy stale-plan
validation. `IStorageStatement.CreateTableAsync` brings table creation under the durable database before-image; a
statement can publish a target catalog record and its populated heap together, or restore both on failure/restart.

Secondary-index creation validates the definition, allocates an empty leaf root, scans every live heap row, constructs each composite key from the catalog ordering configuration, and inserts the `(key, RowId)` pair. Unique-key violations or other build failures leave the index unpublished and raise an `IndexBuildException` containing all allocated and unreclaimed page IDs. Successful builds flush before publishing their final (possibly split) root in the catalog and can be reopened for lookup.

`GetTableStatisticsAsync` and `GetIndexStatisticsAsync` return immutable, point-in-time optimizer snapshots. Table
statistics contain exact row, heap-page, per-column NULL, and per-column distinct counts. B-tree index statistics contain
entry/distinct-key/leaf-page counts and up to 200 equi-depth key histogram steps. Statistics are calculated on demand
under the statement read gate in this release; they are not persisted and do not silently become stale catalog facts.
