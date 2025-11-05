using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Respond.Profile;

namespace Service.Interfaces
{
    public interface IPublicProfileService
    {
        Task<PublicProfileResponse> GetAsync(Guid viewerAccountId, Guid targetAccountId, CancellationToken ct = default);
    }
}

