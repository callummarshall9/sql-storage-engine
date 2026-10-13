// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Rows;
public sealed record SqlXmlSchemaCollection
{
    public string SchemaName { get => schemaName!; init => schemaName = value; }
    public string Name { get => name!; init => name = value; }
    public string DefinitionContent { get => definitionContent!; init => definitionContent = value; }

    private string? schemaName;
    private string? name;
    private string? definitionContent;
    internal IEnumerable<string>? DefinitionSource { get; init; }
}