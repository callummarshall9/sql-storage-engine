// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Security;
internal sealed record SecurityAdmission
{
    public StorageAuthorizationRequest? Request { get; set; }
    public StorageSecurityState? State { get; set; }
    public IReadOnlyList<CatalogTable>? Tables { get; set; }
    public DatabaseId DatabaseId { get; set; }
    public StorageAuditRecord? Record { get; set; }
    public bool ReadOnly { get; set; }
}