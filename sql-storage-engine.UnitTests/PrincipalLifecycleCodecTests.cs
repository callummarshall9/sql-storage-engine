using sql_storage_engine.Models.Catalog;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Security;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.UnitTests;
public sealed class PrincipalLifecycleCodecTests
{
    [Test]
    public void LegacySecurityPayloadMigratesWithoutInventingLifecycleAuthority()
    {
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([], []);
        var current = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog);
        var old = Encoding.UTF8.GetBytes("{\"Revision\":1,\"Principals\":[],\"Permissions\":[],\"Audit\":[]}");
        var prefix = current[..^(sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.Encode(catalog.Security).Length + 4)];
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length,old.Length);
        var bytes = prefix.Concat(length).Concat(old).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x31544143);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 11);
        var restored = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(bytes).Security;
        restored.DatabasePermissions.Should().BeEmpty();
        restored.RetiredPrincipals.Should().BeEmpty();
        restored.PrincipalDependencies.Should().BeEmpty();
        sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.FormatVersion.Should().Be(13);
    }

    [TestCase("duplicate")]
    [TestCase("live-retired")]
    [TestCase("orphan")]
    [TestCase("invalid-enum")]
    public void CorruptLifecycleCatalogFailsClosed(string mode)
    {
        var id = PrincipalLifecycleTests.Target();
        var state = sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.CreateStorageSecurityState() with
        {
            RetiredPrincipals = [id]
        };
        state = mode switch
        {
            "duplicate" => state with
            {
                RetiredPrincipals = [id, id]
            },
            "live-retired" => state with
            {
                Principals = [new sql_storage_engine.Models.Security.StoragePrincipal
                {
                    Id = id,
                    Active = true
                }

                ]
            },
            "orphan" => state with
            {
                PrincipalDependencies = [new sql_storage_engine.Models.Security.StoragePrincipalReference
                {
                    Principal = id,
                    Kind = StoragePrincipalDependencyKind.Ownership,
                    ReferenceId = Guid.NewGuid()
                }

                ]
            },
            _ => sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.CreateStorageSecurityState() with
            {
                Principals = [new sql_storage_engine.Models.Security.StoragePrincipal
                {
                    Id = id,
                    Active = true
                }

                ],
                DatabasePermissions = [new sql_storage_engine.Models.Security.StorageDatabasePermission
                {
                    Principal = id,
                    Action = (StorageDatabasePermissionAction)99,
                    Effect = StoragePermissionEffect.Grant
                }

                ]
            }
        };
        Action decode = () => sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.Decode(sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.Encode(state));
        decode.Should().Throw<StorageFormatException>();
    }
}