// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Security;
internal sealed record SecurityCatalogMutation
{
    public StorageSecurityState? State { get; set; }
    public IReadOnlyList<CatalogTable>? Tables { get; set; }
    public DatabaseId DatabaseId { get; set; }
    public long ExpectedRevision { get; set; }
    public Guid OperationId { get; set; }
    public bool Remove { get; set; }
    public StoragePrincipal? Principal { get; set; }
    public StoragePermission? Permission { get; set; }
    public StorageDatabasePermission? DatabasePermission { get; set; }
    public StoragePrincipalReference? PrincipalDependency { get; set; }
    public StoragePrincipalChange? PrincipalChange { get; set; }
    public StorageAuditRecord? Record { get; set; }
}