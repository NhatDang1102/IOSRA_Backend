namespace Service.Constants
{
    public static class ReportStatuses
    {
        public const string Pending = "pending";
        public const string Resolved = "resolved";
        public const string Rejected = "rejected";

        public static readonly string[] Allowed = { Pending, Resolved, Rejected };
    }
}
