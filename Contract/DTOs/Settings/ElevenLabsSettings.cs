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
        public string MusicOutputFormat { get; set; } = "mp3_44100_128";
        public int MusicLengthMs { get; set; } = 30000;
        public bool ForceInstrumental { get; set; } = true;
    }
}
