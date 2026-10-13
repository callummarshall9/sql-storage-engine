// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace sql_storage_engine.Dependencies.ClrRuntimes;
internal sealed class SqlClrRuntimeDependency : AssemblyLoadContext
{
    private readonly ConcurrentDictionary<(string Assembly, string Class), (Func<string, ReadOnlyMemory<byte>> Parse, Func<ReadOnlyMemory<byte>, string> Format, Func<ReadOnlyMemory<byte>, bool> Validate, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> Compare)> bindings = new();
    public SqlClrRuntimeDependency() : base(name: "sql-storage-engine.trusted-clr", isCollectible: false)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName) =>
        null;

    public bool Register(string assemblyName, string className, Func<string, ReadOnlyMemory<byte>> parse, Func<ReadOnlyMemory<byte>, string> format, Func<ReadOnlyMemory<byte>, bool> validate, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> compare) =>
        bindings.TryAdd(key: (assemblyName, className), value: (parse, format, validate, compare));

    public bool Unregister(string assemblyName, string className) =>
        bindings.TryRemove(key: (assemblyName, className), value: out _);

    public ReadOnlyMemory<byte> Parse(string assemblyName, string className, string text) =>
        Require(assemblyName: assemblyName, className: className)
            .Parse(arg: text);

    public string Format(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        Require(assemblyName: assemblyName, className: className)
            .Format(arg: value);

    public bool Validate(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        Require(assemblyName: assemblyName, className: className)
            .Validate(arg: value);

    public int Compare(string assemblyName, string className, ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        Require(assemblyName: assemblyName, className: className)
            .Compare(arg1: left, arg2: right);

    public (bool Found, bool Valid) TryValidate(string assemblyName, string className, ReadOnlyMemory<byte> value) =>
        bindings.TryGetValue(key: (assemblyName, className), value: out var runtime) ? (true, runtime.Validate(arg: value)) : (false, false);

    private (Func<string, ReadOnlyMemory<byte>> Parse, Func<ReadOnlyMemory<byte>, string> Format, Func<ReadOnlyMemory<byte>, bool> Validate, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> Compare) Require(string assemblyName, string className) =>
        bindings.TryGetValue(key: (assemblyName, className), value: out var runtime) ? runtime : throw new InvalidOperationException(message: $"No trusted CLR runtime is registered for [{assemblyName}].[{className}].");

    public void RegisterAssembly(string assemblyName, byte[] image, string[] classNames, string? [] validationMethods)
    {
        using var stream = new MemoryStream(image, writable: false);
        var loaded = LoadFromStream(assembly: stream);

        for (var index = 0; index < classNames.Length; index++)
        {
            var type = loaded.GetType(name: classNames[index], throwOnError: true, ignoreCase: false)!;
            var parse = type.GetMethod(name: "Parse", bindingAttr: BindingFlags.Public | BindingFlags.Static, types: [typeof(string)]) ?? throw new ArgumentException(message: $"CLR UDT '{type.FullName}' has no public static Parse(string).");
            var write = type.GetMethod(name: "Write", bindingAttr: BindingFlags.Public | BindingFlags.Instance, types: [typeof(BinaryWriter)]) ?? throw new ArgumentException(message: $"CLR UDT '{type.FullName}' has no public Write(BinaryWriter).");
            var read = type.GetMethod(name: "Read", bindingAttr: BindingFlags.Public | BindingFlags.Instance, types: [typeof(BinaryReader)]) ?? throw new ArgumentException(message: $"CLR UDT '{type.FullName}' has no public Read(BinaryReader).");
            var methodName = validationMethods[index];
            var validate = methodName is null ? null : type.GetMethod(name: methodName, bindingAttr: BindingFlags.Public | BindingFlags.Instance, types: Type.EmptyTypes) ?? throw new ArgumentException(message: $"CLR UDT validation method '{methodName}' was not found.");

            if (validate is not null && validate.ReturnType != typeof(bool))
            {
                throw new ArgumentException(message: "CLR UDT validation methods must return bool.");
            }

            if (!Register(assemblyName: assemblyName, className: classNames[index], parse: text => Serialize(instance: parse.Invoke(obj: null, parameters: [text]) ?? throw new FormatException(message: "CLR Parse returned null."), write: write), format: bytes => Deserialize(value: bytes, type: type, read: read)
            .ToString() ?? string.Empty, validate: bytes => validate is null || (bool)validate.Invoke(obj: Deserialize(value: bytes, type: type, read: read), parameters: null)!, compare: (left, right) => CompareInstances(left: Deserialize(value: left, type: type, read: read), right: Deserialize(value: right, type: type, read: read))))
            {
                throw new InvalidOperationException(message: $"A CLR runtime is already registered for [{assemblyName}].[{classNames[index]}].");
            }
        }
    }

    private static int CompareInstances(object left, object right) =>
        left is IComparable comparable ? comparable.CompareTo(obj: right) : throw new InvalidOperationException(message: $"CLR UDT '{left.GetType()
            .FullName}' does not implement IComparable.");

    private static byte[] Serialize(object instance, MethodInfo write)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            write.Invoke(obj: instance, parameters: [writer]);
        }

        return stream.ToArray();
    }

    private static object Deserialize(ReadOnlyMemory<byte> value, Type type, MethodInfo read)
    {
        var instance = Activator.CreateInstance(type: type) ?? throw new InvalidOperationException(message: $"CLR UDT '{type.FullName}' requires a public parameterless constructor.");
        using var stream = new MemoryStream(value.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        read.Invoke(obj: instance, parameters: [reader]);

        if (stream.Position != stream.Length)
        {
            throw new FormatException(message: "CLR UDT did not consume its serialized payload.");
        }

        return instance;
    }
}