// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Brokers.SqlTypeDocuments;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections;
internal sealed partial class SqlXmlSchemaCollectionService : ISqlXmlSchemaCollectionService
{
    private readonly ISqlTypeDocumentBroker broker = new SqlTypeDocumentBroker();
    public SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateInput(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        var definitions = sqlXmlSchemaCollection.DefinitionSource!.ToArray();

        if (definitions.Length == 0 || definitions.Any(predicate: string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(message: "XML schema collection definitions cannot be empty.", paramName: nameof(definitions));
        }

        broker.CheckSchemas(definitions: definitions);
        var content = new StringBuilder();

        foreach (var definition in definitions)
        {
            content.Append(value: definition.Length.ToString(provider: CultureInfo.InvariantCulture))
            .Append(value: ':')
            .Append(value: definition);
        }

        return new SqlXmlSchemaCollection
        {
            SchemaName = sqlXmlSchemaCollection.SchemaName,
            Name = sqlXmlSchemaCollection.Name,
            DefinitionContent = content.ToString()
        };
    });

    public IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        return DecodeDefinitions(content: sqlXmlSchemaCollection.DefinitionContent);
    });

    public string ReadSqlXmlSchemaCollectionDefinition(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        return DecodeDefinitions(content: sqlXmlSchemaCollection.DefinitionContent)[0];
    });

    public string ReadSqlXmlSchemaCollectionQualifiedName(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        return $"[{sqlXmlSchemaCollection.SchemaName}].[{sqlXmlSchemaCollection.Name}]";
    });

    private static IReadOnlyList<string> DecodeDefinitions(string content)
    {
        var definitions = new List<string>();
        var offset = 0;

        while (offset < content.Length)
        {
            var colon = content.IndexOf(value: ':', startIndex: offset);

            if (colon < 0 || !int.TryParse(s: content.AsSpan(start: offset, length: colon - offset), style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var length) || length <= 0 || length > content.Length - colon - 1)
            {
                throw new StorageFormatException(message: "XML schema definition content is invalid.");
            }

            definitions.Add(item: content.Substring(startIndex: colon + 1, length: length));
            offset = colon + 1 + length;
        }

        if (definitions.Count == 0)
        {
            throw new StorageFormatException(message: "XML schema definition content is empty.");
        }

        return definitions.AsReadOnly();
    }

    public string CheckSqlXmlSchemaCollectionIdentifier(string value, string parameterName) =>
        TryCatch(operation: () =>
    {
        ValidateIdentifierArguments(value: value, parameterName: parameterName);
        return ValidateIdentifier(value: value, parameterName: parameterName);
    });
}