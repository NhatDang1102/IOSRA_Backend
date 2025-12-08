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
    public class ChapterPricingService : IChapterPricingService
    {
        private readonly IChapterPricingRepository _repository;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "chapter_pricing_rules";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public ChapterPricingService(IChapterPricingRepository repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        //lấy giá trong db (để ko hard code)
        public async Task<int> GetPriceAsync(int wordCount, CancellationToken ct = default)
        {
            if (wordCount <= 0)
            {
                throw new AppException("InvalidWordCount", "Word count must be positive.", 400);
            }

            var rules = await GetRulesAsync(ct);
            //tìm tier gá phù hợp từ min và max word count 
            var rule = rules
                .FirstOrDefault(r =>
                    wordCount >= r.min_word_count &&
                    (!r.max_word_count.HasValue || wordCount <= r.max_word_count.Value));

            if (rule == null)
            { //ko có cái nào thỏa mãn yêu cầu thì lấy cái cao nhất 
                // fallback to highest tier
                rule = rules.OrderByDescending(r => r.min_word_count).FirstOrDefault();
            }

            if (rule == null)
            {
                throw new AppException("PricingRulesMissing", "Chapter pricing rules are not configured.", 500);
            }

            return (int)rule.dias_price;
        }

        private async Task<IReadOnlyList<chapter_price_rule>> GetRulesAsync(CancellationToken ct)
        {
            //check imemorycache coi có pricing trong đó ko để khỏi truy ván lại cho nhẹ 
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<chapter_price_rule>? cached) && cached != null && cached.Count > 0)
            {
                return cached;
            }

            var rules = await _repository.GetRulesAsync(ct);
            if (rules.Count > 0)
            {
                //lưu vô cache 
                _cache.Set(CacheKey, rules, CacheDuration);
            }

            return rules;
        }
    }
}
