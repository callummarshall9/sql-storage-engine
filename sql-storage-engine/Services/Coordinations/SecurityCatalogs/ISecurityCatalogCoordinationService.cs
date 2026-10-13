// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Coordinations.SecurityCatalogs;
internal interface ISecurityCatalogCoordinationService
{
    SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext);
}