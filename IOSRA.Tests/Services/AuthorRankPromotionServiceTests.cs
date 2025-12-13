using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class AuthorRankPromotionServiceTests
    {
        private readonly Mock<IOpRequestRepository> _opRepo;
        private readonly Mock<INotificationService> _notiMock;
        private readonly AuthorRankPromotionService _service;
        private readonly Guid _authorId = Guid.NewGuid();

        public AuthorRankPromotionServiceTests()
        {
            _opRepo = new Mock<IOpRequestRepository>();
            _notiMock = new Mock<INotificationService>();
            _service = new AuthorRankPromotionService(_opRepo.Object, _notiMock.Object);
        }

        [Fact]
        public async Task SubmitAsync_Should_Create_Request()
        {
            var req = new RankPromotionSubmitRequest { Commitment = "I commit to write more" };
            var bronzeId = Guid.NewGuid();
            var silverId = Guid.NewGuid();
            
            _opRepo.Setup(x => x.GetAuthorWithRankAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new author { account_id = _authorId, rank_id = bronzeId, rank = new author_rank { rank_name = "Bronze" }, total_follower = 100 });
            
            _opRepo.Setup(x => x.AuthorHasPublishedStoryAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _opRepo.Setup(x => x.HasPendingRankPromotionRequestAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _opRepo.Setup(x => x.GetAllAuthorRanksAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<author_rank> 
                { 
                    new() { rank_id = bronzeId, rank_name = "Bronze", min_followers = 0 },
                    new() { rank_id = silverId, rank_name = "Silver", min_followers = 50 }
                });

            _opRepo.Setup(x => x.CreateRankPromotionRequestAsync(_authorId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new op_request { request_id = Guid.NewGuid(), status = "pending" });

            _opRepo.Setup(x => x.GetRankPromotionRequestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new op_request { request_id = Guid.NewGuid(), status = "pending", requester = new account() });

            var res = await _service.SubmitAsync(_authorId, req);

            res.Status.Should().Be("pending");
        }
    }
}
