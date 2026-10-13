// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.ClrRuntimes;
internal interface ISqlClrRuntimeBroker
{
    bool Register(string assemblyName, string className, Func<string, ReadOnlyMemory<byte>> parse, Func<ReadOnlyMemory<byte>, string> format, Func<ReadOnlyMemory<byte>, bool> validate, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> compare);

    bool Unregister(string assemblyName, string className);

    ReadOnlyMemory<byte> Parse(string assemblyName, string className, string text);

    string Format(string assemblyName, string className, ReadOnlyMemory<byte> value);

    bool Validate(string assemblyName, string className, ReadOnlyMemory<byte> value);

    int Compare(string assemblyName, string className, ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right);

    (bool Found, bool Valid) TryValidate(string assemblyName, string className, ReadOnlyMemory<byte> value);

    void RegisterAssembly(string assemblyName, byte[] image, string[] classNames, string? [] validationMethods);
}