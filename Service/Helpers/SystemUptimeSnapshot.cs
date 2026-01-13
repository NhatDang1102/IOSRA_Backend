using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public static class SystemUptimeSnapshot
    {
        private static readonly DateTime StartedAtUtc = DateTime.UtcNow;
        private static readonly Stopwatch Sw = Stopwatch.StartNew();

        public static object Current => new
        {
            startedAtUtc = StartedAtUtc,
            uptimeSeconds = (long)Sw.Elapsed.TotalSeconds
        };
    }
}
