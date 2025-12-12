using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class VoicePricingService : IVoicePricingService
    {
        private readonly IVoicePricingRepository _repository;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "voice_pricing_rules";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public VoicePricingService(IVoicePricingRepository repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<int> GetPriceAsync(int charCount, CancellationToken ct = default)
        {
            if (charCount <= 0)
            {
                throw new AppException("InvalidCharCount", "Character count must be positive.", 400);
            }

            var rules = await GetRulesAsync(ct);
            var rule = rules.FirstOrDefault(r =>
                charCount >= r.min_char_count &&
                (!r.max_char_count.HasValue || charCount <= r.max_char_count.Value));

            if (rule == null)
            {
                rule = rules.OrderByDescending(r => r.min_char_count).FirstOrDefault();
            }

            if (rule == null)
            {
                throw new AppException("VoicePricingMissing", "Voice pricing rules are not configured.", 500);
            }

            return (int)rule.dias_price;
        }

        public async Task<int> GetGenerationCostAsync(int charCount, CancellationToken ct = default)
        {
            if (charCount <= 0)
            {
                throw new AppException("InvalidCharCount", "Character count must be positive.", 400);
            }

            var rules = await GetRulesAsync(ct);
            var rule = rules.FirstOrDefault(r =>
                charCount >= r.min_char_count &&
                (!r.max_char_count.HasValue || charCount <= r.max_char_count.Value));

            if (rule == null)
            {
                rule = rules.OrderByDescending(r => r.min_char_count).FirstOrDefault();
            }

            if (rule == null)
            {
                throw new AppException("VoicePricingMissing", "Voice pricing rules are not configured.", 500);
            }

            return (int)rule.generation_dias;
        }

        private async Task<IReadOnlyList<voice_price_rule>> GetRulesAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<voice_price_rule>? cached) && cached?.Count > 0)
            {
                return cached;
            }

            var rules = await _repository.GetRulesAsync(ct);
            if (rules.Count > 0)
            {
                _cache.Set(CacheKey, rules, CacheDuration);
            }

            return rules;
        }
    }
}
