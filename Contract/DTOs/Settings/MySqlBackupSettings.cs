using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Settings
{
    public class MySqlBackupSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Database { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string BackupDir { get; set; } = "Backups";
    }
}
