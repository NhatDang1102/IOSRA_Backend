using Contract.DTOs.Request.Author;
using Contract.DTOs.Respond.Author;
using Contract.DTOs.Respond.OperationMod;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

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

        public async Task<AuthorUpgradeResponse> SubmitAsync(ulong accountId, SubmitAuthorUpgradeRequest req, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);
            _ = await _profileRepo.GetReaderByIdAsync(accountId, ct)
                ?? throw new AppException("NotFound", "Không tìm thấy hồ sơ reader.", 404);

            if (await _opRepo.AuthorIsUnrestrictedAsync(accountId, ct))
                throw new AppException("AlreadyAuthor", "Tài khoản đã là tác giả.", 409);

            if (await _opRepo.HasPendingAsync(accountId, ct))
                throw new AppException("AlreadyPending", "Bạn đang có yêu cầu chờ duyệt.", 409);

            var lastRejectedAt = await _opRepo.GetLastRejectedAtAsync(accountId, ct);
            if (lastRejectedAt.HasValue)
            {
                var until = lastRejectedAt.Value.Add(Cooldown);
                var now = DateTime.UtcNow;
                if (now < until)
                {
                    var remain = until - now;
                    var msg = remain.TotalHours >= 1
                        ? $"Bạn có thể nộp lại sau {Math.Ceiling(remain.TotalHours)} giờ."
                        : $"Bạn có thể nộp lại sau {Math.Ceiling(remain.TotalMinutes)} phút.";
                    throw new AppException("Cooldown", $"Đơn trước đã bị từ chối. Vui lòng chờ 7 ngày để nộp lại. {msg}", 429);
                }
            }

            var created = await _opRepo.CreateUpgradeRequestAsync(accountId, req.Commitment, ct);

            return new AuthorUpgradeResponse
            {
                RequestId = created.request_id,
                Status = created.status,
                AssignedOmodId = created.omod_id // nullable
            };
        }

        public async Task<List<OpRequestItemResponse>> ListMyRequestsAsync(ulong accountId, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            var data = await _opRepo.ListRequestsOfRequesterAsync(accountId, ct);
            return data.Select(x => new OpRequestItemResponse
            {
                RequestId = x.request_id,
                RequesterId = x.requester_id,  // ⬅️ map mới
                Status = x.status,
                Content = x.request_content,
                CreatedAt = x.created_at,
                AssignedOmodId = x.omod_id
            }).ToList();
        }
    }
}
