using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;

namespace Repository.Interfaces
{
    public interface IPublicProfileRepository
    {
        Task<PublicProfileProjection?> GetPublicProfileAsync(Guid accountId, CancellationToken ct = default);
    }
}

