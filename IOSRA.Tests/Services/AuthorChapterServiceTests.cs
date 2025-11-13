using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using Service.Services;
using Xunit;

public class AuthorChapterServiceTests
{
    private readonly Mock<IAuthorChapterRepository> _chapterRepo;
    private readonly Mock<IAuthorStoryRepository> _storyRepo;
    private readonly Mock<IChapterContentStorage> _storage;
    private readonly Mock<IOpenAiModerationService> _modAi;
    private readonly Mock<IFollowerNotificationService> _followers;
    private readonly Mock<IChapterPricingService> _pricing;
    private readonly AuthorChapterService _svc;

    public AuthorChapterServiceTests()
    {
        _chapterRepo = new Mock<IAuthorChapterRepository>(MockBehavior.Strict);
        _storyRepo = new Mock<IAuthorStoryRepository>(MockBehavior.Strict);
        _storage = new Mock<IChapterContentStorage>(MockBehavior.Strict);
        _modAi = new Mock<IOpenAiModerationService>(MockBehavior.Strict);
        _followers = new Mock<IFollowerNotificationService>(MockBehavior.Strict);
        _pricing = new Mock<IChapterPricingService>(MockBehavior.Strict);

        _svc = new AuthorChapterService(
            _chapterRepo.Object,
            _storyRepo.Object,
            _storage.Object,
            _modAi.Object,
            _followers.Object,
            _pricing.Object);
    }

    #region Helpers

    private static author MakeAuthor(Guid accountId, bool restricted = false)
    {
        return new author
        {
            account_id = accountId,
            restricted = restricted,
            total_follower = 0,
            account = new account
            {
                account_id = accountId,
                username = "author01",
                email = "author@test.com",
                avatar_url = "a.png",
                status = "unbanned",
                strike = 0
            }
        };
    }

    private static story MakeStory(Guid storyId, author a, bool published = true, bool isPremium = false)
    {
        return new story
        {
            story_id = storyId,
            author_id = a.account_id,
            author = a,
            title = "Story title",
            is_premium = isPremium,
            status = published ? "published" : "draft",
            created_at = DateTime.UtcNow.AddDays(-1),
            updated_at = DateTime.UtcNow
        };
    }

    private static language_list MakeLanguage(string code = "vi", string name = "Vietnamese")
    {
        return new language_list
        {
            lang_id = Guid.NewGuid(),
            lang_code = code,
            lang_name = name
        };
    }

    private static chapter MakeChapter(Guid chapterId, story s, language_list lang, string status = "draft")
    {
        return new chapter
        {
            chapter_id = chapterId,
            story_id = s.story_id,
            story = s,
            chapter_no = 1,
            language_id = lang.lang_id,
            language = lang,
            title = "Chapter 1",
            summary = null,
            dias_price = 10,
            access_type = "free",
            content_url = "content-key",
            word_count = 100,
            status = status,
            created_at = DateTime.UtcNow.AddDays(-1),
            updated_at = DateTime.UtcNow,
            submitted_at = null,
            published_at = null,
            content_approves = new List<content_approve>()
        };
    }

    private static string LongContent(int words = 60)
    {
        return string.Join(" ", Enumerable.Repeat("word", words));
    }

    #endregion

