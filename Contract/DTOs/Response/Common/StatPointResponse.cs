namespace Contract.DTOs.Response.Common
{
    public class StatPointResponse
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public string? PeriodStart { get; set; }
        public string? PeriodEnd { get; set; }
        public long Value { get; set; }
    }
}
