// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
internal sealed partial class SpecializedIndexPathService
{
    private static void ValidateText(string value) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: value);

    private static void ValidateXPath(string path, (string Prefix, string Uri)[] namespaces)
    {
        ValidateText(value: path);
        ArgumentNullException.ThrowIfNull(argument: namespaces);

        foreach (var binding in namespaces)
        {
            ValidateText(value: binding.Prefix);
            ValidateText(value: binding.Uri);
        }
    }
}