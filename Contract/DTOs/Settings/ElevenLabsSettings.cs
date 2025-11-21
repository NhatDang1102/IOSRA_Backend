namespace Contract.DTOs.Settings
{
    public class ElevenLabsSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? ModelId { get; set; }
        public double? Stability { get; set; }
        public double? SimilarityBoost { get; set; }
        public string OutputFormat { get; set; } = "mp3_44100_128";
    }
}
