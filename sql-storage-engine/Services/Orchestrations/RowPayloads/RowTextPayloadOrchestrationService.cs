// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Services.Foundations.JsonPayloads;
using sql_storage_engine.Services.Foundations.XmlPayloads;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowTextPayloadOrchestrationService : IRowTextPayloadOrchestrationService
{
    private static readonly SqlCollation UnicodeStorageCollation = new SqlCollation
    {
        Name = "ROW_UTF16",
        CodePage = 1200,
        CultureName = string.Empty
    };
    private readonly SqlCollationService collations;
    private readonly JsonPayloadService json;
    private readonly XmlPayloadService xml;
    public RowTextPayloadOrchestrationService(SqlCollationService collations, JsonPayloadService json, XmlPayloadService xml)
    {
        this.collations = collations;
        this.json = json;
        this.xml = xml;
    }

    public RowPayloadContext WriteRowPayloadContextText(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Bytes = EncodeText(value: ((TextSqlValue)rowPayloadContext.Value!).Value, type: rowPayloadContext.Column!.Type);
        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextText(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);

        rowPayloadContext.Value = new TextSqlValue
        {
            Value = DecodeText(source: rowPayloadContext.Source!, type: rowPayloadContext.Column!.Type),
            StorageFamily = SqlStorageFamily.Text
        };

        return rowPayloadContext;
    });

    private byte[] EncodeText(string value, SqlType type)
    {
        if (type.Name == SqlTypeName.Json)
        {
            return json.EncodeJsonPayload(json: value);
        }

        if (type.Name == SqlTypeName.Xml)
        {
            return xml.EncodeXmlPayload(xml: value);
        }

        byte[] bytes;

        if (type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText)
        {
            bytes = collations.EncodeSqlCollation(sqlCollation: UnicodeStorageCollation, value: value);
        }
        else
        {
            bytes = collations.EncodeSqlCollation(sqlCollation: ((type.Collation is null ? null : collations.ParseSqlCollation(name: type.Collation)) ?? collations.ReadDefaultSqlCollation()), value: value);
        }

        if (type.Name is not (SqlTypeName.Char or SqlTypeName.NChar) || type.Length is not { } declared)
        {
            return bytes;
        }

        var targetLength = type.Name == SqlTypeName.NChar ? checked(declared * 2) : declared;

        if (bytes.Length == targetLength)
        {
            return bytes;
        }

        var padded = new byte[targetLength];
        bytes.CopyTo(array: padded, index: 0);

        if (type.Name == SqlTypeName.NChar)
        {
            for (var offset = bytes.Length; offset < padded.Length; offset += 2)
            {
                padded[offset] = 0x20;
            }
        }
        else
        {
            padded.AsSpan(start: bytes.Length)
            .Fill(value: 0x20);
        }

        return padded;
    }

    private string DecodeText(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.Name == SqlTypeName.Json)
        {
            return json.DecodeJsonPayload(source: source);
        }

        if (type.Name == SqlTypeName.Xml)
        {
            return xml.DecodeXmlPayload(source: source);
        }

        return type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText ? collations.DecodeSqlCollation(sqlCollation: UnicodeStorageCollation, value: source) : collations.DecodeSqlCollation(sqlCollation: ((type.Collation is null ? null : collations.ParseSqlCollation(name: type.Collation)) ?? collations.ReadDefaultSqlCollation()), value: source);
    }

    public RowPayloadContext WriteRowPayloadContextUtf8(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateUtf8Text(rowPayloadContext: rowPayloadContext);
        var value = ((TextSqlValue)rowPayloadContext.Value!).Value;
        rowPayloadContext.Bytes = new byte[collations.CountSqlCollationUtf8(value: value)];
        collations.WriteSqlCollationUtf8(destination: rowPayloadContext.Bytes, value: value);
        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextUtf8(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateUtf8Source(rowPayloadContext: rowPayloadContext);

        rowPayloadContext.Value = new TextSqlValue
        {
            Value = collations.ReadSqlCollationUtf8(value: rowPayloadContext.Source!),
            StorageFamily = SqlStorageFamily.Text
        };

        return rowPayloadContext;
    });

    internal static RowTextPayloadOrchestrationService CreateRowTextPayloadOrchestrationService() =>
        new RowTextPayloadOrchestrationService(collations: new SqlCollationService(), json: new JsonPayloadService(), xml: new XmlPayloadService());

    public RowPayloadContext ReadRowPayloadContextColumnSet(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetText(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.ColumnSetElements = xml.ReadColumnSetXmlElements(xml: rowPayloadContext.Text!);
        return rowPayloadContext;
    });

    public RowPayloadContext WriteRowPayloadContextColumnSet(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetElements(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Text = xml.WriteColumnSetXmlElements(columnSetXmlElements: rowPayloadContext.ColumnSetElements!);
        return rowPayloadContext;
    });
}