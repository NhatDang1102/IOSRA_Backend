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
        private static readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private static readonly Stopwatch _sw = Stopwatch.StartNew();

        public static DateTime StartedAtUtc => _startedAtUtc;
        public static long UptimeSeconds => (long)_sw.Elapsed.TotalSeconds;
    }
}
