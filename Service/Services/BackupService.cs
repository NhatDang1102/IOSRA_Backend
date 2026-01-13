using Contract.DTOs.Response.Admin;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Helpers;
using Service.Interfaces;
using System.Diagnostics;

public class BackupService : IBackupService
{
    private readonly MySqlBackupSettings _mysql;
    private readonly BackupOptions _backupOpt;

    public BackupService(IOptions<MySqlBackupSettings> mysql, IOptions<BackupOptions> backupOpt)
    {
        _mysql = mysql.Value;
        _backupOpt = backupOpt.Value;
    }

    public Task<BackupCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        if (string.Equals(_backupOpt.Mode, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new BackupCapabilitiesResponse
            {
                Mode = "snapshot",
                CanDump = false,
                CanRestore = false,
                Provider = _backupOpt.Provider,
                ActionUrl = _backupOpt.ActionUrl
            });
        }

        var canDump = CommandHelper.CommandExists("mysqldump");
        var canRestore = CommandHelper.CommandExists("mysql");

        return Task.FromResult(new BackupCapabilitiesResponse
        {
            Mode = "mysqldump",
            CanDump = canDump,
            CanRestore = canRestore
        });
    }

    public async Task<BackupRunResponse> RunAsync(CancellationToken ct = default)
    {
        // Snapshot mode: vẫn trả JSON cho FE hiển thị
        if (string.Equals(_backupOpt.Mode, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return new BackupRunResponse
            {
                Mode = "snapshot",
                Status = "NotSupported",
                Message = "Môi trường này sử dụng managed DB snapshot. Vui lòng tạo snapshot trên dashboard provider.",
                Provider = _backupOpt.Provider,
                ActionUrl = _backupOpt.ActionUrl
            };
        }

        // mysqldump mode: kiểm tra tool trước, tránh 500
        if (!CommandHelper.CommandExists("mysqldump"))
        {
            return new BackupRunResponse
            {
                Mode = "mysqldump",
                Status = "NotSupported",
                Message = "Server không có mysqldump. Không thể chạy backup dạng SQL dump ở môi trường hiện tại."
            };
        }

        Directory.CreateDirectory(_mysql.BackupDir);

        var backupId = Guid.NewGuid().ToString("N");
        var fileName = $"mysql_{_mysql.Database}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{backupId}.sql";
        var filePath = Path.Combine(_mysql.BackupDir, fileName);

        var startedAt = DateTime.UtcNow;

        var args =
            $"--host={_mysql.Host} --port={_mysql.Port} --user={_mysql.User} " +
            "--single-transaction --routines --events --triggers " +
            "--set-gtid-purged=OFF " +
            $"{_mysql.Database}";

        var psi = new ProcessStartInfo
        {
            FileName = "mysqldump",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["MYSQL_PWD"] = _mysql.Password;

        try
        {
            using var p = Process.Start(psi)!;
            var dump = await p.StandardOutput.ReadToEndAsync();
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            var finishedAt = DateTime.UtcNow;

            if (p.ExitCode != 0)
            {
                return new BackupRunResponse
                {
                    Mode = "mysqldump",
                    Status = "Failed",
                    Message = "Backup thất bại khi chạy mysqldump.",
                    Error = err,
                    BackupId = backupId,
                    FileName = fileName,
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = finishedAt
                };
            }

            await File.WriteAllTextAsync(filePath, dump, ct);

            return new BackupRunResponse
            {
                Mode = "mysqldump",
                Status = "Success",
                Message = "Backup thành công.",
                BackupId = backupId,
                FileName = fileName,
                StartedAtUtc = startedAt,
                FinishedAtUtc = finishedAt
            };
        }
        catch (Exception ex)
        {
            return new BackupRunResponse
            {
                Mode = "mysqldump",
                Status = "Failed",
                Message = "Backup gặp lỗi runtime.",
                Error = ex.Message
            };
        }
    }

    public Task<List<BackupHistoryItemResponse>> GetHistoryAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<BackupHistoryItemResponse>());
    }

    public async Task<BackupRunResponse> RestoreAsync(string backupId, CancellationToken ct = default)
    {
        if (string.Equals(_backupOpt.Mode, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return new BackupRunResponse
            {
                Mode = "snapshot",
                Status = "NotSupported",
                Message = "Restore được thực hiện bằng snapshot của provider, không thực hiện qua API."
            };
        }

        if (!CommandHelper.CommandExists("mysql"))
        {
            return new BackupRunResponse
            {
                Mode = "mysqldump",
                Status = "NotSupported",
                Message = "Server không có mysql client. Không thể restore ở môi trường hiện tại."
            };
        }

        return new BackupRunResponse
        {
            Mode = "mysqldump",
            Status = "Failed",
            Message = "Chưa map backupId -> filePath. Cần implement history store để restore thực."
        };
    }
}
