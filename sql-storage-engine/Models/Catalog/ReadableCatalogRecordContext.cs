// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Catalog;
internal ref struct ReadableCatalogRecordContext
{
    public ReadOnlySpan<byte> Source { get; set; }
    public int Offset { get; set; }
    public ushort Version { get; set; }
    public Dictionary<(string Schema, string Name), SqlXmlSchemaCollection>? XmlSchemas { get; set; }
}