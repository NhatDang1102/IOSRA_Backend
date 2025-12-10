using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Interfaces;
using Service.Services;   // StoryHighlightService
using Contract.DTOs.Internal; // StoryViewCount
using Contract.DTOs.Response.Story;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class StoryHighlightServiceTests
    {
        private readonly Mock<IStoryCatalogRepository> _storyRepo;
        private readonly Mock<IChapterCatalogRepository> _chapterRepo;
        private readonly Mock<IStoryViewTracker> _tracker;
        private readonly Mock<IStoryWeeklyViewRepository> _weeklyStore;
        private readonly StoryHighlightService _svc;

        public StoryHighlightServiceTests()
        {
            _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
            _chapterRepo = new Mock<IChapterCatalogRepository>(MockBehavior.Strict);
            _tracker = new Mock<IStoryViewTracker>(MockBehavior.Strict);
            _weeklyStore = new Mock<IStoryWeeklyViewRepository>(MockBehavior.Strict);

            _svc = new StoryHighlightService(_storyRepo.Object, _chapterRepo.Object, _tracker.Object, _weeklyStore.Object);
        }

        private static story MakeStoryFull(Guid id, string title = "t")
        {
            // Tạo author + account để mapper không NRE khi đọc username/avatar
            var authorId = Guid.NewGuid();
            return new story
            {
                story_id = id,
                title = title,
                status = "published",

                author_id = authorId,
                author = new author
                {
                    account_id = authorId,
                    account = new account
                    {
                        account_id = authorId,
                        username = "author01",
                        avatar_url = "a.png",
                        email = "a@ex.com"
                    }
                },

                // các field thường dùng trong list-item mapper
                cover_url = "https://cdn/cover.jpg",
                desc = "short",
                created_at = DateTime.UtcNow.AddDays(-7),
                updated_at = DateTime.UtcNow,

                // rất quan trọng: list rỗng thay vì null để tránh NRE khi mapper .Select(...)
                story_tags = new List<story_tag>(),
                chapters = new List<chapter>(),

            };
        }

        // CASE: Latest – repo trả rỗng -> trả mảng rỗng, không gọi chapterRepo
        [Fact]
        public async Task GetLatestStoriesAsync_Should_Return_Empty_When_No_Stories()
        {
            _storyRepo.Setup(r => r.GetLatestPublishedStoriesAsync(10, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story>());

            var res = await _svc.GetLatestStoriesAsync(limit: 0, CancellationToken.None);

            res.Should().BeEmpty();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyNoOtherCalls();
        }

        // CASE: Latest – dùng default limit khi input <= 0, fetch chapterCounts và map theo danh sách story
        [Fact]
        public async Task GetLatestStoriesAsync_Should_Use_Default_Limit_And_Fetch_ChapterCounts()
        {
            var s1 = MakeStoryFull(Guid.NewGuid(), "A");
            var s2 = MakeStoryFull(Guid.NewGuid(), "B");

            _storyRepo.Setup(r => r.GetLatestPublishedStoriesAsync(10, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s1, s2 });

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s1.story_id, s2.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int>
                        {
                            [s1.story_id] = 5,
                            [s2.story_id] = 3
                        });

            var res = await _svc.GetLatestStoriesAsync(limit: -1, CancellationToken.None);

            res.Should().HaveCount(2);
            // không ép các field cụ thể của mapper, chỉ cần có items tương ứng
            res.Select(x => x.StoryId).Should().BeEquivalentTo(new[] { s1.story_id, s2.story_id });

            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: Latest: truyền limit dương → repo phải nhận đúng limit (không dùng default)
        [Fact]
        public async Task GetLatestStoriesAsync_Should_Pass_Through_Positive_Limit()
        {
            var s = MakeStoryFull(Guid.NewGuid(), "Only");
            _storyRepo.Setup(r => r.GetLatestPublishedStoriesAsync(7, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s });
            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [s.story_id] = 1 });

            var res = await _svc.GetLatestStoriesAsync(7, CancellationToken.None);

            res.Should().HaveCount(1);
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: Latest – thiếu key trong kết quả chapterCounts → không nổ, mapper xử lý thành 0
        [Fact]
        public async Task GetLatestStoriesAsync_Should_Tolerate_Missing_ChapterCount_Key()
        {
            var s1 = MakeStoryFull(Guid.NewGuid(), "A");
            var s2 = MakeStoryFull(Guid.NewGuid(), "B");
            _storyRepo.Setup(r => r.GetLatestPublishedStoriesAsync(10, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s1, s2 });

            // Thiếu key của s2 → kỳ vọng không nổ nếu mapper xử lý thiếu key về 0
            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s1.story_id, s2.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [s1.story_id] = 5 });

            var res = await _svc.GetLatestStoriesAsync(0, CancellationToken.None);

            res.Should().HaveCount(2);
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }


        // Helper tạo danh sách top
        private static List<StoryViewCount> Top(params (Guid id, int views)[] rows) =>
            rows.Select(r => new StoryViewCount { StoryId = r.id, ViewCount = (ulong)r.views }).ToList();

        // CASE: WeeklyTop – tracker có dữ liệu -> không fallback, giữ đúng thứ tự & set WeekStartUtc
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Use_Tracker_And_Preserve_Order()
        {
            var weekStart = new DateTime(2025, 11, 10, 0, 0, 0, DateTimeKind.Utc);
            var s1 = MakeStoryFull(Guid.NewGuid(), "S1");
            var s2 = MakeStoryFull(Guid.NewGuid(), "S2");
            var top = Top((s2.story_id, 20), (s1.story_id, 10)); // s2 đứng trước

            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(weekStart);
            _tracker.Setup(t => t.GetWeeklyTopAsync(weekStart, 5, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(top);

            _weeklyStore.Verify(ws => ws.GetTopWeeklyViewsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s2.story_id, s1.story_id })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s1, s2 }); // thứ tự bất kỳ

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s2.story_id, s1.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [s1.story_id] = 7, [s2.story_id] = 4 });

            var res = await _svc.GetTopWeeklyStoriesAsync(limit: 5, CancellationToken.None);

            res.Should().HaveCount(2);
            // Giữ đúng thứ tự theo "top"
            res[0].Story.StoryId.Should().Be(s2.story_id);
            res[0].WeeklyViewCount.Should().Be(20);
            res[0].WeekStartUtc.Should().Be(weekStart);

            res[1].Story.StoryId.Should().Be(s1.story_id);
            res[1].WeeklyViewCount.Should().Be(10);
            res[1].WeekStartUtc.Should().Be(weekStart);

            _tracker.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
            _weeklyStore.Verify(ws => ws.GetTopWeeklyViewsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // CASE: WeeklyTop – tracker rỗng -> fallback sang kho lưu, sau đó map
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Fallback_To_Stored_When_Tracker_Empty()
        {
            var weekStart = new DateTime(2025, 11, 10, 0, 0, 0, DateTimeKind.Utc);
            var s1 = MakeStoryFull(Guid.NewGuid(), "S1");
            var s2 = MakeStoryFull(Guid.NewGuid(), "S2");
            var stored = Top((s1.story_id, 30), (s2.story_id, 25));

            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(weekStart);
            _tracker.Setup(t => t.GetWeeklyTopAsync(weekStart, 3, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<StoryViewCount>()); // rỗng

            _weeklyStore.Setup(ws => ws.GetTopWeeklyViewsAsync(weekStart, 3, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stored);

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s1.story_id, s2.story_id })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s1, s2 });

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s1.story_id, s2.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [s1.story_id] = 11, [s2.story_id] = 12 });

            var res = await _svc.GetTopWeeklyStoriesAsync(limit: 3, CancellationToken.None);

            res.Should().HaveCount(2);
            res.Select(x => x.Story.StoryId).Should().Contain(new[] { s1.story_id, s2.story_id });
            res.All(x => x.WeekStartUtc == weekStart).Should().BeTrue();

            _tracker.VerifyAll();
            _weeklyStore.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: WeeklyTop – cả tracker & store đều rỗng -> trả rỗng
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Return_Empty_When_No_Data_Anywhere()
        {
            var weekStart = DateTime.UtcNow.Date;
            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(weekStart);
            _tracker.Setup(t => t.GetWeeklyTopAsync(weekStart, 10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<StoryViewCount>());
            _weeklyStore.Setup(ws => ws.GetTopWeeklyViewsAsync(weekStart, 10, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new List<StoryViewCount>());

            var res = await _svc.GetTopWeeklyStoriesAsync(limit: 10, CancellationToken.None);

            res.Should().BeEmpty();

            _tracker.VerifyAll();
            _weeklyStore.VerifyAll();
            _storyRepo.VerifyNoOtherCalls();
            _chapterRepo.VerifyNoOtherCalls();
        }

        // CASE: WeeklyTop – story trong top có id không tồn tại -> item đó bị bỏ qua
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Skip_Unknown_Stories()
        {
            var weekStart = new DateTime(2025, 11, 10, 0, 0, 0, DateTimeKind.Utc);
            var known = MakeStoryFull(Guid.NewGuid(), "Known");
            var missingId = Guid.NewGuid();
            var top = Top((known.story_id, 50), (missingId, 40));

            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(weekStart);
            _tracker.Setup(t => t.GetWeeklyTopAsync(weekStart, 5, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(top);

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { known.story_id, missingId })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { known }); // chỉ trả về story hợp lệ

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { known.story_id, missingId })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [known.story_id] = 9 });

            var res = await _svc.GetTopWeeklyStoriesAsync(5, CancellationToken.None);

            res.Should().HaveCount(1);
            res[0].Story.StoryId.Should().Be(known.story_id);
            res[0].WeeklyViewCount.Should().Be(50);
            res[0].WeekStartUtc.Should().Be(weekStart);

            _tracker.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: WeeklyTop – truyền limit <= 0 → dùng default limit
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Use_Default_Limit_When_NonPositive()
        {
            var start = DateTime.UtcNow.Date;
            var s = MakeStoryFull(Guid.NewGuid(), "X");
            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(start);
            _tracker.Setup(t => t.GetWeeklyTopAsync(start, 10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Top((s.story_id, 1)));

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s.story_id })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s });
            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [s.story_id] = 1 });

            var res = await _svc.GetTopWeeklyStoriesAsync(0, CancellationToken.None);

            res.Should().HaveCount(1);
            _tracker.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: WeeklyTop – repo trả thừa story không nằm trong top → chỉ lấy đúng story trong top
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Ignore_Stories_Not_In_Top_List()
        {
            var start = DateTime.UtcNow.Date;
            var sTop = MakeStoryFull(Guid.NewGuid(), "Top");
            var sExtra = MakeStoryFull(Guid.NewGuid(), "Extra"); // không nằm trong top

            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(start);
            _tracker.Setup(t => t.GetWeeklyTopAsync(start, 5, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Top((sTop.story_id, 9)));

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { sTop.story_id })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { sTop, sExtra }); // repo trả thừa

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { sTop.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int> { [sTop.story_id] = 2 });

            var res = await _svc.GetTopWeeklyStoriesAsync(5, CancellationToken.None);

            res.Should().HaveCount(1);
            res[0].Story.StoryId.Should().Be(sTop.story_id); // không lẫn sExtra
            _tracker.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: WeeklyTop – thiếu key trong kết quả chapterCounts → không nổ, mapper xử lý thành 0
        [Fact]
        public async Task GetTopWeeklyStoriesAsync_Should_Work_When_ChapterCounts_Empty()
        {
            var start = DateTime.UtcNow.Date;
            var s = MakeStoryFull(Guid.NewGuid(), "Top");
            _tracker.Setup(t => t.GetCurrentWeekStartUtc()).Returns(start);
            _tracker.Setup(t => t.GetWeeklyTopAsync(start, 10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<StoryViewCount>());

            _weeklyStore.Setup(ws => ws.GetTopWeeklyViewsAsync(start, 10, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Top((s.story_id, 42)));

            _storyRepo.Setup(r => r.GetStoriesByIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s.story_id })),
                            It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<story> { s });

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { s.story_id })),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Dictionary<Guid, int>()); // rỗng

            var res = await _svc.GetTopWeeklyStoriesAsync(0, CancellationToken.None);

            res.Should().HaveCount(1);
            res[0].WeeklyViewCount.Should().Be(42);
            res[0].Story.StoryId.Should().Be(s.story_id);

            _tracker.VerifyAll();
            _weeklyStore.VerifyAll();
            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }
    }
}
