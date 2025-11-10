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
                .OrderBy(r => r.min_word_count)
                .ToListAsync(ct);
        }
    }
}
