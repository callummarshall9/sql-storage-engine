using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Rows;

/// <summary>Host-provided behavior for one SQL CLR UDT. The storage engine never loads untrusted assemblies itself.</summary>
public interface ISqlClrTypeRuntime
{
    ReadOnlyMemory<byte> Parse(string text);
    string Format(ReadOnlyMemory<byte> serializedValue);
    bool Validate(ReadOnlyMemory<byte> serializedValue);
    int Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right);
}

/// <summary>Process-local binding of cataloged assembly/class identities to trusted CLR implementations.</summary>
public static class SqlClrRuntime
{
    private static readonly ConcurrentDictionary<(string Assembly, string Class), ISqlClrTypeRuntime> Bindings = new();

    public static void Register(string assemblyName, string className, ISqlClrTypeRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentNullException.ThrowIfNull(runtime);
        if (!Bindings.TryAdd((assemblyName, className), runtime))
            throw new InvalidOperationException($"A CLR runtime is already registered for [{assemblyName}].[{className}].");
    }

    public static bool Unregister(string assemblyName, string className) => Bindings.TryRemove((assemblyName, className), out _);

    /// <summary>Explicitly loads a cataloged assembly into the trusted host and binds its declared SQL UDT classes.</summary>
    public static void RegisterAssembly(CatalogAssembly assembly, IEnumerable<SqlType> declaredTypes)
    {
        ArgumentNullException.ThrowIfNull(assembly); ArgumentNullException.ThrowIfNull(declaredTypes);
        if (assembly.Image.IsEmpty) throw new ArgumentException("The catalog assembly has no deployable image.", nameof(assembly));
        Assembly loaded;
        using (var stream = new MemoryStream(assembly.Image.ToArray(), writable: false)) loaded = AssemblyLoadContext.Default.LoadFromStream(stream);
        foreach (var declaration in declaredTypes)
        {
            if (declaration.UserTypeKind != SqlUserTypeKind.Clr || !StringComparer.Ordinal.Equals(declaration.ClrAssemblyName, assembly.Name))
                throw new ArgumentException("Every declaration must be a CLR UDT bound to the supplied assembly.", nameof(declaredTypes));
            var runtimeType = loaded.GetType(declaration.ClrClassName!, throwOnError: true, ignoreCase: false)!;
            Register(assembly.Name, declaration.ClrClassName!, new ReflectionRuntime(runtimeType, declaration.ClrValidationMethodName));
        }
    }

    public static SqlValue Parse(SqlType type, string text)
    {
        var runtime = Require(type); var value = runtime.Parse(text);
        if (!runtime.Validate(value)) throw new FormatException($"Parsed value failed validation for {type.QualifiedUserTypeName}.");
        return SqlValue.Binary(value.Span);
    }
    public static string Format(SqlType type, BinarySqlValue value) => Require(type).Format(value.Value);
    public static bool Validate(SqlType type, ReadOnlyMemory<byte> value) => Require(type).Validate(value);
    public static int Compare(SqlType type, ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) => Require(type).Compare(left, right);

    internal static bool TryValidate(SqlType type, ReadOnlyMemory<byte> value, out bool valid)
    {
        if (type.UserTypeKind == SqlUserTypeKind.Clr && Bindings.TryGetValue((type.ClrAssemblyName!, type.ClrClassName!), out var runtime))
        { valid = runtime.Validate(value); return true; }
        valid = false; return false;
    }

    private static ISqlClrTypeRuntime Require(SqlType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.UserTypeKind != SqlUserTypeKind.Clr) throw new ArgumentException("The type is not a CLR UDT.", nameof(type));
        return Bindings.TryGetValue((type.ClrAssemblyName!, type.ClrClassName!), out var runtime) ? runtime :
            throw new InvalidOperationException($"No trusted CLR runtime is registered for {type.QualifiedUserTypeName}.");
    }

    private sealed class ReflectionRuntime : ISqlClrTypeRuntime
    {
        private readonly Type _type; private readonly MethodInfo _parse; private readonly MethodInfo _write;
        private readonly MethodInfo _read; private readonly MethodInfo? _validate;
        public ReflectionRuntime(Type type, string? validationMethod)
        {
            _type = type;
            _parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]) ??
                throw new ArgumentException($"CLR UDT '{type.FullName}' has no public static Parse(string).");
            _write = type.GetMethod("Write", BindingFlags.Public | BindingFlags.Instance, [typeof(BinaryWriter)]) ??
                throw new ArgumentException($"CLR UDT '{type.FullName}' has no public Write(BinaryWriter).");
            _read = type.GetMethod("Read", BindingFlags.Public | BindingFlags.Instance, [typeof(BinaryReader)]) ??
                throw new ArgumentException($"CLR UDT '{type.FullName}' has no public Read(BinaryReader).");
            _validate = validationMethod is null ? null : type.GetMethod(validationMethod, BindingFlags.Public | BindingFlags.Instance,
                Type.EmptyTypes) ?? throw new ArgumentException($"CLR UDT validation method '{validationMethod}' was not found.");
            if (_validate is not null && _validate.ReturnType != typeof(bool))
                throw new ArgumentException("CLR UDT validation methods must return bool.");
        }
        public ReadOnlyMemory<byte> Parse(string text) => Serialize(_parse.Invoke(null, [text]) ?? throw new FormatException("CLR Parse returned null."));
        public string Format(ReadOnlyMemory<byte> serializedValue) => Deserialize(serializedValue).ToString() ?? string.Empty;
        public bool Validate(ReadOnlyMemory<byte> serializedValue)
        { var instance = Deserialize(serializedValue); return _validate is null || (bool)_validate.Invoke(instance, null)!; }
        public int Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
        {
            var a = Deserialize(left); var b = Deserialize(right);
            return a is IComparable comparable ? comparable.CompareTo(b) :
                throw new InvalidOperationException($"CLR UDT '{_type.FullName}' does not implement IComparable.");
        }
        private byte[] Serialize(object instance)
        { using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) _write.Invoke(instance, [writer]); return stream.ToArray(); }
        private object Deserialize(ReadOnlyMemory<byte> value)
        {
            var instance = Activator.CreateInstance(_type) ?? throw new InvalidOperationException($"CLR UDT '{_type.FullName}' requires a public parameterless constructor.");
            using var stream = new MemoryStream(value.ToArray(), writable: false); using var reader = new BinaryReader(stream);
            _read.Invoke(instance, [reader]); if (stream.Position != stream.Length) throw new FormatException("CLR UDT did not consume its serialized payload."); return instance;
        }
    }
}
