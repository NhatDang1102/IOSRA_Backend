using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class PublicProfileRepository : IPublicProfileRepository
    {
        private static readonly string[] PublicStoryStatuses = { "published", "completed" };
        private readonly AppDbContext _db;

        public PublicProfileRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<PublicProfileProjection?> GetPublicProfileAsync(Guid accountId, CancellationToken ct = default)
        {
            return _db.accounts
                .AsNoTracking()
                .Where(a => a.account_id == accountId)
                .Select(a => new PublicProfileProjection
                {
                    AccountId = a.account_id,
                    Username = a.username,
                    Status = a.status,
                    AvatarUrl = a.avatar_url,
                    CreatedAt = a.created_at,
                    Bio = a.reader != null ? a.reader.bio : null,
                    Gender = a.reader != null ? a.reader.gender : null,
                    IsAuthor = a.author != null,
                    AuthorRestricted = a.author != null && a.author.restricted,
                    AuthorVerified = a.author != null && a.author.verified_status,
                    AuthorRankName = a.author != null && a.author.rank != null ? a.author.rank.rank_name : null,
                    FollowerCount = a.author != null
                        ? _db.follows.Count(f => f.followee_id == a.account_id)
                        : 0,
                    PublishedStoryCount = a.author != null
                        ? _db.stories.Count(s => s.author_id == a.account_id && PublicStoryStatuses.Contains(s.status))
                        : 0,
                    LatestPublishedAt = a.author != null
                        ? _db.stories
                            .Where(s => s.author_id == a.account_id && PublicStoryStatuses.Contains(s.status) && s.published_at != null)
                            .Max(s => (DateTime?)s.published_at)
                        : null
                })
                .FirstOrDefaultAsync(ct);
        }
    }
}

