namespace Service.Constants
{
    public static class ReportTargetTypes
    {
        public const string Story = "story";
        public const string Chapter = "chapter";
        public const string Comment = "comment";

        public static readonly string[] Allowed = { Story, Chapter, Comment };
    }
}
