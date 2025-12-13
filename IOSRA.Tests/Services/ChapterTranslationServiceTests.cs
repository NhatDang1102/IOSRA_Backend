using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Subscription;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ChapterTranslationServiceTests
    {
        private readonly Mock<IChapterCatalogRepository> _repoMock;
        private readonly Mock<IChapterContentStorage> _storageMock;
        private readonly Mock<IOpenAiTranslationService> _aiMock;
        private readonly Mock<ISubscriptionService> _subMock;
        private readonly ChapterTranslationService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public ChapterTranslationServiceTests()
        {
            _repoMock = new Mock<IChapterCatalogRepository>();
            _storageMock = new Mock<IChapterContentStorage>();
            _aiMock = new Mock<IOpenAiTranslationService>();
            _subMock = new Mock<ISubscriptionService>();
            _service = new ChapterTranslationService(_repoMock.Object, _storageMock.Object, _aiMock.Object, _subMock.Object);
        }

        [Fact]
        public async Task GetAsync_Should_Return_Translation_If_Exists()
        {
            var chapterId = Guid.NewGuid();
            var langCode = "vi";
            var langId = Guid.NewGuid();
            
            var chapter = new chapter 
            { 
                chapter_id = chapterId, 
                access_type = "free", 
                language = new language_list { lang_code = "en" },
                story = new story { }
            };
            
            var loc = new chapter_localization { content_url = "url", word_count = 100 };
            var lang = new language_list { lang_id = langId, lang_code = "vi" };

            _subMock.Setup(x => x.GetStatusAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionStatusResponse { HasActiveSubscription = true, PlanCode = "premium_month" });

            _repoMock.Setup(x => x.GetPublishedChapterByIdAsync(chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chapter);
            
            _repoMock.Setup(x => x.GetLanguageByCodeAsync(langCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(lang);

            _repoMock.Setup(x => x.GetLocalizationAsync(chapterId, langId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(loc);

            var res = await _service.GetAsync(chapterId, langCode, _userId);

            res.Should().NotBeNull();
            res.ContentUrl.Should().Be("url");
            res.Cached.Should().BeTrue();
        }
    }
}