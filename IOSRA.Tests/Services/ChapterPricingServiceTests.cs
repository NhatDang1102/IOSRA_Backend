using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services; // ChapterPricingService
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class ChapterPricingServiceTests
    {
        private readonly Mock<IChapterPricingRepository> _repo;
        private readonly IMemoryCache _cache;
        private readonly ChapterPricingService _svc;

        public ChapterPricingServiceTests()
        {
            _repo = new Mock<IChapterPricingRepository>(MockBehavior.Strict);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _svc = new ChapterPricingService(_repo.Object, _cache);
        }

        private static chapter_price_rule Rule(uint min, uint? max, uint price) =>
            new chapter_price_rule
            {
                min_char_count = min,
                max_char_count = max,
                dias_price = price
            };

        // CASE: chọn đúng rule theo biên (inclusive)
        [Fact]
        public async Task GetPriceAsync_Should_Select_Correct_Range_Inclusive_Bounds()
        {
            var rules = new List<chapter_price_rule>
        {
            Rule(1, 1000, 2),
            Rule(1001, 2000, 3),
            Rule(2001, null, 5)
        };
            _repo.Setup(r => r.GetRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

            var p1 = await _svc.GetPriceAsync(1000, CancellationToken.None); // biên trên của rule 1
            var p2 = await _svc.GetPriceAsync(1001, CancellationToken.None); // biên dưới của rule 2

            p1.Should().Be(2);
            p2.Should().Be(3);

            _repo.Verify(r => r.GetRulesAsync(It.IsAny<CancellationToken>()), Times.Once); // lần 2 lấy từ cache
            _repo.VerifyAll();
        }

        // CASE: chọn rule open-ended (max = null)
        [Fact]
        public async Task GetPriceAsync_Should_Select_OpenEnded_Rule()
        {
            var rules = new List<chapter_price_rule>
        {
            Rule(1, 1000, 2),
            Rule(2001, null, 5)
        };
            _repo.Setup(r => r.GetRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

            var price = await _svc.GetPriceAsync(8000, CancellationToken.None);

            price.Should().Be(5);
            _repo.VerifyAll();
        }

        // CASE: không khớp khoảng nào -> fallback tier có min lớn nhất
        [Fact]
        public async Task GetPriceAsync_Should_Fallback_To_Highest_Min_Tier_When_No_Range_Matches()
        {
            var rules = new List<chapter_price_rule>
        {
            Rule(100, 200, 2),
            Rule(300, 400, 4)
        };
            _repo.Setup(r => r.GetRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

            var price = await _svc.GetPriceAsync(10_000, CancellationToken.None);

            price.Should().Be(4); // tier có min=300 (cao nhất)
            _repo.VerifyAll();
        }

        // CASE: wordCount <= 0 -> 400
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetPriceAsync_Should_Throw_When_WordCount_Invalid(int wordCount)
        {
            var act = () => _svc.GetPriceAsync(wordCount, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Word count must be positive*");

            _repo.VerifyNoOtherCalls();
        }

        // CASE: không có rule nào -> 500
        [Fact]
        public async Task GetPriceAsync_Should_Throw_When_No_Rules_Configured()
        {
            _repo.Setup(r => r.GetRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<chapter_price_rule>());

            var act = () => _svc.GetPriceAsync(500, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*pricing rules are not configured*");

            _repo.VerifyAll();
        }

        // CASE: cache – rules non-empty -> lần 2 không gọi repo
        [Fact]
        public async Task GetPriceAsync_Should_Use_Cache_When_Rules_Available()
        {
            var rules = new List<chapter_price_rule> { Rule(1, null, 9) };
            _repo.Setup(r => r.GetRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

            var p1 = await _svc.GetPriceAsync(50, CancellationToken.None);
            var p2 = await _svc.GetPriceAsync(60, CancellationToken.None);

            p1.Should().Be(9);
            p2.Should().Be(9);

            _repo.Verify(r => r.GetRulesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _repo.VerifyAll();
        }

        // CASE: repo trả rỗng -> không cache; lần 2 vẫn gọi repo
        [Fact]
        public async Task GetPriceAsync_Should_Not_Cache_When_Rules_Empty()
        {
            _repo.SetupSequence(r => r.GetRulesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<chapter_price_rule>())
                 .ReturnsAsync(new List<chapter_price_rule>());

            // Lần 1
            var first = () => _svc.GetPriceAsync(100, CancellationToken.None);
            await first.Should().ThrowAsync<AppException>().WithMessage("*pricing rules*");

            // Lần 2: vẫn hit repo do không cache
            var second = () => _svc.GetPriceAsync(200, CancellationToken.None);
            await second.Should().ThrowAsync<AppException>().WithMessage("*pricing rules*");

            _repo.Verify(r => r.GetRulesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            _repo.VerifyAll();
        }
    }
}
