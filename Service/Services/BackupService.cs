using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class BackupService : IBackupService
    {
        private readonly MySqlBackupSettings _opt;
        private readonly BackupHistoryStore _store;

        public BackupService(IOptions<MySqlBackupSettings> opt)
        {
            _opt = opt.Value;
            _store = new BackupHistoryStore(_opt.BackupDir);
        }

        public async Task<object> RunAsync(CancellationToken ct = default)
        {
            Directory.CreateDirectory(_opt.BackupDir);

            var backupId = Guid.NewGuid().ToString("N");
            var fileName = $"mysql_{_opt.Database}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{backupId}.sql";
            var filePath = Path.Combine(_opt.BackupDir, fileName);

            var startedAt = DateTime.UtcNow;

            // mysqldump args (không đưa password vào args để tránh lộ)
            var args =
                $"--host={_opt.Host} --port={_opt.Port} --user={_opt.User} " +
                "--single-transaction --routines --events --triggers " +
                "--set-gtid-purged=OFF " +
                $"{_opt.Database}";

            var psi = new ProcessStartInfo
            {
                FileName = "mysqldump",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.Environment["MYSQL_PWD"] = _opt.Password;

            using var p = Process.Start(psi)!;
            var dump = await p.StandardOutput.ReadToEndAsync();
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            var finishedAt = DateTime.UtcNow;

            var history = await _store.ReadAsync(ct);

            if (p.ExitCode != 0)
            {
                history.Insert(0, new BackupRecord(backupId, fileName, filePath, "Failed", err, startedAt, finishedAt));
                await _store.WriteAsync(history, ct);
                return new { backupId, fileName, status = "Failed", error = err };
            }

            await File.WriteAllTextAsync(filePath, dump, ct);

            history.Insert(0, new BackupRecord(backupId, fileName, filePath, "Success", null, startedAt, finishedAt));
            await _store.WriteAsync(history, ct);

            return new { backupId, fileName, status = "Success", startedAtUtc = startedAt, finishedAtUtc = finishedAt };
        }

        public async Task<List<object>> GetHistoryAsync(CancellationToken ct = default)
        {
            var history = await _store.ReadAsync(ct);
            return history.Select(h => (object)new
            {
                h.BackupId,
                h.FileName,
                h.Status,
                h.Error,
                h.StartedAtUtc,
                h.FinishedAtUtc
            }).ToList();
        }

        public async Task<object> RestoreAsync(string backupId, CancellationToken ct = default)
        {
            var history = await _store.ReadAsync(ct);
            var rec = history.FirstOrDefault(x => x.BackupId == backupId);

            if (rec is null || !File.Exists(rec.FilePath))
                return new { backupId, status = "Failed", error = "Backup file not found." };

            var args = $"--host={_opt.Host} --port={_opt.Port} --user={_opt.User} {_opt.Database}";

            var psi = new ProcessStartInfo
            {
                FileName = "mysql",
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.Environment["MYSQL_PWD"] = _opt.Password;

            using var p = Process.Start(psi)!;

            var sql = await File.ReadAllTextAsync(rec.FilePath, ct);
            await p.StandardInput.WriteAsync(sql);
            p.StandardInput.Close();

            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            if (p.ExitCode != 0)
                return new { backupId, status = "Failed", error = err };

            return new { backupId, status = "Restored" };
        }
    }
}
