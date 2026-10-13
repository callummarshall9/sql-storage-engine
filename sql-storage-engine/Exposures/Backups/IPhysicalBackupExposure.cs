// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Exposures.Backups;
public interface IPhysicalBackupExposure
{
    BackupServiceBinding ReadBackupServiceBinding();
}