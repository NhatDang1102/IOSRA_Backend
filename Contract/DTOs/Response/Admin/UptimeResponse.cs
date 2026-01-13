using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Admin
{
    public class UptimeResponse
    {
        public DateTime StartedAtUtc { get; set; }
        public long UptimeSeconds { get; set; }
    }
}
