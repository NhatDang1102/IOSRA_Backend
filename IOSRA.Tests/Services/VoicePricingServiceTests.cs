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
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class VoicePricingServiceTests
    {
        private readonly Mock<IVoicePricingRepository> _repoMock;
        private readonly IMemoryCache _cache;
        private readonly VoicePricingService _service;

        public VoicePricingServiceTests()
        {
            _repoMock = new Mock<IVoicePricingRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _service = new VoicePricingService(_repoMock.Object, _cache);
        }

        [Fact]
        public async Task GetPriceAsync_Should_Return_Correct_Selling_Price()
        {
            // Arrange
            var rules = new List<voice_price_rule>
            {
                new() { min_char_count = 0, max_char_count = 1000, dias_price = 5, generation_dias = 1 },
                new() { min_char_count = 1001, max_char_count = 5000, dias_price = 10, generation_dias = 2 }
            };
            _repoMock.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(rules);

            // Act
            var price = await _service.GetPriceAsync(2000);

            // Assert
            price.Should().Be(10);
        }

        [Fact]
        public async Task GetGenerationCostAsync_Should_Return_Correct_Generation_Cost()
        {
            // Arrange
            var rules = new List<voice_price_rule>
            {
                new() { min_char_count = 0, max_char_count = 1000, dias_price = 5, generation_dias = 1 },
                new() { min_char_count = 1001, max_char_count = 5000, dias_price = 10, generation_dias = 2 }
            };
            _repoMock.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(rules);

            // Act
            var cost = await _service.GetGenerationCostAsync(2000);

            // Assert
            cost.Should().Be(2);
        }

        [Fact]
        public async Task GetGenerationCostAsync_Should_Fallback_To_Highest_Tier()
        {
            // Arrange
            var rules = new List<voice_price_rule>
            {
                new() { min_char_count = 0, max_char_count = 1000, dias_price = 5, generation_dias = 1 },
                new() { min_char_count = 1001, max_char_count = null, dias_price = 20, generation_dias = 5 }
            };
            _repoMock.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(rules);

            // Act
            var cost = await _service.GetGenerationCostAsync(99999); // Rất lớn

            // Assert
            cost.Should().Be(5);
        }

        [Fact]
        public async Task GetAllRulesAsync_Should_Return_Mapped_DTOs()
        {
            // Arrange
            var rules = new List<voice_price_rule>
            {
                new() { min_char_count = 0, max_char_count = 1000, dias_price = 5, generation_dias = 1 }
            };
            _repoMock.Setup(x => x.GetRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(rules);

            // Act
            var result = await _service.GetAllRulesAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].GenerationCostDias.Should().Be(1);
            result[0].SellingPriceDias.Should().Be(5);
        }
    }
}
