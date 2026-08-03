# Logical values and row format

The initial logical type system supports boolean, signed 64-bit integer, text, binary, and SQL `NULL`. `NULL` is a dedicated `SqlValue` and is distinct from `false`, zero, empty text, and empty binary. A column definition has a stable unsigned `ColumnId`, an ordinally unique name, a declared type, and nullability. Schema validation rejects nulls in non-nullable columns, mismatched value types, duplicate IDs/names, and unsupported CLR objects before encoding.

Logical comparison is type-strict and deterministic: integers and booleans use their natural order, text uses ordinal Unicode comparison, and binary uses unsigned lexicographic byte order. Any comparison involving SQL `NULL` returns `Unknown`; two nulls are not treated as a SQL equality result.

Persistent row layouts are versioned and use explicit little-endian integers. No CLR object or runtime type metadata is serialized.

## Version 1 header

| Offset | Width | Field |
|---:|---:|---|
| 0 | 2 | Format version (`1`) |
| 2 | 2 | Column count |
| 4 | 2 | Null-bitmap byte length |
| 6 | 2 | Variable-column count |
| 8 | 4 | Fixed-data length |
| 12 | 4 | Variable-offset-table offset |
| 16 | 4 | Variable-data offset |
| 20 | 4 | Total encoded length |
| 24 | 4 | FNV-1a schema fingerprint over column IDs, types, and nullability |
| 28 | 4 | Reserved zero bytes |

The null bitmap immediately follows the header, one bit per schema column. Fixed fields follow in schema order: booleans occupy one byte (`0` or `1`) and signed integers occupy eight little-endian two's-complement bytes. Null fixed fields retain zero-filled space so later field offsets are schema-derived. Persisted counts and lengths are checked against the supplied schema and the input span before any value array is allocated.

Variable columns have one 12-byte table entry in schema order: two-byte column index, two-byte storage tag (`0` null, `1` inline, `2` overflow), four-byte absolute offset, and four-byte byte length. Variable payloads are contiguous in that same order. Offsets must begin at the declared variable-data boundary and each must equal the previous field's end; this rejects gaps, overlap, decreasing offsets, and out-of-range lengths. Text uses strict UTF-8, while binary bytes are never text-converted. Empty non-null fields have length zero and are distinguished from null by the bitmap and tag. Inline fields are limited to 1 MiB and a complete encoded row to 16 MiB. Overflow entries contain exactly one 16-byte overflow reference.

`OverflowRowCodec` uses a configurable byte threshold: values at or below it stay inline, and larger values receive exclusively owned overflow chains. Encoding results list newly allocated references. Updates reuse unchanged references and list replaced old references as retired; reclaiming retired chains remains a table/transaction responsibility. If construction fails, only newly allocated chains are cleaned up and old references remain intact.

Partial logical updates identify columns by zero-based schema index. The codec first validates and decodes the complete original row, rejects unknown or duplicate indices, and validates every replacement's type and nullability. Only after all checks pass does it build and encode one replacement row, recalculating variable offsets. Exceptions return no partially encoded row and do not mutate the input bytes.
