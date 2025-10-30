namespace Contract.DTOs.Settings
{
    public class OpenAiSettings
    {
        public string ApiKey { get; set; } = null!;
        public string ModerationModel { get; set; } = "omni-moderation-latest";
        public string ChatModel { get; set; } = "gpt-4.1-mini";
        public string ImageModel { get; set; } = "gpt-image-1";
        public string? BaseUrl { get; set; }
    }
}
