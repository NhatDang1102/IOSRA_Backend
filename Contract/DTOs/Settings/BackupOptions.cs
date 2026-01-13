using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Settings
{
    public class BackupOptions
    {
        public string Mode { get; set; } = "mysqldump"; // mysqldump | snapshot
        public string? Provider { get; set; } // Render/Railway/Aiven/VPS...
        public string? ActionUrl { get; set; } // link dashboard snapshot
    }
}
