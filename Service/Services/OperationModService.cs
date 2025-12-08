using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;
using Service.Models;

namespace Service.Implementations
{
    public class OperationModService : IOperationModService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IOpRequestRepository _opRepo;
        private readonly INotificationService _notificationService;
        private readonly IAuthorRevenueRepository _authorRevenueRepository;

        public OperationModService(
            IOpRequestRepository opRepo,
            INotificationService notificationService,
            IAuthorRevenueRepository authorRevenueRepository)
        {
            _opRepo = opRepo;
            _notificationService = notificationService;
            _authorRevenueRepository = authorRevenueRepository;
        }

        public async Task<List<OpRequestItemResponse>> ListAsync(string? status, CancellationToken ct = default)
        {
            var data = await _opRepo.ListRequestsAsync(status, ct);
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

        public async Task ApproveAsync(Guid requestId, Guid omodAccountId, CancellationToken ct = default)
        {
            //bóc request ra từ db 
            var request = await _opRepo.GetRequestAsync(requestId, ct)
                          ?? throw new AppException("RequestNotFound", "Upgrade request was not found.", 404);

            //chỉ approve đc pending request 
            if (!string.Equals(request.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be approved.", 400);
            }

            //bóc rank id trong bảng author_rank ra 
            var casualRankId = await _opRepo.GetRankIdByNameAsync("Casual", ct);
            if (casualRankId is null || casualRankId == Guid.Empty)
            {
                throw new AppException("SeedMissing", "Author rank 'Casual' has not been seeded.", 500);
            }

            //bước nghiệp vụ qtr nhất của method này: update từ reader sang author, gán bảng author rank Casual
            await _opRepo.EnsureAuthorUpgradedAsync(request.requester_id, casualRankId.Value, ct);

            //xong author rank thì bóc role author trong bảng role ra 
            var authorRoleId = await _opRepo.GetRoleIdByCodeAsync("author", ct);
            if (authorRoleId is null || authorRoleId == Guid.Empty)
            {
                throw new AppException("SeedMissing", "Role 'author' has not been seeded.", 500);
            }

            //thêm vô acocunt_role
            await _opRepo.AddAccountRoleIfNotExistsAsync(request.requester_id, authorRoleId.Value, ct);
            //approved status
            await _opRepo.SetRequestApprovedAsync(request.request_id, omodAccountId, ct);
            //bắn noti qua cho author
            await _notificationService.CreateAsync(new NotificationCreateModel(
                request.requester_id,
                NotificationTypes.OperationRequest,
                "Yêu cầu trở thành tác giả được chấp thuận",
                "Chúc mừng! Đội vận hành đã phê duyệt yêu cầu trở thành tác giả của bạn.",
                new
                {
                    requestId = request.request_id,
                    status = "approved"
                }), ct);
        }

        public async Task RejectAsync(Guid requestId, Guid omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default)
        {
            var entity = await _opRepo.GetRequestAsync(requestId, ct)
                         ?? throw new AppException("RequestNotFound", "Upgrade request was not found.", 404);

            if (!string.Equals(entity.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending requests can be rejected.", 400);
            }
            //rejected
            await _opRepo.SetRequestRejectedAsync(requestId, omodAccountId, req.Reason, ct);
            //bắn noti qua
            await _notificationService.CreateAsync(new NotificationCreateModel(
                entity.requester_id,
                NotificationTypes.OperationRequest,
                "Yêu cầu trở thành tác giả bị từ chối",
                string.IsNullOrWhiteSpace(req.Reason)
                    ? "Rất tiếc! Yêu cầu trở thành tác giả của bạn đã bị từ chối."
                    : $"Yêu cầu trở thành tác giả của bạn đã bị từ chối: {req.Reason}",
                new
                {
                    requestId = entity.request_id,
                    status = "rejected",
                    reason = req.Reason
                }), ct);
        }

       
        public async Task<IReadOnlyList<AuthorWithdrawRequestResponse>> ListWithdrawRequestsAsync(string? status, CancellationToken ct = default)
        {
            var data = await _opRepo.ListWithdrawRequestsAsync(null, status, ct);
            return data.Select(MapWithdrawResponse).ToList();
        }

        public async Task ApproveWithdrawAsync(Guid requestId, Guid omodAccountId, ApproveWithdrawRequest request, CancellationToken ct = default)
        {
            var entity = await _opRepo.GetWithdrawRequestAsync(requestId, ct)
                         ?? throw new AppException("WithdrawRequestNotFound", "Withdraw request was not found.", 404);

            if (!string.Equals(entity.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending withdraw requests can be approved.", 400);
            }

            if (!entity.withdraw_amount.HasValue)
            {
                throw new AppException("InvalidWithdrawAmount", "Withdraw amount was not specified.", 400);
            }

            var author = await _authorRevenueRepository.GetAuthorAsync(entity.requester_id, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Author profile was not found.", 404);

            
            var amount = (long)entity.withdraw_amount.Value;
            var note = string.IsNullOrWhiteSpace(request?.Note) ? null : request!.Note!.Trim();
            var transactionCode = string.IsNullOrWhiteSpace(request?.TransactionCode) ? null : request!.TransactionCode!.Trim();
            var metadata = entity.request_content;
            var now = TimezoneConverter.VietnamNow;


            //gọi transaction: đảm bảo status và cập nhật số dư phải đều thành công, ko thì rollback hết 
            await using var transaction = await _authorRevenueRepository.BeginTransactionAsync(ct);
            
            //tính toán lại revenue cho author
            author.revenue_pending_vnd -= amount;
            author.revenue_withdrawn_vnd += amount;

            entity.status = "approved";
            entity.omod_id = omodAccountId;
            entity.reviewed_at = now;
            entity.omod_note = note;
            entity.withdraw_code = transactionCode;
            //update db
            await _opRepo.UpdateOpRequestAsync(entity, ct);

            await _authorRevenueRepository.SaveChangesAsync(ct);
            //xong hết thì mới commit hết lên DB (cái ở trên là up op_request, cái này là commit transaction và revenue)
            await transaction.CommitAsync(ct);
            //bắn notification
            await _notificationService.CreateAsync(new NotificationCreateModel(
                entity.requester_id,
                NotificationTypes.OperationRequest,
                "Yêu cầu rút tiền được chấp thuận",
                "Yêu cầu rút tiền của bạn đã được xử lý thành công.",
                new
                {
                    requestId = entity.request_id,
                    status = "approved",
                    amount
                }), ct);
        }

        public async Task RejectWithdrawAsync(Guid requestId, Guid omodAccountId, RejectWithdrawRequest request, CancellationToken ct = default)
        {
            var entity = await _opRepo.GetWithdrawRequestAsync(requestId, ct)
                         ?? throw new AppException("WithdrawRequestNotFound", "Withdraw request was not found.", 404);

            if (!string.Equals(entity.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidState", "Only pending withdraw requests can be rejected.", 400);
            }

            if (!entity.withdraw_amount.HasValue)
            {
                throw new AppException("InvalidWithdrawAmount", "Withdraw amount was not specified.", 400);
            }

            if (string.IsNullOrWhiteSpace(request?.Note))
            {
                throw new AppException("ReasonRequired", "Rejection reason is required.", 400);
            }

            var author = await _authorRevenueRepository.GetAuthorAsync(entity.requester_id, ct)
                         ?? throw new AppException("AuthorProfileMissing", "Author profile was not found.", 404);

            var amount = (long)entity.withdraw_amount.Value;
            var reason = request!.Note!.Trim();
            var metadata = entity.request_content;
            var now = TimezoneConverter.VietnamNow;

            await using var transaction = await _authorRevenueRepository.BeginTransactionAsync(ct);

            author.revenue_pending_vnd -= amount;
            author.revenue_balance_vnd += amount;

            entity.status = "rejected";
            entity.omod_id = omodAccountId;
            entity.reviewed_at = now;
            entity.omod_note = reason;

            await _opRepo.UpdateOpRequestAsync(entity, ct);

            await _authorRevenueRepository.AddTransactionAsync(new author_revenue_transaction
            {
                trans_id = Guid.NewGuid(),
                author_id = author.account_id,
                type = "withdraw_release",
                amount_vnd = amount,
                request_id = entity.request_id,
                metadata = metadata,
                created_at = now
            }, ct);

            await _authorRevenueRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _notificationService.CreateAsync(new NotificationCreateModel(
                entity.requester_id,
                NotificationTypes.OperationRequest,
                "Yêu cầu rút tiền bị từ chối",
                reason,
                new
                {
                    requestId = entity.request_id,
                    status = "rejected",
                    reason
                }), ct);
        }

        private static AuthorWithdrawRequestResponse MapWithdrawResponse(op_request entity)
        {
            var payload = ParseWithdrawPayload(entity);
            var amount = entity.withdraw_amount.HasValue ? (long)entity.withdraw_amount.Value : 0L;
            return new AuthorWithdrawRequestResponse
            {
                RequestId = entity.request_id,
                Amount = amount,
                Status = entity.status,
                BankName = payload.BankName,
                BankAccountNumber = payload.BankAccountNumber,
                AccountHolderName = payload.AccountHolderName,
                Commitment = payload.Commitment,
                ModeratorNote = entity.omod_note,
                ModeratorUsername = entity.omod?.account?.username,
                TransactionCode = entity.withdraw_code,
                CreatedAt = entity.created_at,
                ReviewedAt = entity.reviewed_at
            };
        }

        private static AuthorWithdrawPayload ParseWithdrawPayload(op_request entity)
        {
            if (string.IsNullOrWhiteSpace(entity.request_content))
            {
                return new AuthorWithdrawPayload();
            }

            try
            {
                return JsonSerializer.Deserialize<AuthorWithdrawPayload>(entity.request_content, JsonOptions)
                       ?? new AuthorWithdrawPayload();
            }
            catch
            {
                return new AuthorWithdrawPayload();
            }
        }
    }
}
