using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Security;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PrincipalLifecycleCodecTests
{
    [Test]
    public void LegacySecurityPayloadMigratesWithoutInventingLifecycleAuthority()
    {
        var catalog = new CatalogDefinition([], []); var current = CatalogCodec.Encode(catalog);
        var old = Encoding.UTF8.GetBytes("{\"Revision\":1,\"Principals\":[],\"Permissions\":[],\"Audit\":[]}");
        var prefix = current[..^(StorageSecurityState.Encode(catalog.Security).Length + 4)];
        byte[] length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, old.Length);
        var bytes = prefix.Concat(length).Concat(old).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x31544143); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 11);
        var restored = CatalogCodec.Decode(bytes).Security;
        restored.DatabasePermissions.Should().BeEmpty(); restored.RetiredPrincipals.Should().BeEmpty(); restored.PrincipalDependencies.Should().BeEmpty();
        CatalogCodec.FormatVersion.Should().Be(12);
    }
    [TestCase("duplicate")]
    [TestCase("live-retired")]
    [TestCase("orphan")]
    [TestCase("invalid-enum")]
    public void CorruptLifecycleCatalogFailsClosed(string mode)
    {
        var id = PrincipalLifecycleTests.Target(); var state = new StorageSecurityState { RetiredPrincipals = [id] };
        state = mode switch { "duplicate" => state with { RetiredPrincipals = [id, id] }, "live-retired" => state with { Principals = [new(id, true)] }, "orphan" => state with { PrincipalDependencies = [new(id, StoragePrincipalDependencyKind.Ownership, Guid.NewGuid())] }, _ => new() { Principals = [new(id, true)], DatabasePermissions = [new(id, (StorageDatabasePermissionAction)99, StoragePermissionEffect.Grant)] } };
        Action decode = () => StorageSecurityState.Decode(StorageSecurityState.Encode(state)); decode.Should().Throw<StorageFormatException>();
    }
}
