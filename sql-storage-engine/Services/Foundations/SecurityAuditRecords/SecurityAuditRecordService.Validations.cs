// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityAuditRecords;
internal sealed partial class SecurityAuditRecordService
{
    private static void ValidateStorageAuditRecordInput(StorageAuditRecord storageAuditRecord) =>
        Validate(inputs: [(storageAuditRecord is null, nameof(storageAuditRecord))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}