using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IBackupService
    {
        Task<object> RunAsync(CancellationToken ct = default);
        Task<List<object>> GetHistoryAsync(CancellationToken ct = default);
        Task<object> RestoreAsync(string backupId, CancellationToken ct = default);
    }
}
