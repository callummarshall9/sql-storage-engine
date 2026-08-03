# Production support matrix

This engine is “production grade” only inside the narrow support envelope below. Anything not explicitly listed is
unsupported until its qualification evidence is added to `qualification-evidence.json` and passes on every release.

## Qualified combination

| OS / runtime | Architecture | Filesystem and device | Required evidence |
|---|---|---|---|
| Linux kernel 6.0+ / .NET 10 | x86-64 | Local ext4, `data=ordered`, barriers enabled, non-removable 512-byte or 4-KiB logical-sector block device | `linux-x64-ext4` |

The database and WAL must reside on locally attached storage that honors flush-to-durable-media and atomic sector
writes. Database pages are 4–64 KiB powers of two and are checksum protected; the engine does not assume a whole
page write is atomic. A verified WAL full-page image is required to repair a torn page.

## Explicit exclusions

Network filesystems and shared mounts—including NFS, SMB/CIFS, clustered filesystems, object-store mounts, and
userspace synchronization folders—are unsupported. Removable USB/SD storage, RAM disks, device write caches that
ignore flushes, copy-on-write snapshots taken without the backup protocol, 32-bit processes, Windows, and macOS are
not currently qualified. Containers are supported only when their host and persistent volume meet the qualified
local ext4 contract.

## Guarantees and operator responsibilities

- Commit succeeds only after its commit WAL record is flushed. A connection loss after that durability point is an
  intentionally ambiguous client response and must be resolved by application identity/idempotency.
- Crash recovery validates database/timeline identity, redoes committed changes, undoes incomplete work, rejects
  mid-log corruption, and truncates only an incomplete final WAL record.
- Offline backup requires clean shutdown. Online backup retains WAL from its start LSN. A backup is supported only
  after manifest/checksum verification and an automated restore plus integrity check.
- Restore always creates a separate destination. Point-in-time restore requires contiguous archived WAL through the
  target LSN and forks a new timeline. Operators must keep at least one independently verified backup and all WAL
  required by their recovery-point objective.
- Format upgrade requires a verified backup, supports only documented adjacent migrations, checkpoints every
  idempotent boundary, and completes only after integrity validation. Downgrade is unsupported; rollback restores
  the pre-upgrade backup.

## Published capacity ceilings

| Resource | Hard ceiling |
|---|---:|
| Buffer frames | 1,000,000 |
| Encoded row | 16 MiB |
| Index key | 65,535 bytes |
| Logical value | 64 MiB |
| Overflow chain | 8,192 pages |
| Transaction duration | 86,400 seconds |
| In-memory undo per transaction | 256 MiB |
| Pins per transaction | 65,536 |
| Pages per bounded scan | 1,000,000 |
| Concurrent transactions | 65,536 |
| WAL record payload | 16 MiB |

Practical deployments should configure substantially lower limits and alert before reaching them. Disk-full,
permission, checksum, recovery-required, deadlock, and resource-exhaustion failures remain explicit storage errors.

## Qualification evidence

The `linux-x64-ext4` evidence entry names the deterministic suites for binary compatibility and fuzz bounds; crash
boundaries; torn/short I/O; backup/restore/PITR; recovery; concurrency; integrity; and restartable upgrade. Release
qualification requires the complete solution command in that evidence file to pass with warnings treated as errors.
