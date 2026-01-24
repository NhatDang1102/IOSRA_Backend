using System;

namespace Contract.DTOs.Response.Admin
{
    public class UptimeResponse
    {
        // Thời điểm instance/process hiện tại bắt đầu chạy (UTC)
        public DateTime StartedAtUtc { get; set; }

        // Tổng số giây đã chạy liên tục kể từ StartedAtUtc
        public long UptimeSeconds { get; set; }
    }
}
