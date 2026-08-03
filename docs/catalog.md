# Catalog model

The catalog owns stable numeric identities for tables, columns, and indexes. Object names are ordinally compared attributes; renaming an object does not change its identity or physical page references.

Each table record stores its `TableId`, name, schema version, first heap `PageId`, and ordered columns. Each column stores its `ColumnId`, name, SQL type, and nullability. Each index stores its `IndexId`, owning `TableId`, root `PageId`, uniqueness flag, and one or more ordered column references. Every indexed column specifies ascending or descending direction, NULL placement, and an optional collation name.

Catalog validation is performed across the complete record set. Table IDs and names are globally unique. Column IDs and names are unique within a table. Index IDs are globally unique and index names are unique within their table. Every index must reference an existing table and every indexed column must belong to that table. Public collections are immutable snapshots.
