using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Admin
{
    public class BackupRunResponse
    {
        public string Mode { get; set; } = "mysqldump"; // mysqldump | snapshot
        public string Status { get; set; } = "Success"; // Success | Failed | NotSupported
        public string Message { get; set; } = "";

        public string? BackupId { get; set; }
        public string? FileName { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }

        // snapshot mode info (để FE show nút mở dashboard)
        public string? Provider { get; set; }
        public string? ActionUrl { get; set; }

        // lỗi kỹ thuật (FE show optional)
        public string? Error { get; set; }
    }
}
