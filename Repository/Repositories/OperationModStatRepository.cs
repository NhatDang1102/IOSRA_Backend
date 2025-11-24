using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Interfaces;
using Repository.Models;
using Repository.Utils;

namespace Repository.Repositories
{
    public class OperationModStatRepository : BaseRepository, IOperationModStatRepository
    {
        public OperationModStatRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<OperationRevenueData> GetRevenueAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var receipts = _db.payment_receipts
                .AsNoTracking()
                .Where(r => r.created_at >= from && r.created_at <= to);

            long dia = await receipts.Where(r => r.type == "dia_topup").SumAsync(r => (long)r.amount_vnd, ct);
            long subscription = await receipts.Where(r => r.type == "subscription").SumAsync(r => (long)r.amount_vnd, ct);
            long voice = await receipts.Where(r => r.type == "voice_topup").SumAsync(r => (long)r.amount_vnd, ct);

            var source = receipts.Select(r => new StatPointSource
            {
                Timestamp = r.created_at,
                Value = (long)r.amount_vnd
            });

            return new OperationRevenueData
            {
                DiaTopup = dia,
                Subscription = subscription,
                VoiceTopup = voice,
                Points = await GroupAsync(source, period, ct)
            };
        }

        public Task<List<StatPointData>> GetRequestStatsAsync(string requestType, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.op_requests
                .AsNoTracking()
                .Where(r => r.request_type == requestType
                            && r.created_at >= from
                            && r.created_at <= to)
                .Select(r => new StatPointSource
                {
                    Timestamp = r.created_at,
                    Value = 1
                });

            return GroupAsync(query, period, ct);
        }

        public Task<List<StatPointData>> GetAuthorRevenueStatsAsync(string metric, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.author_revenue_transactions
                .AsNoTracking()
                .Where(t => t.created_at >= from && t.created_at <= to);

            if (metric == "withdrawn")
            {
                query = query.Where(t => t.type == "withdraw_complete");
            }
            else
            {
                query = query.Where(t => t.type == "purchase");
            }

            var source = query.Select(t => new StatPointSource
            {
                Timestamp = t.created_at,
                Value = metric == "withdrawn" ? -t.amount_vnd : t.amount_vnd
            });

            return GroupAsync(source, period, ct);
        }

        private async Task<List<StatPointData>> GroupAsync(IQueryable<StatPointSource> source, string period, CancellationToken ct)
        {
            var data = await source.ToListAsync(ct);
            return data
                .GroupBy(x => StatPeriodHelper.GetPeriodStart(x.Timestamp, period))
                .Select(g =>
                {
                    var start = g.Key;
                    var end = StatPeriodHelper.GetPeriodEnd(start, period);
                    return new StatPointData
                    {
                        Label = StatPeriodHelper.BuildLabel(start, end, period),
                        RangeStart = start,
                        RangeEnd = end,
                        Value = g.Sum(v => v.Value)
                    };
                })
                .OrderBy(g => g.RangeStart)
                .ToList();
        }

        private sealed class StatPointSource
        {
            public DateTime Timestamp { get; set; }
            public long Value { get; set; }
        }
    }
}
