using System.Globalization;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class SqlClrRuntimeArchitectureTests
{
    [Test]
    public void TrustedAssemblyBindingSupportsParseFormatValidationAndOrdering()
    {
        var assemblyName = "TrustedReflection";
        var className = typeof(ReflectionByteUdt).FullName!;
        var type = SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","ReflectionByte",assemblyName,className,SqlClrSerializationFormat.Native,validationMethodName: "IsValid");
        var image = File.ReadAllBytes(typeof(ReflectionByteUdt).Assembly.Location);
        var assembly = CatalogAssemblyExposure.CreateCatalogAssembly(assemblyName, image);
        SqlClrRuntimeExposure.RegisterAssembly(assembly,[type]);
        try
        {
            var value = (BinarySqlValue)SqlClrRuntimeExposure.Parse(type,"7");
            SqlValueExposure.ReadBinarySqlValue(value).ToArray().Should().Equal(7);
            SqlClrRuntimeExposure.Format(type,value).Should().Be("7");
            SqlClrRuntimeExposure.Validate(type,new byte[] { 0 }).Should().BeFalse();
            SqlClrRuntimeExposure.Compare(type,new byte[] { 7 },new byte[] { 8 }).Should().BeNegative();
            ((Action)(() => SqlClrRuntimeExposure.Parse(type,"0"))).Should().Throw<FormatException>();
            ((Action)(() => SqlClrRuntimeExposure.Format(type,value with { Content = "\u0100" }))).Should().Throw<ArgumentException>();
        }
        finally
        {
            SqlClrRuntimeExposure.Unregister(assemblyName,className).Should().BeTrue();
        }
        ((Action)(() => SqlClrRuntimeExposure.Parse(type,"7"))).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void MissingProviderAndInvalidAssemblyDeclarationsFailBeforeRegistration()
    {
        ((Action)(() => SqlClrRuntimeExposure.Register("Missing","Missing.Type",null!))).Should().Throw<ArgumentException>();
        var assembly = CatalogAssemblyExposure.CreateCatalogAssembly("Empty");
        ((Action)(() => SqlClrRuntimeExposure.RegisterAssembly(assembly,[]))).Should().Throw<ArgumentException>();
        SqlClrRuntimeExposure.Unregister("Missing","Missing.Type").Should().BeFalse();
    }
}

public sealed class ReflectionByteUdt : IComparable
{
    private byte value;
    public static ReflectionByteUdt Parse(string text) => new() { value = byte.Parse(text,CultureInfo.InvariantCulture) };
    public void Write(BinaryWriter writer) => writer.Write(value);
    public void Read(BinaryReader reader) => value = reader.ReadByte();
    public bool IsValid() => value != 0;
    public int CompareTo(object? other) => value.CompareTo(((ReflectionByteUdt)other!).value);
    public override string ToString() => value.ToString(CultureInfo.InvariantCulture);
}