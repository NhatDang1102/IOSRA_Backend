using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Contract.DTOs.Request.Follow;
using Contract.DTOs.Response.Follow;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Internal;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class AuthorFollowServiceTests
    {
        private readonly Mock<IAuthorFollowRepository> _followRepo;
        private readonly Mock<IProfileRepository> _profileRepo;
        private readonly Mock<IAuthorStoryRepository> _authorRepo;
        private readonly Mock<INotificationService> _notify;
        private readonly Mock<IMemoryCache> _cache;
        private readonly AuthorFollowService _svc;

        public AuthorFollowServiceTests()
        {
            _followRepo = new Mock<IAuthorFollowRepository>(MockBehavior.Strict);
            _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);
            _authorRepo = new Mock<IAuthorStoryRepository>(MockBehavior.Strict);
            _notify = new Mock<INotificationService>(MockBehavior.Strict);
            _cache = new Mock<IMemoryCache>(MockBehavior.Strict);

            _svc = new AuthorFollowService(
                _followRepo.Object,
                _profileRepo.Object,
                _authorRepo.Object,
                _notify.Object,
                _cache.Object);
        }

        private static reader MakeReader(Guid id, string username = "reader1")
            => new reader
            {
                account_id = id,
                account = new account
                {
                    account_id = id,
                    username = username
                }
            };

        private static author MakeAuthor(Guid id, bool restricted = false)
            => new author
            {
                account_id = id,
                total_follower = 0,
                restricted = restricted
            };

        // CASE : FollowAsync – không cho follow chính mình
        [Fact]
        public async Task FollowAsync_Should_Throw_When_SelfFollow()
        {
            var id = Guid.NewGuid();
            Func<Task> act = () => _svc.FollowAsync(id, id, new AuthorFollowRequest(), CancellationToken.None);
            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("FollowSelfNotAllowed");
        }


        // CASE : FollowAsync – reader hoặc author không tồn tại
        [Fact]
        public async Task FollowAsync_Should_Throw_When_Reader_NotFound()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((reader?)null);

            Func<Task> act = () => _svc.FollowAsync(readerId, authorId, new AuthorFollowRequest(), CancellationToken.None);

            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("ReaderProfileMissing");

            _profileRepo.VerifyAll();
        }

        [Fact]
        public async Task FollowAsync_Should_Throw_When_Author_NotFound()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var reader = MakeReader(readerId);

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((author?)null);

            Func<Task> act = () => _svc.FollowAsync(readerId, authorId, new AuthorFollowRequest(), CancellationToken.None);

            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("AuthorNotFound");

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
        }

        // CASE : FollowAsync – author bị restricted
        [Fact]
        public async Task FollowAsync_Should_Throw_When_Author_Restricted()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();

            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId, restricted: true);

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);

            Func<Task> act = () => _svc.FollowAsync(readerId, authorId, new AuthorFollowRequest(), CancellationToken.None);

            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("AuthorRestricted");

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
        }

        // CASE : FollowAsync – đã tồn tại follow -> chỉ update notification
        [Fact]
        public async Task FollowAsync_Should_Update_Notification_When_Already_Following()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();

            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId);

            var existing = new follow
            {
                follower_id = readerId,
                followee_id = authorId,
                noti_new_story = false,
                created_at = DateTime.UtcNow
            };

            var updated = new follow
            {
                follower_id = readerId,
                followee_id = authorId,
                noti_new_story = true,
                created_at = existing.created_at
            };

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetAsync(readerId, authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(existing);
            _followRepo.Setup(r => r.UpdateNotificationAsync(existing, true, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(updated);

            var result = await _svc.FollowAsync(readerId, authorId, new AuthorFollowRequest { EnableNotifications = true }, CancellationToken.None);

            result.NotificationsEnabled.Should().BeTrue();
            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
            _notify.VerifyNoOtherCalls();
        }

        // CASE : FollowAsync – follow mới -> tăng follower, gửi noti
        [Fact]
        public async Task FollowAsync_Should_Add_New_Follow_And_Send_Notification()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();

            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId);
            var newFollow = new follow
            {
                follower_id = readerId,
                followee_id = authorId,
                noti_new_story = true,
                created_at = DateTime.UtcNow
            };

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetAsync(readerId, authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((follow?)null);
            _followRepo.Setup(r => r.AddAsync(readerId, authorId, true, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(newFollow);
            _authorRepo.Setup(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            _notify.Setup(n => n.CreateAsync(
                    It.Is<NotificationCreateModel>(m =>
                        m.RecipientId == authorId &&
                        m.Type == NotificationTypes.NewFollower &&
                        m.Message.Contains(reader.account.username)),
                    It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Contract.DTOs.Response.Notification.NotificationResponse());

            _cache.Setup(c => c.Remove(It.IsAny<object>()));

            var result = await _svc.FollowAsync(readerId, authorId, new AuthorFollowRequest { EnableNotifications = true }, CancellationToken.None);

            result.IsFollowing.Should().BeTrue();
            author.total_follower.Should().Be(1u);

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
            _notify.VerifyAll();
            _cache.VerifyAll();
        }

        // CASE : UnfollowAsync – chưa follow -> lỗi 404
        [Fact]
        public async Task UnfollowAsync_Should_Throw_When_Not_Following()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId);

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetAsync(readerId, authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((follow?)null);

            Func<Task> act = () => _svc.UnfollowAsync(readerId, authorId, CancellationToken.None);
            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("FollowNotFound");

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
        }

        // CASE : UnfollowAsync – xóa follow hợp lệ, giảm follower
        [Fact]
        public async Task UnfollowAsync_Should_Remove_And_Decrease_Count()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId);
            author.total_follower = 5;

            var existing = new follow { follower_id = readerId, followee_id = authorId };

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetAsync(readerId, authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(existing);
            _followRepo.Setup(r => r.RemoveAsync(existing, It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            _authorRepo.Setup(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            _cache.Setup(c => c.Remove(It.IsAny<object>()));

            await _svc.UnfollowAsync(readerId, authorId, CancellationToken.None);

            author.total_follower.Should().Be(4u);

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
            _cache.VerifyAll();
        }

        // CASE : UpdateNotificationAsync – chưa follow -> lỗi
        [Fact]
        public async Task UpdateNotificationAsync_Should_Throw_When_Not_Following()
        {
            var readerId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var reader = MakeReader(readerId);
            var author = MakeAuthor(authorId);

            _profileRepo.Setup(p => p.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(reader);
            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetAsync(readerId, authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((follow?)null);

            Func<Task> act = () => _svc.UpdateNotificationAsync(readerId, authorId, false, CancellationToken.None);
            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("FollowNotFound");

            _profileRepo.VerifyAll();
            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
        }

        // CASE : GetFollowersAsync – normalize page/pageSize và map projection
        [Fact]
        public async Task GetFollowersAsync_Should_Normalize_And_Map()
        {
            var authorId = Guid.NewGuid();
            var author = MakeAuthor(authorId);
            var follower = new AuthorFollowerProjection
            {
                FollowerId = Guid.NewGuid(),
                Username = "R1",
                AvatarUrl = "a.png",
                NotificationsEnabled = true,
                FollowedAt = DateTime.UtcNow
            };

            _authorRepo.Setup(a => a.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(author);
            _followRepo.Setup(r => r.GetFollowersAsync(authorId, 1, 20, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((new List<AuthorFollowerProjection> { follower }, 1));

            var res = await _svc.GetFollowersAsync(authorId, 0, -5, CancellationToken.None);

            res.Page.Should().Be(1);
            res.PageSize.Should().Be(20);
            res.Items.Should().ContainSingle(x => x.Username == "R1");

            _authorRepo.VerifyAll();
            _followRepo.VerifyAll();
        }
    }
}
