// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Brokers.SecurityEvents;

namespace sql_storage_engine.Services.Foundations.SecurityEventFiles;
internal sealed partial class SecurityEventFileService(SecurityEventFileBroker securityEventFileBroker) : ISecurityEventFileService
{
    public bool ExistsSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        return securityEventFileBroker.Exists(path: securityEventFile.Path!);
    });

    public long ReadLengthSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        return securityEventFileBroker.ReadLength(path: securityEventFile.Path!);
    });

    public byte[] ReadBytesSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        return securityEventFileBroker.ReadBytes(path: securityEventFile.Path!);
    });

    public void FlushSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        securityEventFileBroker.Flush(path: securityEventFile.Path!);
    });

    public void WriteSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        securityEventFileBroker.Write(path: securityEventFile.Path!, bytes: securityEventFile.Bytes);
    });

    public void PublishSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        securityEventFileBroker.Publish(source: securityEventFile.Path!, destination: securityEventFile.Destination!);
    });

    public void DeleteSecurityEventFile(SecurityEventFile deletedSecurityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFileOnDelete(deletedSecurityEventFile: deletedSecurityEventFile);
        securityEventFileBroker.Delete(path: deletedSecurityEventFile.Path!);
    });

    public byte[] EncodeSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        var payload = securityEventFileBroker.Serialize(value: securityEventFile.Records);

        if (payload.Length > 4 * 1024 * 1024 - 48)
        {
            throw new InvalidOperationException(message: "Security event byte quota exceeded.");
        }

        var bytes = new byte[48 + payload.Length];
        securityEventFile.DatabaseId.TryWriteBytes(destination: bytes.AsSpan(start: 0, length: 16));

        securityEventFileBroker.Hash(bytes: payload)
            .CopyTo(array: bytes, index: 16);

        payload.CopyTo(array: bytes, index: 48);
        return bytes;
    });

    public SecurityEventFile DecodeSecurityEventFile(SecurityEventFile securityEventFile) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventFile(securityEventFile: securityEventFile);
        var bytes = securityEventFile.Bytes;

        if (bytes.Length is < 48 or > 4 * 1024 * 1024)
        {
            throw new StorageFormatException(message: "Invalid security event file length.");
        }

        if (new Guid(b: bytes.Span.Slice(start: 0, length: 16)) != securityEventFile.DatabaseId || !securityEventFileBroker.Compare(left: bytes.Slice(start: 16, length: 32), right: securityEventFileBroker.Hash(bytes: bytes.Slice(start: 48))))
        {
            throw new StorageFormatException(message: "Invalid security event identity or checksum.");
        }

        return securityEventFile with
        {
            Records = securityEventFileBroker.Deserialize<StorageAuditRecord[]>(bytes: bytes.Slice(start: 48)) ?? throw new ArgumentException(message: "Security event records are missing.")
        };
    });

    internal static SecurityEventFileService CreateSecurityEventFileService() =>
        new(securityEventFileBroker: new sql_storage_engine.Brokers.SecurityEvents.SecurityEventFileBroker());
}