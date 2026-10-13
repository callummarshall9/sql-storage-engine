// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityMutations;
internal sealed partial class SecurityMutationOrchestrationService : ISecurityMutationOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.SecurityCatalogMutations.SecurityCatalogMutationService mutations;
    private readonly sql_storage_engine.Services.Foundations.SecurityAuditRecords.SecurityAuditRecordService records;
    private SecurityMutationOrchestrationService(sql_storage_engine.Services.Foundations.SecurityCatalogMutations.SecurityCatalogMutationService mutations, sql_storage_engine.Services.Foundations.SecurityAuditRecords.SecurityAuditRecordService records)
    {
        this.mutations = mutations;
        this.records = records;
    }

    internal static SecurityMutationOrchestrationService CreateSecurityMutationOrchestrationService() =>
        new(mutations: new sql_storage_engine.Services.Foundations.SecurityCatalogMutations.SecurityCatalogMutationService(), records: sql_storage_engine.Services.Foundations.SecurityAuditRecords.SecurityAuditRecordService.CreateSecurityAuditRecordService());

    public SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);

        if (securitySessionContext.Operation == 7)
        {
            records.ValidateStorageAuditRecord(storageAuditRecord: securitySessionContext.Record!);
            return securitySessionContext;
        }

        var mutation = securitySessionContext.Operation switch
        {
            1 => mutations.PrepareSecurityCatalogMutationPrincipal(securityCatalogMutation: securitySessionContext.Mutation!),
            2 => mutations.PrepareSecurityCatalogMutationPermission(securityCatalogMutation: securitySessionContext.Mutation!),
            3 => mutations.PrepareSecurityCatalogMutationDatabasePermission(securityCatalogMutation: securitySessionContext.Mutation!),
            4 => mutations.PrepareSecurityCatalogMutationPrincipalDependency(securityCatalogMutation: securitySessionContext.Mutation!),
            5 => mutations.PrepareSecurityCatalogMutationPrincipalChange(securityCatalogMutation: securitySessionContext.Mutation!),
            _ => throw new ArgumentException(message: "Unknown security mutation.")};

        records.ValidateStorageAuditRecord(storageAuditRecord: mutation.Record!);

        return securitySessionContext with
        {
            Mutation = mutation
        };
    });
}