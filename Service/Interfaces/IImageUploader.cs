using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IImageUploader
    {
        Task<string> UploadAvatarAsync(IFormFile file, string publicIdPrefix, CancellationToken ct = default);
        Task<string> UploadStoryCoverAsync(Stream imageStream, string fileName, CancellationToken ct = default);
    }
}
