using System;
using System.Diagnostics;

namespace Service.Helpers
{
    public static class SystemUptimeSnapshot
    {
        // Snapshot uptime ở mức process/instance
        // - _startedAtUtc: thời điểm app start (UTC)
        // - _sw: stopwatch đo thời gian đã chạy liên tục
        private static readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private static readonly Stopwatch _sw = Stopwatch.StartNew();

        public static DateTime StartedAtUtc => _startedAtUtc;
        public static long UptimeSeconds => (long)_sw.Elapsed.TotalSeconds;
    }
}
