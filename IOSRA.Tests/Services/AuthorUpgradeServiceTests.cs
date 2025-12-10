using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Implementations;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class AuthorUpgradeServiceTests
    {
        private readonly Mock<IProfileRepository> _profileRepo;
        private readonly Mock<IOpRequestRepository> _opRepo;
        private readonly AuthorUpgradeService _svc;

        public AuthorUpgradeServiceTests()
        {
            _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);
            _opRepo = new Mock<IOpRequestRepository>(MockBehavior.Strict);

            _svc = new AuthorUpgradeService(_profileRepo.Object, _opRepo.Object);
        }

        #region SubmitAsync

        [Fact]
        public async Task SubmitAsync_Should_Throw_When_Account_Not_Found()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "serious" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((account?)null);

            var act = () => _svc.SubmitAsync(accId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("Account was not found.");

            _profileRepo.VerifyAll();
            _opRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SubmitAsync_Should_Throw_When_Reader_Profile_Missing()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "serious" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });
            _profileRepo.Setup(r => r.GetReaderByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((reader?)null);

            var act = () => _svc.SubmitAsync(accId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("Reader profile was not found.");

            _profileRepo.VerifyAll();
            _opRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SubmitAsync_Should_Throw_When_Already_Author()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "serious" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });
            _profileRepo.Setup(r => r.GetReaderByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new reader { account_id = accId });

            _opRepo.Setup(r => r.AuthorIsUnrestrictedAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

            var act = () => _svc.SubmitAsync(accId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("Account is already an author.");

            _profileRepo.VerifyAll();
            _opRepo.VerifyAll();
        }

        [Fact]
        public async Task SubmitAsync_Should_Throw_When_Has_Pending_Request()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "serious" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });
            _profileRepo.Setup(r => r.GetReaderByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new reader { account_id = accId });

            _opRepo.Setup(r => r.AuthorIsUnrestrictedAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
            _opRepo.Setup(r => r.HasPendingAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

            var act = () => _svc.SubmitAsync(accId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("You already have a pending upgrade request.");

            _profileRepo.VerifyAll();
            _opRepo.VerifyAll();
        }

        [Fact]
        public async Task SubmitAsync_Should_Throw_When_Cooldown_Not_Over()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "serious" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });
            _profileRepo.Setup(r => r.GetReaderByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new reader { account_id = accId });

            _opRepo.Setup(r => r.AuthorIsUnrestrictedAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
            _opRepo.Setup(r => r.HasPendingAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

            // Bị reject gần đây -> chưa hết 7 ngày
            var lastRejected = DateTime.UtcNow; // until = now + 7 days > now -> chắc chắn vào cooldown
            _opRepo.Setup(r => r.GetLastRejectedAtAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(lastRejected);

            var act = () => _svc.SubmitAsync(accId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("The previous request was rejected. Please wait 7 days before submitting again*");

            _profileRepo.VerifyAll();
            _opRepo.VerifyAll();
        }

        [Fact]
        public async Task SubmitAsync_Should_Create_Request_When_All_Conditions_Passed()
        {
            var accId = Guid.NewGuid();
            var req = new SubmitAuthorUpgradeRequest { Commitment = "I will follow the rules" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });
            _profileRepo.Setup(r => r.GetReaderByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new reader { account_id = accId });

            _opRepo.Setup(r => r.AuthorIsUnrestrictedAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
            _opRepo.Setup(r => r.HasPendingAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
            // Không có reject, hoặc đã quá 7 ngày
            _opRepo.Setup(r => r.GetLastRejectedAtAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((DateTime?)null);

            var created = new op_request
            {
                request_id = Guid.NewGuid(),
                requester_id = accId,
                status = "pending",
                request_content = req.Commitment,
                created_at = DateTime.UtcNow,
                omod_id = null
            };

            _opRepo.Setup(r => r.CreateUpgradeRequestAsync(accId, req.Commitment, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(created);

            var res = await _svc.SubmitAsync(accId, req, CancellationToken.None);

            res.RequestId.Should().Be(created.request_id);
            res.Status.Should().Be("pending");
            res.AssignedOmodId.Should().BeNull();

            _profileRepo.VerifyAll();
            _opRepo.VerifyAll();
        }

        #endregion

        #region ListMyRequestsAsync

        [Fact]
        public async Task ListMyRequestsAsync_Should_Throw_When_Account_Not_Found()
        {
            var accId = Guid.NewGuid();

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((account?)null);

            var act = () => _svc.ListMyRequestsAsync(accId, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("Account was not found.");

            _profileRepo.VerifyAll();
            _opRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ListMyRequestsAsync_Should_Map_Requests_Correctly()
        {
            var accId = Guid.NewGuid();

            _profileRepo.Setup(r => r.GetAccountByIdAsync(accId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new account { account_id = accId });

            var r1 = new op_request
            {
                request_id = Guid.NewGuid(),
                requester_id = accId,
                status = "pending",
                request_content = "pls upgrade",
                created_at = new DateTime(2025, 11, 1),
                omod_id = null
            };
            var r2 = new op_request
            {
                request_id = Guid.NewGuid(),
                requester_id = accId,
                status = "approved",
                request_content = "second",
                created_at = new DateTime(2025, 11, 2),
                omod_id = Guid.NewGuid()
            };

            _opRepo.Setup(r => r.ListRequestsOfRequesterAsync(accId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<op_request> { r1, r2 });

            var res = await _svc.ListMyRequestsAsync(accId, CancellationToken.None);

            res.Should().HaveCount(2);

            res.Select(x => x.RequestId).Should().BeEquivalentTo(new[] { r1.request_id, r2.request_id });
            res.First(x => x.RequestId == r1.request_id).Status.Should().Be("pending");
            res.First(x => x.RequestId == r2.request_id).AssignedOmodId.Should().Be(r2.omod_id);

            _profileRepo.VerifyAll();
            _opRepo.VerifyAll();
        }

        #endregion
    }
}
