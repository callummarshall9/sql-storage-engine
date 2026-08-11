# SQL Server 2025 data-type coverage

The type model covers every data-type form documented for the SQL Server 2025 (17.x) Database Engine. `SqlType`
represents a complete declaration; `SqlValue` represents the physical value family used by rows and keys. The storage
boundary also implements assignment conversion and rounding/padding, persisted database-default collation, database-generated
identity/rowversion/generated-always values, defaults/computed/check expressions, engine-native binary XML/JSON payloads,
structured hierarchy/spatial/vector operations, and JSON/XML/spatial/vector index methods. Engine-native formats are
validated storage formats, not SQL Server on-disk or TDS binary formats.

| SQL Server category | Declarations |
|---|---|
| Exact numeric | `bit`, `tinyint`, `smallint`, `int`, `bigint`, `decimal(p,s)`, `numeric(p,s)`, `smallmoney`, `money` |
| Approximate numeric | `real`, canonical `float(24)`, canonical `float(53)` |
| Date and time | `date`, `time(s)`, `smalldatetime`, `datetime`, `datetime2(s)`, `datetimeoffset(s)` |
| Character | `char(n)`, `varchar(n|max)`, `text`, `nchar(n)`, `nvarchar(n|max)`, `ntext`, with collation |
| Binary | `binary(n)`, `varbinary(n|max)`, `image`, `timestamp`/`rowversion` |
| Other built-ins | `uniqueidentifier`, typed/untyped `xml`, native `json`, `sql_variant`, `hierarchyid`, `geometry`, `geography`, `vector(dimensions,float32|float16)` |
| Execution-scoped | `cursor`, anonymous `table`, and cataloged user-defined table types |
| User-defined scalar | Alias types over every base type accepted by `CREATE TYPE`; CLR UDTs with opaque byte round-trip |

SQL Server's declaration defaults and limits are enforced: decimal precision 1–38; temporal scale 0–7; character and
binary limits of 8,000 bytes or 4,000 UTF-16 code units; `max`; `float(n)` folding to 24 or 53 bits; vector limits of
1,998 float32 or float16 elements; rowversion width 8; and hierarchyid serialized width at most 892 bytes.
Legacy LOB types are retained even though new schemas should prefer `max` types. Overflow chains support the SQL LOB
ceiling of `2^31-1` bytes.

`SqlConversion.ConvertTo(target, source, value)` retains source-declaration-sensitive rules, including `money`-to-integer
and legacy `datetime`-to-integer rounding versus numeric/float truncation. Direct computed-column references use that typed
path automatically; computed dependencies are cycle-checked and evaluated in dependency order.

`IDENTITY` supports every SQL Server exact-integer target, including `decimal(38,0)` and `numeric(38,0)` seeds and
increments beyond CLR `Int64`; its next value is catalog-persisted before later row validation, so rejected inserts
still consume generated identity values.

ISO spellings are constructor aliases and normalize to the SQL Server metadata type: `binary varying`, `char varying`,
`character`, `character varying`, `dec`, `double precision`, `integer`, `national character`, `national char`, both
national varying forms, `national text`, and `rowversion`/`timestamp`. `sysname` normalizes to `nvarchar(128)`.
Synonym spelling is deliberately not persisted because SQL Server metadata doesn't retain it.

Typed XML retains `CONTENT` versus `DOCUMENT` and references a shared database-scoped XML schema collection. Collections
have create/alter/drop operations and dependency validation. XSDs are compiled when catalog metadata is read, and values
are schema-validated before binary XML encoding. Untyped XML accepts well-formed fragments and prohibits DTDs.

Alias types retain their schema-qualified identity, exact native base declaration, and default nullability. The catalog
allows only the native bases accepted by SQL Server `CREATE TYPE`. CLR UDTs retain their schema-qualified identity,
assembly/class binding, native or user-defined serialization, user-defined maximum size (1–8,000 or `-1` for LOB),
`IsByteOrdered`, `IsFixedLength`, and optional validation-method name. Rows can retrieve CLR values as raw bytes without
loading the assembly. Native serialization correctly omits `MaxByteSize`. Trusted hosts can bind parse, format,
validation, and comparison behavior through `ISqlClrTypeRuntime`. Whole-value B-tree keys require byte ordering.

User-defined table types retain defaults, identity and ROWGUIDCOL attributes, computed columns, check constraints,
clustered/nonclustered/hash indexes, included columns, duplicate-key options, bucket counts, and memory optimization.
`TableSqlValue` and `CursorSqlValue` supply execution-scoped values; neither is legal in an ordinary persisted column.
Disk table values generate monotonic execution-local rowversion values for `DEFAULT`/`NULL` placeholders and reject
explicit rowversion assignment; persisted tables use the durable database-wide allocator.
Memory-optimized table types use their narrower DDL surface: supported native types only, `IDENTITY(1,1)`, no defaults,
computed/ROWGUIDCOL/check facets, and nonclustered or hash indexes without INCLUDE/IGNORE_DUP_KEY/standalone UNIQUE.
Their ordered indexes enforce the 2,500-byte declared-key limit; hash indexes accept bucket counts through 1,073,741,824
without incorrectly inheriting the 1,700-byte rowstore runtime limit.

Disk tables physically omit null `SPARSE` values and expose the XML column set as a virtual, directly updatable projection.
Dynamic masking is available through permission-aware reads, including column-granular unmasking. Deterministic and
randomized encrypted columns use authenticated provider-backed payloads; deterministic values may be indexed subject to SQL
type/collation rules, while randomized values cannot be keys. These encryption envelopes are local provider contracts, not
SQL Server driver-compatible Always Encrypted envelopes. `FILESTREAM` is currently retained as validated catalog metadata;
its values use ordinary overflow chains rather than an external NTFS/Win32 streaming store. Generated-always columns allocate
values, but system-versioned history tables and ledger cryptographic verification are outside this storage boundary.

Specialized indexes maintain durable keys and SQL Server declaration restrictions. A JSON index recursively indexes its
promoted subtree and supports property, wildcard, numeric-index, range, list, and `last` path seeks through 128 path levels.
Primary XML indexes retain namespace-expanded element and attribute names plus text/CDATA, comment, and processing-
instruction nodes; selective XML indexes persist prefix bindings, promote element/attribute/text values, allow at most 1,024 paths and one selective index per XML column, and reject computed
sources or documents deeper than 128 element levels. Vector nearest-neighbor and spatial nearest-neighbor calls currently
evaluate durable index entries exactly; they do not claim SQL Server DiskANN or spatial-grid plan/performance parity. XML
keys likewise do not claim SQL Server's internal node-table binary layout or XQuery optimizer implementation.

Character storage uses the declared collation code page (`_UTF8` uses UTF-8; national types use UTF-16). Collation keys
retain the SQL comparison tuple used by `sql_variant`: LCID, version, comparison flags, and sort ID. `char`, `nchar`, and
`binary` are padded on assignment, while temporal inputs are rounded to their declared subtype and scale.
