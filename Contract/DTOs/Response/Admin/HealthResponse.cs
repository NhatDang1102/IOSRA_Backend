using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.Admin
{
    public class HealthResponse
    {
        // Status tổng quan: Healthy | Degraded
        public string Status { get; set; } = "Healthy";

        // Thời điểm server thực hiện health-check (UTC)
        public DateTime CheckedAtUtc { get; set; }

        // Map các thành phần để FE render danh sách checkmark
        public Dictionary<string, bool> Components { get; set; } = new();
    }
}
