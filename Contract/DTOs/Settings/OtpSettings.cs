using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Settings
{
    public class OtpSettings
    {
        public int Length { get; set; } = 6;
        public int TtlMinutes { get; set; } = 5;
        public int MaxSendPerHour { get; set; } = 5;
        public string RedisPrefix { get; set; } = "regotp";
    }
}
