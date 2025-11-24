using System;

namespace Repository.Utils
{
    public static class StatPeriodHelper
    {
        public static DateTime GetPeriodStart(DateTime timestamp, string period)
        {
            var date = timestamp.Date;
            return period switch
            {
                "day" => date,
                "week" => GetWeekStart(date),
                "year" => new DateTime(date.Year, 1, 1),
                _ => new DateTime(date.Year, date.Month, 1)
            };
        }

        public static DateTime GetPeriodEnd(DateTime start, string period)
        {
            return period switch
            {
                "day" => start,
                "week" => start.AddDays(6),
                "year" => new DateTime(start.Year, 12, 31),
                _ => new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month))
            };
        }

        public static string BuildLabel(DateTime start, DateTime end, string period)
        {
            return period switch
            {
                "day" => start.ToString("yyyy-MM-dd"),
                "week" => $"{start:yyyy-MM-dd}~{end:yyyy-MM-dd}",
                "year" => start.Year.ToString(),
                _ => start.ToString("yyyy-MM")
            };
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff);
        }
    }
}
