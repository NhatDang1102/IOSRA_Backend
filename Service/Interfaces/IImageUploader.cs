using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IImageUploader
    {
        Task<string> UploadAvatarAsync(IFormFile file, string publicIdPrefix, CancellationToken ct = default);
    }
}
