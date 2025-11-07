using System;

namespace Repository.Utils
{
    // Tiện ích chuyển đổi múi giờ Việt Nam (GMT+7)
    public static class TimezoneConverter
    {
        // Offset múi giờ Việt Nam so với UTC (+7 giờ)
        public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        // Lấy thời gian hiện tại theo múi giờ Việt Nam (GMT+7)
        public static DateTime Now()
        {
            return DateTime.UtcNow + VietnamOffset;
        }

        // Alias cho dễ đọc
        public static DateTime VietnamNow => Now();
    }
}
