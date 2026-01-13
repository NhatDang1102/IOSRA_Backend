using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Admin
{
    public class BackupCapabilitiesResponse
    {
        public string Mode { get; set; } = "mysqldump"; // mysqldump | snapshot
        public bool CanDump { get; set; }
        public bool CanRestore { get; set; }
        public string? Provider { get; set; }
        public string? ActionUrl { get; set; }
    }
}
