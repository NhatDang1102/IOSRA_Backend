using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ChapterPricingRepository : IChapterPricingRepository
    {
        private readonly AppDbContext _db;

        public ChapterPricingRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<chapter_price_rule>> GetRulesAsync(CancellationToken ct = default)
        {
            return await _db.chapter_price_rules
                .AsNoTracking()
                .OrderBy(r => r.min_char_count)
                .ToListAsync(ct);
        }

        public Task<chapter_price_rule?> GetRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
        {
            return _db.chapter_price_rules.FirstOrDefaultAsync(r => r.rule_id == ruleId, ct);
        }

        public async Task UpdateRuleAsync(chapter_price_rule rule, CancellationToken ct = default)
        {
            _db.chapter_price_rules.Update(rule);
            await _db.SaveChangesAsync(ct);
        }
    }
}
