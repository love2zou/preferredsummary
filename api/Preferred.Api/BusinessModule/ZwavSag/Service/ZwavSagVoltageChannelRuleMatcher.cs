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
        public sealed class RuleItem
        {
            public string Keyword { get; set; }
            public string PhaseName { get; set; }
        }

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        private static DateTime CacheAtUtc = DateTime.MinValue;
        private static RuleItem[] Cache = Array.Empty<RuleItem>();

        public static async Task<RuleItem[]> LoadRulesAsync(ApplicationDbContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var now = DateTime.UtcNow;
            if (IsCacheValid(now))
                return Cache;

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                now = DateTime.UtcNow;
                if (IsCacheValid(now))
                    return Cache;

                var rules = await context.ZwavSagChannelRules
                    .AsNoTracking()
                    .OrderBy(x => x.SeqNo)
                    .ThenBy(x => x.Id)
                    .Select(x => new
                    {
                        x.RuleName,
                        x.PhaseName
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                Cache = rules
                    .Where(x => !string.IsNullOrWhiteSpace(x.RuleName) && !string.IsNullOrWhiteSpace(x.PhaseName))
                    .Select(x => new RuleItem
                    {
                        Keyword = NormalizeKeyword(x.RuleName),
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

            var text = BuildMatchText(channelName, channelCode, unit);
            if (text.Length == 0)
                return string.Empty;

            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.Keyword))
                    continue;

                if (text.Contains(rule.Keyword, StringComparison.Ordinal))
                    return rule.PhaseName ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool IsCacheValid(DateTime now)
        {
            return Cache.Length > 0 && (now - CacheAtUtc) <= CacheTtl;
        }

        private static string NormalizeKeyword(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static string BuildMatchText(string channelName, string channelCode, string unit)
        {
            channelName = string.IsNullOrWhiteSpace(channelName) ? string.Empty : channelName.Trim();
            channelCode = string.IsNullOrWhiteSpace(channelCode) ? string.Empty : channelCode.Trim();
            unit = string.IsNullOrWhiteSpace(unit) ? string.Empty : unit.Trim();

            if (channelName.Length == 0 && channelCode.Length == 0 && unit.Length == 0)
                return string.Empty;

            return string.Concat(channelName, " ", channelCode, " ", unit).ToUpperInvariant();
        }

        private static string NormalizePhaseName(string phaseName)
        {
            var p = (phaseName ?? string.Empty).Trim().ToUpperInvariant();
            switch (p)
            {
                case "A":
                case "B":
                case "C":
                case "AB":
                case "BC":
                case "CA":
                    return p;
                default:
                    return string.Empty;
            }
        }
    }
}