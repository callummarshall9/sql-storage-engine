// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityEventFiles;
internal interface ISecurityEventFileService
{
    bool ExistsSecurityEventFile(SecurityEventFile securityEventFile);

    long ReadLengthSecurityEventFile(SecurityEventFile securityEventFile);

    byte[] ReadBytesSecurityEventFile(SecurityEventFile securityEventFile);

    void FlushSecurityEventFile(SecurityEventFile securityEventFile);

    void WriteSecurityEventFile(SecurityEventFile securityEventFile);

    void PublishSecurityEventFile(SecurityEventFile securityEventFile);

    void DeleteSecurityEventFile(SecurityEventFile deletedSecurityEventFile);

    byte[] EncodeSecurityEventFile(SecurityEventFile securityEventFile);

    SecurityEventFile DecodeSecurityEventFile(SecurityEventFile securityEventFile);
}