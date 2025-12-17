namespace Contract.DTOs.Response.Common
{
    public class FileExportResponse
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string FileName { get; set; } = "download";
    }
}
