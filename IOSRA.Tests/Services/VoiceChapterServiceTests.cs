using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Repository.DBContext;
using Repository.Entities;
using Service.Exceptions;
using Service.Interfaces;
using Service.Models;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class VoiceChapterServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly Mock<IChapterContentStorage> _contentStorageMock;
        private readonly Mock<IVoicePricingService> _pricingMock;
        private readonly Mock<IVoiceSynthesisQueue> _queueMock;
        private readonly VoiceChapterService _service;

        private readonly Guid _authorId = Guid.NewGuid();
        private readonly Guid _storyId = Guid.NewGuid();
        private readonly Guid _chapterId = Guid.NewGuid();
        private readonly Guid _voiceId = Guid.NewGuid();

        public VoiceChapterServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(options);

            _contentStorageMock = new Mock<IChapterContentStorage>();
            _pricingMock = new Mock<IVoicePricingService>();
            _queueMock = new Mock<IVoiceSynthesisQueue>();

            _service = new VoiceChapterService(_db, _contentStorageMock.Object, _pricingMock.Object, _queueMock.Object);

            SetupData();
        }

        private void SetupData()
        {
            var rank = new author_rank { rank_id = Guid.NewGuid(), rank_name = "Bronze", min_followers = 0, reward_rate = 50 };
            _db.author_ranks.Add(rank);

            var account = new account 
            { 
                account_id = _authorId, 
                username = "author",
                email = "author@test.com",
                password_hash = "hashed_password",
                status = "unbanned",
                strike_status = "none"
            };
            var author = new author 
            { 
                account_id = _authorId, 
                rank_id = rank.rank_id, 
                revenue_balance = 100 // Author có 100 Dias
            };
            account.author = author;
            _db.accounts.Add(account);
            _db.authors.Add(author);

            var story = new story 
            { 
                story_id = _storyId, 
                author_id = _authorId, 
                title = "Test Story",
                status = "published",
                outline = "Test Story Outline"
            };
            _db.stories.Add(story);

            var chapter = new chapter
            {
                chapter_id = _chapterId,
                story_id = _storyId,
                title = "Chapter 1",
                status = "published",
                content_url = "path/to/content",
                char_count = 2000,
                access_type = "free"
            };
            _db.chapter.Add(chapter);

            var voice = new voice_list 
            { 
                voice_id = _voiceId, 
                voice_name = "Test Voice", 
                is_active = true,
                provider_voice_id = "some_provider_id",
                voice_code = "some_voice_code"
            };
            _db.voice_lists.Add(voice);

            _db.SaveChanges();
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        [Fact]
        public async Task OrderVoicesAsync_Should_Succeed_When_Balance_Is_Sufficient()
        {
            // Arrange
            var req = new VoiceChapterOrderRequest { VoiceIds = new List<Guid> { _voiceId } };
            
            // Mock Content download
            _contentStorageMock.Setup(x => x.DownloadAsync("path/to/content", It.IsAny<CancellationToken>()))
                .ReturnsAsync("Nội dung dài 2000 ký tự..."); // Giả lập content

            // Mock Pricing: Cost = 2 Dias, Sell = 10 Dias
            _pricingMock.Setup(x => x.GetGenerationCostAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            _pricingMock.Setup(x => x.GetPriceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            // Act
            var result = await _service.OrderVoicesAsync(_authorId, _chapterId, req);

            // Assert
            result.Should().NotBeNull();
            result.TotalGenerationCostDias.Should().Be(2);
            result.AuthorRevenueBalanceAfter.Should().Be(98); // 100 - 2 = 98

            // Verify DB changes
            var author = await _db.authors.FindAsync(_authorId);
            author!.revenue_balance.Should().Be(98);

            var trans = await _db.author_revenue_transaction.FirstOrDefaultAsync();
            trans.Should().NotBeNull();
            trans!.type.Should().Be("voice_generation");
            trans.amount.Should().Be(-2);

            var chapterVoice = await _db.chapter_voice.FirstOrDefaultAsync();
            chapterVoice.Should().NotBeNull();
            chapterVoice!.status.Should().Be("pending");

            // Verify Queue
            _queueMock.Verify(x => x.EnqueueAsync(It.IsAny<VoiceSynthesisJob>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OrderVoicesAsync_Should_Fail_When_Balance_Insufficient()
        {
            // Arrange
            // Giả sử giá tạo là 200 Dias (trong khi Author chỉ có 100)
            _contentStorageMock.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("content");
            _pricingMock.Setup(x => x.GetGenerationCostAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(200);

            var req = new VoiceChapterOrderRequest { VoiceIds = new List<Guid> { _voiceId } };

            // Act
            var act = () => _service.OrderVoicesAsync(_authorId, _chapterId, req);

            // Assert
            await act.Should().ThrowAsync<AppException>()
                .WithMessage("*Not enough revenue balance*");
        }

        [Fact]
        public async Task OrderVoicesAsync_Should_Fail_If_Chapter_Not_Published()
        {
            // Arrange
            var draftChapterId = Guid.NewGuid();
            _db.chapter.Add(new chapter 
            { 
                chapter_id = draftChapterId, 
                story_id = _storyId, 
                title = "Draft Chapter", // Thêm title
                status = "draft",
                access_type = "free"
            });
            await _db.SaveChangesAsync();

            var req = new VoiceChapterOrderRequest { VoiceIds = new List<Guid> { _voiceId } };

            // Act
            var act = () => _service.OrderVoicesAsync(_authorId, draftChapterId, req);

            // Assert
            await act.Should().ThrowAsync<AppException>()
                .WithMessage("*Only published chapters*");
        }
    }
}
