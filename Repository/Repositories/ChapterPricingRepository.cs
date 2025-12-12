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
            //lấy giá trong db (as no tracking để ef core ko cần theo dõi vì k có tác động gì thay đỏi data)
            return await _db.chapter_price_rules
                .AsNoTracking()
                .OrderBy(r => r.min_char_count)
                .ToListAsync(ct);
        }
    }
}
