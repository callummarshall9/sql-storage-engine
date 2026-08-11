# Logical values and row format

`SqlType` represents the complete SQL Server 2025 system-type declaration rather than a physical storage tag. It covers
all exact and approximate numerics; all six temporal types; fixed, varying, Unicode, `max`, and legacy LOB strings;
fixed, varying, `max`, image, and rowversion binary values; unique identifiers; XML, JSON, `sql_variant`, hierarchy,
geometry, geography, and float16/float32 vectors. Precision, scale, length, `max`, collation, vector dimensions/base
type, typed-XML schema/content constraints, alias identity/base type, and CLR serialization contracts are retained in
schema metadata. `cursor`, anonymous `table`, and named table types are execution-scoped and aren't embedded as a
field of an ordinary persisted row.

Logical values use thirteen shared `SqlStorageFamily` representations plus typed hierarchy/spatial/runtime wrappers. The declared `SqlType` validates the SQL Server
range and facets before a value enters a row: integer widths, decimal/numeric precision up to 38, money ranges, real/
float precision, temporal ranges and fractional scale, character/binary lengths, XML/JSON syntax, rowversion width,
`sql_variant` restrictions, hierarchyid's 892-byte representation limit, CLR UDT serialized size, and vector shape.
Typed XML is checked against every XSD document in its schema collection. CLR `decimal` remains a convenience input; `SqlDecimal` carries values
outside CLR decimal's 29-digit range. `NULL` is a dedicated `SqlValue` and is distinct from every non-null default.

Logical comparison is type-strict and deterministic: numeric and temporal values use their natural order, binary uses
unsigned lexicographic byte order, date-time offsets compare by instant, hierarchy values use path order, and unique
identifiers use SQL Server's `SqlGuid` order. Collated keys use their declared SQL metadata. Any comparison involving SQL `NULL` returns `Unknown`; two nulls are not
treated as a SQL equality result.

Persistent row layouts are versioned and use explicit little-endian integers. No CLR object or runtime type metadata is serialized.

## Version 4 header

| Offset | Width | Field |
|---:|---:|---|
| 0 | 2 | Format version (`4`) |
| 2 | 2 | Column count |
| 4 | 2 | Null-bitmap byte length |
| 6 | 2 | Variable-column count |
| 8 | 4 | Fixed-data length |
| 12 | 4 | Variable-offset-table offset |
| 16 | 4 | Variable-data offset |
| 20 | 4 | Total encoded length |
| 24 | 4 | FNV-1a schema fingerprint over column IDs, complete type facets, nullability, and encryption metadata |
| 28 | 4 | Reserved zero bytes |

The null bitmap immediately follows the header, one bit per schema column. Fixed fields follow in schema order. Bit and
integer widths are 1, 1, 2, 4, or 8 bytes according to the declared SQL type. `real` and canonical `float(24)` use IEEE binary32;
canonical `float(53)` uses binary64. Decimal/numeric values use SQL Server's precision-dependent 5/9/13/17-byte layout: one
sign byte followed by a little-endian unsigned coefficient at the column's declared scale. Smallmoney and money use
scaled signed 32- and 64-bit integers. Dates occupy four bytes, temporal tick values eight, datetime-offset ten (UTC
ticks plus signed offset minutes), rowversion eight, and GUIDs sixteen in RFC 4122/network byte order. Date-times have
no CLR `DateTimeKind`; date-time offsets retain their original offset. Null fixed fields keep zero-filled space so later
offsets remain schema-derived.

Variable and encrypted columns have one 12-byte table entry in schema order: two-byte column index, two-byte storage tag (`0` null,
`1` inline, `2` overflow), four-byte absolute offset, and four-byte byte length. Variable payloads are contiguous in
that same order. Null sparse values and virtual XML column sets have no directory entry; populated sparse values are encoded
as typed variable entries even when their base type is normally fixed-width. National text uses strict little-endian UTF-16; non-Unicode text uses its collation code page, including
UTF-8 collations. XML and JSON use distinct versioned binary payloads. Hierarchy and spatial values use validated typed
serializations, vectors use packed IEEE elements, CLR UDTs use serialized bytes, and `sql_variant` carries nested type metadata. Alias types use the exact physical encoding of their native base type. Empty non-null fields have length
zero and remain distinct from null. Inline fields are limited to 1 MiB and a complete encoded row to 16 MiB. Overflow
entries contain exactly one 16-byte overflow reference.

Before encoding, SQL assignment semantics canonicalize values: decimal/money and temporal scales round, `real` folds to
binary32, and fixed character/binary values pad to their declaration. Index lookup applies the same normalization, so
an assignment-form value and its stored form always produce the same key.

Ordinary B-tree keys accept the types SQL Server permits as comparable key columns. Legacy LOBs, `max` values, XML,
JSON, spatial values, vectors, and non-byte-ordered CLR UDTs are rejected by ordinary B-trees. JSON-path, XML, spatial, and
vector index methods are explicit catalog options and maintain durable keys. Nearest-neighbor operations currently perform
exact evaluation over those entries rather than implementing SQL Server DiskANN or spatial-grid internals. Byte-ordered CLR UDT keys compare their
serialized representation. Text keys use the column collation (or index override), SQL trailing-space
comparison behavior, and escaped terminators so composite byte ordering remains lexical. `BIN2`/ordinal collations use
big-endian UTF-16 code-unit order for Unicode types; legacy `BIN` compares the first WCHAR by code point and remaining
little-endian storage bytes. Character types use declared code-page order; CI/AI/KI/WI flags
produce deterministic locale sort keys. `sql_variant` prefixes character keys with LCID, version, flags, and sort ID.
Rowstore B-tree declarations allow 32 key columns and 1,023 included columns. Fixed-width declarations that cannot fit are
rejected at DDL; variable-width declarations are accepted and their normalized value bytes are checked on every build,
insert, update, and lookup against the 900-byte clustered or 1,700-byte nonclustered limit. The logical width calculation
uses SQL Server's precision/scale-dependent decimal and temporal widths rather than this engine's row-codec widths.

`OverflowRowCodec` uses a configurable byte threshold: values at or below it stay inline, and larger values receive exclusively owned overflow chains. Encoding results list newly allocated references. Updates reuse unchanged references and list replaced old references as retired; reclaiming retired chains remains a table/transaction responsibility. If construction fails, only newly allocated chains are cleaned up and old references remain intact.

Partial logical updates identify columns by zero-based schema index. The codec first validates and decodes the complete original row, rejects unknown or duplicate indices, and validates every replacement's type and nullability. Only after all checks pass does it build and encode one replacement row, recalculating variable offsets. Exceptions return no partially encoded row and do not mutate the input bytes.
