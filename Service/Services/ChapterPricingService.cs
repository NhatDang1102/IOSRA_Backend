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
        public async Task<int> GetPriceAsync(int charCount, CancellationToken ct = default)
        {
            if (charCount <= 0)
            {
                throw new AppException("InvalidCharCount", "Số lượng ký tự phải là số dương.", 400);
            }

            var rules = await GetRulesAsync(ct);
            //tìm tier gá phù hợp từ min và max char count 
            var rule = rules
                .FirstOrDefault(r =>
                    charCount >= r.min_char_count &&
                    (!r.max_char_count.HasValue || charCount <= r.max_char_count.Value));

            if (rule == null)
            { //ko có cái nào thỏa mãn yêu cầu thì lấy cái cao nhất 
                // fallback to highest tier
                rule = rules.OrderByDescending(r => r.min_char_count).FirstOrDefault();
            }

            if (rule == null)
            {
                throw new AppException("PricingRulesMissing", "Quy tắc định giá chương chưa được định cấu hình.", 500);
            }

            return (int)rule.dias_price;
        }

        public async Task<IReadOnlyList<chapter_price_rule>> GetAllRulesAsync(CancellationToken ct = default)
        {
            return await GetRulesAsync(ct);
        }

        public async Task UpdateRuleAsync(Contract.DTOs.Request.Admin.UpdateChapterPriceRuleRequest request, CancellationToken ct = default)
        {
            var existing = await _repository.GetRuleByIdAsync(request.RuleId, ct);
            if (existing == null) throw new AppException("NotFound", "Không tìm thấy quy tắc", 404);

            existing.dias_price = request.DiasPrice;

            await _repository.UpdateRuleAsync(existing, ct);
            _cache.Remove(CacheKey);
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