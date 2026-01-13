using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Admin
{
    public class BackupHistoryItemResponse
    {
        public string BackupId { get; set; } = "";
        public string? FileName { get; set; }
        public string Mode { get; set; } = "mysqldump"; // mysqldump | snapshot
        public string Status { get; set; } = ""; // Success | Failed | NotSupported
        public string? Message { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
    }
}
