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
                ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            _ = await _profileRepo.GetReaderByIdAsync(accountId, ct)
                ?? throw new AppException("ReaderProfileMissing", "Reader profile was not found.", 404);

            if (await _opRepo.AuthorIsUnrestrictedAsync(accountId, ct))
            {
                throw new AppException("AlreadyAuthor", "Account is already an author.", 409);
            }

            if (await _opRepo.HasPendingAsync(accountId, ct))
            {
                throw new AppException("AlreadyPending", "You already have a pending upgrade request.", 409);
            }

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
                    throw new AppException("Cooldown", $"The previous request was rejected. Please wait 7 days before submitting again. {message}", 429);
                }
            }

            var created = await _opRepo.CreateUpgradeRequestAsync(accountId, req.Commitment, ct);

            return new AuthorUpgradeResponse
            {
                RequestId = created.request_id,
                Status = created.status,
                AssignedOmodId = created.omod_id,
                CreatedAt = created.created_at,
                ReviewedAt = created.reviewed_at,
                ModeratorFeedback = created.omod_note
            };
        }

        public async Task<List<OpRequestItemResponse>> ListMyRequestsAsync(Guid accountId, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

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
                ModeratorFeedback = x.omod_note
            }).ToList();
        }
    }
}
