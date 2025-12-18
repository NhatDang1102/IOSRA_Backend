using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Implementations
{
    public class AuthorUpgradeService : IAuthorUpgradeService
    {
        private readonly IProfileRepository _profileRepo;
        private readonly IOpRequestRepository _opRepo;
        private static readonly TimeSpan Cooldown = TimeSpan.FromDays(7);

        public AuthorUpgradeService(IProfileRepository profileRepo, IOpRequestRepository opRepo)
        {
            _profileRepo = profileRepo;
            _opRepo = opRepo;
        }

        public async Task<AuthorUpgradeResponse> SubmitAsync(Guid accountId, SubmitAuthorUpgradeRequest req, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            _ = await _profileRepo.GetReaderByIdAsync(accountId, ct)
                ?? throw new AppException("ReaderProfileMissing", "Không tìm thấy hồ sơ người đọc.", 404);

            if (await _opRepo.AuthorIsUnrestrictedAsync(accountId, ct))
            {
                throw new AppException("AlreadyAuthor", "Tài khoản đã là tác giả.", 409);
            }

            if (await _opRepo.HasPendingAsync(accountId, ct))
            {
                throw new AppException("AlreadyPending", "Bạn đã có một yêu cầu nâng cấp đang chờ xử lý.", 409);
            }
            //check cooldown, 7 ngày mới đc submit 1 lần từ lúc bị reject để chống spam liên tục 
            var lastRejectedAt = await _opRepo.GetLastRejectedAtAsync(accountId, ct);
            if (lastRejectedAt.HasValue)
            {
                var until = lastRejectedAt.Value.Add(Cooldown);
                var now = TimezoneConverter.VietnamNow;
                if (now < until)
                {
                    var remain = until - now;
                    var message = remain.TotalHours >= 1
                        ? $"You can submit again in {Math.Ceiling(remain.TotalHours)} hour(s)."
                        : $"You can submit again in {Math.Ceiling(remain.TotalMinutes)} minute(s).";
                    throw new AppException("Cooldown", $"Yêu cầu trước đó đã bị từ chối. Vui lòng đợi 7 ngày trước khi gửi lại. {message}", 429);
                }
            }

            //vượt qua đc các validation trên thì khởi tạo vô bảng op_request
            var created = await _opRepo.CreateUpgradeRequestAsync(accountId, req.Commitment, ct);

            return new AuthorUpgradeResponse
            {
                RequestId = created.request_id,
                Status = created.status,
                AssignedOmodId = created.omod_id,
                CreatedAt = created.created_at,
                ReviewedAt = created.reviewed_at,
                ModeratorNote = created.omod_note
            };
        }

        public async Task<List<OpRequestItemResponse>> ListMyRequestsAsync(Guid accountId, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            var data = await _opRepo.ListRequestsOfRequesterAsync(accountId, ct);
            return data.Select(x => new OpRequestItemResponse
            {
                RequestId = x.request_id,
                RequesterId = x.requester_id,
                RequesterUsername = x.requester?.username ?? string.Empty,
                RequesterEmail = x.requester?.email ?? string.Empty,
                Status = x.status,
                Content = x.request_content,
                CreatedAt = x.created_at,
                AssignedOmodId = x.omod_id,
                ReviewedAt = x.reviewed_at,
                ModeratorNote = x.omod_note
            }).ToList();
        }

        public async Task<AuthorRankStatusResponse> GetRankStatusAsync(Guid accountId, CancellationToken ct = default)
        {
            //check author profile lấy rank hiện tại 
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            var author = await _opRepo.GetAuthorWithRankAsync(accountId, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Không tìm thấy hồ sơ tác giả.", 404);

            var ranks = await _opRepo.GetAllAuthorRanksAsync(ct);
            if (ranks == null || ranks.Count == 0)
            {
                throw new AppException("RankSeedMissing", "Hạng tác giả chưa được định cấu hình.", 500);
            }
            //get tất cả bậc rank sort từ dưới lên theo min follower
            var ordered = ranks.OrderBy(r => r.min_followers).ToList();
            //lấy rank hiện tại của tác giả
            var currentRank = author.rank_id.HasValue
                ? ordered.FirstOrDefault(r => r.rank_id == author.rank_id.Value)
                : ordered.FirstOrDefault();

            if (currentRank == null)
            {
                currentRank = ordered.First();
            }

            var currentIndex = ordered.FindIndex(r => r.rank_id == currentRank.rank_id);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextRank = currentIndex + 1 < ordered.Count ? ordered[currentIndex + 1] : null;

            return new AuthorRankStatusResponse
            {
                CurrentRankName = currentRank.rank_name,
                CurrentRewardRate = currentRank.reward_rate,
                TotalFollowers = (int)author.total_follower,
                NextRankName = nextRank?.rank_name,
                NextRankRewardRate = nextRank?.reward_rate,
                NextRankMinFollowers = nextRank != null ? (int?)nextRank.min_followers : null
            };
        }
    }
}