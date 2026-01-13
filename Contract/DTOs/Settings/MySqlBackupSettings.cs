using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Settings
{
    public class MySqlBackupSettings
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string BackupDir { get; set; } = "Backups";
    }
}
