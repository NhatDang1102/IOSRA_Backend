using Contract.DTOs.Response.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IBackupService
    {
        Task<BackupCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken ct = default);
        Task<BackupRunResponse> RunAsync(CancellationToken ct = default);
        Task<List<BackupHistoryItemResponse>> GetHistoryAsync(CancellationToken ct = default);
        Task<BackupRunResponse> RestoreAsync(string backupId, CancellationToken ct = default);
    }
}
