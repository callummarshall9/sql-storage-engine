// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace sql_storage_engine.Dependencies.SpecializedIndexPaths;
internal sealed class SpecializedIndexPathDependency : XmlNamespaceManager, IDisposable
{
    private JsonDocument? document;
    public SpecializedIndexPathDependency() : base(nameTable: new NameTable())
    {
    }

    public override string? LookupNamespace(string prefix) =>
        base.LookupNamespace(prefix: prefix);

    public void CheckJsonProperty(string value)
    {
        try
        {
            document?.Dispose();
            document = JsonDocument.Parse(json: value);

            if (document.RootElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(message: "A JSON string is required.");
            }

            _ = document.RootElement.GetString();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(message: "Invalid JSON quoted property.", innerException: exception);
        }
    }

    public void CheckNamespace(string prefix)
    {
        try
        {
            _ = XmlConvert.VerifyNCName(name: prefix);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(message: "Invalid XML namespace prefix.", innerException: exception);
        }
    }

    public void CheckXPath(string path, (string Prefix, string Uri)[] namespaces)
    {
        try
        {
            foreach (var binding in namespaces)
            {
                AddNamespace(prefix: binding.Prefix, uri: binding.Uri);
            }

            var expression = XPathExpression.Compile(xpath: path);
            expression.SetContext(nsManager: this);
        }
        catch (XPathException exception)
        {
            throw new InvalidDataException(message: "Invalid XML index path.", innerException: exception);
        }
    }

    public void Dispose() =>
        document?.Dispose();
}