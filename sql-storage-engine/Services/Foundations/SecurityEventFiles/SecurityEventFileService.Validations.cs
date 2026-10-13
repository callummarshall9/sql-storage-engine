// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityEventFiles;
internal sealed partial class SecurityEventFileService
{
    private static void ValidateSecurityEventFile(SecurityEventFile securityEventFile) =>
        Validate(inputs: [(securityEventFile is null, nameof(securityEventFile)), (string.IsNullOrWhiteSpace(value: securityEventFile?.Path), nameof(SecurityEventFile.Path))]);

    private static void ValidateSecurityEventFileOnDelete(SecurityEventFile deletedSecurityEventFile) =>
        Validate(inputs: [(deletedSecurityEventFile is null, nameof(deletedSecurityEventFile)), (string.IsNullOrWhiteSpace(value: deletedSecurityEventFile?.Path), nameof(SecurityEventFile.Path))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A security event file is required.", paramName: input.ParameterName);
            }
        }
    }
}