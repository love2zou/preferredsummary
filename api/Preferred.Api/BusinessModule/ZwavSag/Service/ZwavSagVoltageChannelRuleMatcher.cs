using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;

namespace Preferred.Api.Services
{
    public static class ZwavSagVoltageChannelRuleMatcher
    {
        public class RuleItem
        {
            public string Keyword { get; set; }
            public string PhaseName { get; set; }
        }

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static DateTime CacheAtUtc = DateTime.MinValue;
        private static RuleItem[] Cache = Array.Empty<RuleItem>();

        public static async Task<RuleItem[]> LoadRulesAsync(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;
            if ((now - CacheAtUtc) <= TimeSpan.FromMinutes(2))
                return Cache;

            await Gate.WaitAsync();
            try
            {
                now = DateTime.UtcNow;
                if ((now - CacheAtUtc) <= TimeSpan.FromMinutes(2))
                    return Cache;

                var rules = await context.ZwavSagChannelRules
                    .AsNoTracking()
                    .OrderBy(x => x.SeqNo)
                    .ThenBy(x => x.Id)
                    .Select(x => new { x.RuleName, x.PhaseName })
                    .ToListAsync();

                Cache = rules
                    .Where(x => !string.IsNullOrWhiteSpace(x.RuleName) && !string.IsNullOrWhiteSpace(x.PhaseName))
                    .Select(x => new RuleItem
                    {
                        Keyword = x.RuleName.Trim().ToUpperInvariant(),
                        PhaseName = NormalizePhaseName(x.PhaseName)
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Keyword) && !string.IsNullOrWhiteSpace(x.PhaseName))
                    .ToArray();
                CacheAtUtc = now;

                return Cache;
            }
            finally
            {
                Gate.Release();
            }
        }

        public static string MatchPhase(string channelName, string channelCode, string unit, RuleItem[] rules)
        {
            if (rules == null || rules.Length == 0)
                return string.Empty;

            var text = $"{channelName} {channelCode} {unit}".ToUpperInvariant();
            foreach (var r in rules)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Keyword))
                    continue;
                if (text.Contains(r.Keyword))
                    return r.PhaseName ?? string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizePhaseName(string phaseName)
        {
            var p = (phaseName ?? string.Empty).Trim().ToUpperInvariant();
            if (p == "A" || p == "B" || p == "C" || p == "AB" || p == "BC" || p == "CA")
                return p;
            return string.Empty;
        }
    }
}
