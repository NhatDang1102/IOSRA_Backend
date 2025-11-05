using System;

namespace Service.Helpers
{
    internal static class StoryViewTimeHelper
    {
        internal static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7); // UTC+7

        internal static DateTime GetCurrentWeekStartUtc()
        {
            return GetWeekStartUtc(DateTime.UtcNow);
        }

        internal static DateTime GetWeekStartUtc(DateTime utcNow)
        {
            var local = utcNow + LocalOffset;
            var daysToMonday = ((int)local.DayOfWeek + 6) % 7;
            var mondayLocal = local.Date.AddDays(-daysToMonday);
            var mondayUtc = mondayLocal - LocalOffset;
            return DateTime.SpecifyKind(mondayUtc, DateTimeKind.Utc);
        }

        internal static DateTime NormalizeToMinuteUtc(DateTime utc)
        {
            return DateTime.SpecifyKind(new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0), DateTimeKind.Utc);
        }
    }
}

