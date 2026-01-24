using System;
using Repository.Utils;

namespace Service.Helpers
{
    internal static class StoryViewTimeHelper
    {
        internal static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7); // UTC+7
        //định nghĩa giờ vn 
        internal static DateTime GetCurrentWeekStartUtc()
        {
            return GetWeekStartUtc(TimezoneConverter.VietnamNow - LocalOffset);
        }
        //lấy thời điểm bắt đầu tuần hiện tại (lấy từ monday)
        internal static DateTime GetWeekStartUtc(DateTime utcNow)
        {
            var local = utcNow + LocalOffset;
            var daysToMonday = ((int)local.DayOfWeek + 6) % 7;
            var mondayLocal = local.Date.AddDays(-daysToMonday);
            var mondayUtc = mondayLocal - LocalOffset;
            return DateTime.SpecifyKind(mondayUtc, DateTimeKind.Utc);
        }
        // làm tròn thời gian tới phút
        internal static DateTime NormalizeToMinuteUtc(DateTime utc)
        {
            return DateTime.SpecifyKind(new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0), DateTimeKind.Utc);
        }
    }
}

