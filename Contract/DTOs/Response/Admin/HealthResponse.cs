using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Response.Admin
{
    public class HealthResponse
    {
        public string Status { get; set; } = "Healthy"; // Healthy | Degraded
        public DateTime CheckedAtUtc { get; set; }

        public Dictionary<string, bool> Components { get; set; } = new();
    }
}
