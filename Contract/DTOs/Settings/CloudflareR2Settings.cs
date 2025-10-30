namespace Contract.DTOs.Settings
{
    public class CloudflareR2Settings
    {
        public string AccountId { get; set; } = null!;
        public string Endpoint { get; set; } = null!;
        public string Bucket { get; set; } = null!;
        public string AccessKeyId { get; set; } = null!;
        public string SecretAccessKey { get; set; } = null!;
        public string? PublicBaseUrl { get; set; }
    }
}
