// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.ClrRuntimes;

namespace sql_storage_engine.Brokers.ClrRuntimes;
internal sealed class SqlClrRuntimeBroker : ISqlClrRuntimeBroker
{
    private static readonly SqlClrRuntimeDependency runtime = new();
    public bool Register(string assemblyName, string className, Func<string, ReadOnlyMemory<byte>> parse, Func<ReadOnlyMemory<byte>, string> format, Func<ReadOnlyMemory<byte>, bool> validate, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> compare) =>
        runtime.Register(assemblyName: assemblyName, className: className, parse: parse, format: format, validate: validate, compare: compare);

    public bool Unregister(string assemblyName, string className) =>
        runtime.Unregister(assemblyName: assemblyName, className: className);

    public ReadOnlyMemory<byte> Parse(string assemblyName, string className, string text) =>
        runtime.Parse(assemblyName: assemblyName, className: className, text: text);

    public string Format(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        runtime.Format(assemblyName: assemblyName, className: className, value: value);

    public bool Validate(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        runtime.Validate(assemblyName: assemblyName, className: className, value: value);

    public int Compare(string assemblyName, string className, ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        runtime.Compare(assemblyName: assemblyName, className: className, left: left, right: right);

    public (bool Found, bool Valid) TryValidate(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        runtime.TryValidate(assemblyName: assemblyName, className: className, value: value);

    public void RegisterAssembly(string assemblyName, byte[] image, string[] classNames, string? [] validationMethods) =>
        runtime.RegisterAssembly(assemblyName: assemblyName, image: image, classNames: classNames, validationMethods: validationMethods);
}