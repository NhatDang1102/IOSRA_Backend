namespace Service.Constants
{
    public static class ReportReasonCodes
    {
        public const string NegativeContent = "negative_content";
        public const string Misinformation = "misinformation";
        public const string Spam = "spam";
        public const string IntellectualProperty = "ip_infringement";

        public static readonly string[] Allowed = { NegativeContent, Misinformation, Spam, IntellectualProperty };
    }
}