    // ====================== CREATE ======================

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Author_Not_Found()
    {
        var accId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "vi",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((author?)null);

        var act = () => _svc.CreateAsync(accId, storyId, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Author profile is not registered*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyNoOtherCalls();
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Story_Not_Found()
    {
        var accId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "vi",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(storyId, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((story?)null);

        var act = () => _svc.CreateAsync(accId, storyId, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Story was not found*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyNoOtherCalls();
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Story_Not_Published()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: false);
        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "vi",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        var act = () => _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Chapters can only be created for published stories*");

        _storyRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Title_Empty()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var req = new ChapterCreateRequest
        {
            Title = "   ",
            LanguageCode = "vi",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetLastAuthorChapterRejectedAtAsync(author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateTime?)null);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        var act = () => _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Chapter title must not be empty*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_LanguageCode_Missing()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "   ",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetLastAuthorChapterRejectedAtAsync(author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateTime?)null);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        var act = () => _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Language code must not be empty*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Language_Not_Supported()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "xx",
            Content = LongContent()
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetLastAuthorChapterRejectedAtAsync(author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateTime?)null);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _chapterRepo.Setup(r => r.GetLanguageByCodeAsync("xx", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((language_list?)null);

        var act = () => _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Language 'xx' is not supported*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Content_Too_Short()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var lang = MakeLanguage("vi", "Vietnamese");

        var req = new ChapterCreateRequest
        {
            Title = "Chap 1",
            LanguageCode = "vi",
            Content = "too short" // < 50 chars
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetLastAuthorChapterRejectedAtAsync(author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateTime?)null);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _chapterRepo.Setup(r => r.GetLanguageByCodeAsync("vi", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(lang);

        var act = () => _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*must contain at least 50 characters*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Draft_With_Pricing_And_Content_Upload()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true, isPremium: true);
        var lang = MakeLanguage("vi", "Vietnamese");
        var content = LongContent(80); // đủ dài

        var req = new ChapterCreateRequest
        {
            Title = "  Chapter 1  ",
            LanguageCode = "vi",
            Content = content
        };

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetLastAuthorChapterRejectedAtAsync(author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateTime?)null);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _chapterRepo.Setup(r => r.GetLanguageByCodeAsync("vi", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(lang);

        _pricing.Setup(p => p.GetPriceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(25);

        _chapterRepo.Setup(r => r.GetNextChapterNumberAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(3);

        _storage.Setup(s => s.UploadAsync(story.story_id, It.IsAny<Guid>(), content, It.IsAny<CancellationToken>()))
                .ReturnsAsync("chapter-content-key");

        _chapterRepo.Setup(r => r.AddAsync(It.IsAny<chapter>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((chapter c, CancellationToken _) => c);

        _chapterRepo.Setup(r => r.GetContentApprovalsForChapterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<content_approve>());

        var res = await _svc.CreateAsync(accId, story.story_id, req, CancellationToken.None);

        res.ChapterId.Should().NotBeEmpty();
        res.StoryId.Should().Be(story.story_id);
        res.Title.Should().Be("Chapter 1"); // từ MapChapter (MakeChapter định nghĩa Title), nên ở đây tùy bạn, nhưng quan trọng là không null
        res.LanguageCode.Should().Be("vi");
        res.PriceDias.Should().Be(25);
        res.Status.Should().Be("draft");
        res.AccessType.Should().Be("coin"); // vì story.is_premium = true

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
        _storage.VerifyAll();
        _pricing.VerifyAll();
    }

    // ====================== LIST ======================

    [Fact]
    public async Task ListAsync_Should_Throw_When_Status_Invalid()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        var act = () => _svc.ListAsync(accId, story.story_id, "UNKNOWN", CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Unsupported status*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListAsync_Should_Return_ListItems_For_Story()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var lang = MakeLanguage();
        var ch = MakeChapter(Guid.NewGuid(), story, lang, status: "draft");

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetByStoryAsync(story.story_id, null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<chapter> { ch });

        var res = await _svc.ListAsync(accId, story.story_id, null, CancellationToken.None);

        res.Should().HaveCount(1);
        res[0].ChapterId.Should().Be(ch.chapter_id);
        res[0].LanguageCode.Should().Be(lang.lang_code);

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    // ====================== SUBMIT ======================

    [Fact]
    public async Task SubmitAsync_Should_Throw_When_Author_Not_Found()
    {
        var accId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var req = new ChapterSubmitRequest();

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((author?)null);

        var act = () => _svc.SubmitAsync(accId, chapterId, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Author profile is not registered*");

        _storyRepo.VerifyAll();
    }

    [Fact]
    public async Task SubmitAsync_Should_Reject_When_Ai_Fails()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var lang = MakeLanguage();
        var chapterId = Guid.NewGuid();

        var chapter = MakeChapter(chapterId, story, lang, status: "draft");
        chapter.content_url = "content-key";

        var req = new ChapterSubmitRequest();

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _storage.Setup(s => s.DownloadAsync("content-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync("bad content");

        var violations = new List<ModerationViolation>
        {
            new("badword", 2, new List<string> { "sample 1" })
        };

        var aiResult = new OpenAiModerationResult(
            ShouldReject: true,
            Score: 3.5,
            Violations: violations,
            Content: "bad content",
            SanitizedContent: "sanitized",
            Explanation: "contains disallowed words");

        _modAi.Setup(m => m.ModerateChapterAsync(chapter.title, "bad content", It.IsAny<CancellationToken>()))
              .ReturnsAsync(aiResult);

        _chapterRepo.Setup(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((content_approve?)null);

        _chapterRepo.Setup(r => r.AddContentApproveAsync(It.IsAny<content_approve>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        var act = () => _svc.SubmitAsync(accId, chapterId, req, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Message.Should().Contain("Chapter was rejected by automated moderation");

        chapter.status.Should().Be("rejected");
        chapter.published_at.Should().BeNull();

        _chapterRepo.Verify(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()), Times.Once);
        _chapterRepo.Verify(r => r.AddContentApproveAsync(It.IsAny<content_approve>(), It.IsAny<CancellationToken>()), Times.Once);
        _followers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitAsync_Should_Publish_And_Notify_When_Score_High()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var lang = MakeLanguage();
        var chapterId = Guid.NewGuid();
        var chapter = MakeChapter(chapterId, story, lang, status: "draft");
        chapter.content_url = "content-key";

        var req = new ChapterSubmitRequest();

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _storage.Setup(s => s.DownloadAsync("content-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync("content ok");

        var aiResult = new OpenAiModerationResult(
            ShouldReject: false,
            Score: 8.0,
            Violations: Array.Empty<ModerationViolation>(),
            Content: "content ok",
            SanitizedContent: "content ok",
            Explanation: "safe");

        _modAi.Setup(m => m.ModerateChapterAsync(chapter.title, "content ok", It.IsAny<CancellationToken>()))
              .ReturnsAsync(aiResult);

        _chapterRepo.Setup(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((content_approve?)null);

        _chapterRepo.Setup(r => r.AddContentApproveAsync(It.IsAny<content_approve>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalsForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<content_approve>());

        _followers.Setup(f => f.NotifyChapterPublishedAsync(
                            story.author_id,
                            author.account.username,
                            story.story_id,
                            story.title,
                            chapter.chapter_id,
                            chapter.title,
                            (int)chapter.chapter_no,
                            It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var res = await _svc.SubmitAsync(accId, chapterId, req, CancellationToken.None);

        res.Status.Should().Be("published");
        chapter.status.Should().Be("published");

        _followers.VerifyAll();
        _chapterRepo.Verify(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_Should_Set_Pending_When_Score_Mid()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author, published: true);
        var lang = MakeLanguage();
        var chapterId = Guid.NewGuid();
        var chapter = MakeChapter(chapterId, story, lang, status: "draft");
        chapter.content_url = "content-key";

        var req = new ChapterSubmitRequest();

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        _chapterRepo.Setup(r => r.StoryHasPendingChapterAsync(story.story_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        _storage.Setup(s => s.DownloadAsync("content-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync("content mid");

        var aiResult = new OpenAiModerationResult(
            ShouldReject: false,
            Score: 5.5,
            Violations: Array.Empty<ModerationViolation>(),
            Content: "content mid",
            SanitizedContent: "content mid",
            Explanation: "needs review");

        _modAi.Setup(m => m.ModerateChapterAsync(chapter.title, "content mid", It.IsAny<CancellationToken>()))
              .ReturnsAsync(aiResult);

        _chapterRepo.Setup(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((content_approve?)null);

        _chapterRepo.Setup(r => r.AddContentApproveAsync(It.IsAny<content_approve>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalsForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<content_approve>());

        var res = await _svc.SubmitAsync(accId, chapterId, req, CancellationToken.None);

        res.Status.Should().Be("pending");
        chapter.status.Should().Be("pending");

        _followers.VerifyNoOtherCalls();
        _chapterRepo.Verify(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ====================== WITHDRAW ======================

    [Fact]
    public async Task WithdrawAsync_Should_Throw_When_Not_Rejected()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author);
        var lang = MakeLanguage();
        var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "draft");

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(chapter.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        var act = () => _svc.WithdrawAsync(accId, chapter.chapter_id, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Only rejected chapters can be withdrawn*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    // ====================== UPDATE DRAFT ======================

    [Fact]
    public async Task UpdateDraftAsync_Should_Throw_When_No_Changes()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author);
        var lang = MakeLanguage();
        var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "draft");

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetForAuthorAsync(story.story_id, chapter.chapter_id, author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        var req = new ChapterUpdateRequest(); // mọi field đều null

        var act = () => _svc.UpdateDraftAsync(accId, story.story_id, chapter.chapter_id, req, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*No changes were provided*");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task UpdateDraftAsync_Should_Update_Content_And_Recalculate_Price()
    {
        var accId = Guid.NewGuid();
        var author = MakeAuthor(accId);
        var story = MakeStory(Guid.NewGuid(), author);
        var lang = MakeLanguage();
        var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "draft");
        chapter.content_url = "old-key";

        _storyRepo.Setup(r => r.GetAuthorAsync(accId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(author);

        _storyRepo.Setup(r => r.GetStoryForAuthorAsync(story.story_id, author.account_id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(story);

        _chapterRepo.Setup(r => r.GetForAuthorAsync(story.story_id, chapter.chapter_id, author.account_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(chapter);

        var newContent = LongContent(100);

        _pricing.Setup(p => p.GetPriceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(50);

        _storage.Setup(s => s.UploadAsync(story.story_id, chapter.chapter_id, newContent, It.IsAny<CancellationToken>()))
                .ReturnsAsync("new-key");

        _storage.Setup(s => s.DeleteAsync("old-key", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        _chapterRepo.Setup(r => r.GetContentApprovalsForChapterAsync(chapter.chapter_id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<content_approve>());

        var req = new ChapterUpdateRequest
        {
            Content = newContent
        };

        var res = await _svc.UpdateDraftAsync(accId, story.story_id, chapter.chapter_id, req, CancellationToken.None);

        res.PriceDias.Should().Be(50);
        chapter.dias_price.Should().Be((uint)50);
        chapter.content_url.Should().Be("new-key");

        _storage.VerifyAll();
        _pricing.VerifyAll();
        _chapterRepo.Verify(r => r.UpdateAsync(chapter, It.IsAny<CancellationToken>()), Times.Once);
    }
}
