using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class BackupHistoryStore
    {
        private readonly string _historyPath;

        public BackupHistoryStore(string backupDir)
        {
            Directory.CreateDirectory(backupDir);
            _historyPath = Path.Combine(backupDir, "backup_history.json");
        }

        public async Task<List<BackupRecord>> ReadAsync(CancellationToken ct)
        {
            if (!File.Exists(_historyPath)) return new List<BackupRecord>();
            var json = await File.ReadAllTextAsync(_historyPath, ct);
            return JsonSerializer.Deserialize<List<BackupRecord>>(json) ?? new List<BackupRecord>();
        }

        public async Task WriteAsync(List<BackupRecord> items, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyPath, json, ct);
        }
    }

    public record BackupRecord(
        string BackupId,
        string FileName,
        string FilePath,
        string Status,
        string? Error,
        DateTime StartedAtUtc,
        DateTime FinishedAtUtc
    );
}
