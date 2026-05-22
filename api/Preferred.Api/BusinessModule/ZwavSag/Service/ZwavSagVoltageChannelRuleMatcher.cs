using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 暂降电压通道规则匹配器
    /// 优化目标：
    /// 1、稳健识别电压通道，尽量排除电流通道；
    /// 2、稳健识别相别（A/B/C/AB/BC/CA）；
    /// 3、稳健识别分组（高压侧/中压侧/低压侧/低压分支）；
    /// 4、减少三侧录波场景下的误归组。
    /// </summary>
    public static class ZwavSagVoltageChannelRuleMatcher
    {
        /// <summary>
        /// 通道相别匹配规则。
        /// Keyword 表示匹配关键字，PhaseName 表示识别出的标准相名。
        /// </summary>
        public sealed class RuleItem
        {
            public string Keyword { get; set; }
            public string PhaseName { get; set; }
            public int PhaseType { get; set; }
            public decimal PhaseValue { get; set; }
            public bool Enabled { get; set; }
        }

        public sealed class RuleMatchResult
        {
            public bool HasMatch { get; set; }
            public string PhaseName { get; set; }
            public int PhaseType { get; set; }
            public decimal PhaseValue { get; set; }
            public bool Enabled { get; set; }
        }

        /// <summary>
        /// 通道分组匹配规则。
        /// Keyword 表示匹配关键字，GroupName 表示识别出的标准分组名。
        /// </summary>
        public sealed class GroupRuleItem
        {
            public string Keyword { get; set; }
            public string GroupName { get; set; }
        }

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        private static readonly Regex MultiSpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        private static readonly Regex VoltageDecorRegex = new Regex(
            @"(\(|（).+?(\)|）)|\b(UA|UB|UC|UN|UAB|UBC|UCA|VAN|VBN|VCN|PHASE|A相|B相|C相|AB线|BC线|CA线|AB相|BC相|CA相)\b|线电压|相电压|母线电压|保护电压|电压|VOLTAGE|VOLT|KV|MV|V",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BranchRegex = new Regex(
            @"低压侧\s*([1-9]\d*)\s*分支|LV\s*([1-9]\d*)\s*BRANCH",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HvRegex = new Regex(
            @"高压侧|高压|HV|HIGH\s*VOLTAGE",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MvRegex = new Regex(
            @"中压侧|中压|MV|MEDIUM\s*VOLTAGE",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LvRegex = new Regex(
            @"低压侧|低压|LV|LOW\s*VOLTAGE",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] VoltageNameKeywords = new[]
        {
            "电压", "相电压", "线电压", "母线电压", "保护电压", "PT", "TV", "VOLT", "VOLTAGE", "BUS VOLT", "BUSV"
        };

        private static readonly string[] VoltageCodeKeywords = new[]
        {
            "UA", "UB", "UC", "UN", "UAB", "UBC", "UCA", "VAN", "VBN", "VCN", "PT", "TV"
        };

        private static readonly string[] CurrentKeywords = new[]
        {
            "电流", "相电流", "线电流", "CURRENT", "AMP", "AMPERE", "IA", "IB", "IC", "IN"
        };

        /// <summary>
        /// 注意顺序：先匹配线电压，再匹配相电压，避免 UA 抢先匹配 UAB。
        /// </summary>
        private static readonly (string Keyword, string Phase)[] BuiltInPhasePatterns = new[]
        {
            ("UAB", "AB"), ("UBC", "BC"), ("UCA", "CA"),
            ("AB线", "AB"), ("BC线", "BC"), ("CA线", "CA"),
            ("AB相", "AB"), ("BC相", "BC"), ("CA相", "CA"),
            ("AB PHASE", "AB"), ("BC PHASE", "BC"), ("CA PHASE", "CA"),

            ("(UA)", "A"), ("(UB)", "B"), ("(UC)", "C"),
            ("A相", "A"), ("B相", "B"), ("C相", "C"),
            ("A PHASE", "A"), ("B PHASE", "B"), ("C PHASE", "C"),
            ("UA", "A"), ("UB", "B"), ("UC", "C"),
            ("VAN", "A"), ("VBN", "B"), ("VCN", "C")
        };

        private static readonly string[] BuiltInGroupKeywords = new[]
        {
            "高压侧",
            "中压侧",
            "低压侧1分支",
            "低压侧2分支",
            "低压侧3分支",
            "低压侧4分支",
            "低压侧"
        };

        private static DateTime CacheAtUtc = DateTime.MinValue;
        private static RuleItem[] Cache = Array.Empty<RuleItem>();
        private static GroupRuleItem[] GroupCache = Array.Empty<GroupRuleItem>();

        /// <summary>
        /// 加载相别匹配规则。
        /// 使用短时缓存减少同一批分析任务重复访问规则表的开销。
        /// </summary>
        public static async Task<RuleItem[]> LoadRulesAsync(ApplicationDbContext context)
        {
            await EnsureCacheAsync(context).ConfigureAwait(false);
            return Cache;
        }

        /// <summary>
        /// 加载分组匹配规则。
        /// </summary>
        public static async Task<GroupRuleItem[]> LoadGroupRulesAsync(ApplicationDbContext context)
        {
            await EnsureCacheAsync(context).ConfigureAwait(false);
            return GroupCache;
        }

        /// <summary>
        /// 根据通道名称、编码和单位匹配相别。
        /// 优先使用内置高置信模式，其次再使用数据库规则做补充。
        /// </summary>
        public static string MatchPhase(string channelName, string channelCode, string unit, RuleItem[] rules)
        {
            var text = BuildMatchText(channelName, channelCode, unit);
            if (text.Length == 0)
                return string.Empty;

            // 两阶段识别：
            // 1. 先用启用规则识别相别，保证词库显式配置优先生效；
            // 2. 在已识别出的候选结果上，再检查是否命中排除规则。
            var enabledRule = MatchEnabledRule(channelName, channelCode, unit, rules);
            string phase = enabledRule.HasMatch
                ? (enabledRule.PhaseName ?? string.Empty)
                : MatchBuiltInPhase(text);

            if (string.IsNullOrWhiteSpace(phase))
                return string.Empty;

            var excludedRule = MatchExcludedRule(channelName, channelCode, unit, rules);
            if (excludedRule.HasMatch)
                return string.Empty;

            return phase;
        }

        public static RuleMatchResult MatchRule(string channelName, string channelCode, string unit, RuleItem[] rules)
        {
            return MatchRule(channelName, channelCode, unit, rules, enabledFilter: null);
        }

        public static RuleMatchResult MatchEnabledRule(string channelName, string channelCode, string unit, RuleItem[] rules)
        {
            return MatchRule(channelName, channelCode, unit, rules, enabledFilter: true);
        }

        public static RuleMatchResult MatchExcludedRule(string channelName, string channelCode, string unit, RuleItem[] rules)
        {
            return MatchRule(channelName, channelCode, unit, rules, enabledFilter: false);
        }

        private static RuleMatchResult MatchRule(string channelName, string channelCode, string unit, RuleItem[] rules, bool? enabledFilter)
        {
            var text = BuildMatchText(channelName, channelCode, unit);
            if (text.Length == 0 || rules == null || rules.Length == 0)
                return new RuleMatchResult();

            RuleMatchResult best = null;
            int bestScore = -1;

            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.Keyword) || string.IsNullOrEmpty(rule.PhaseName))
                    continue;

                if (enabledFilter.HasValue && rule.Enabled != enabledFilter.Value)
                    continue;

                if (!ContainsKeyword(text, rule.Keyword))
                    continue;

                int score = rule.Keyword.Length;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = new RuleMatchResult
                {
                    HasMatch = true,
                    PhaseName = rule.PhaseName,
                    PhaseType = rule.PhaseType,
                    PhaseValue = rule.PhaseValue,
                    Enabled = rule.Enabled
                };
            }

            return best ?? new RuleMatchResult();
        }

        /// <summary>
        /// 分组匹配原则：
        /// 1、优先规则表；
        /// 2、再走内置侧别识别；
        /// 3、不再宽松地用“清洗后的通道名”当组名，避免三侧误归组。
        /// </summary>
        public static string MatchGroup(string channelName, string channelCode, string unit, GroupRuleItem[] rules)
        {
            var text = BuildMatchText(channelName, channelCode, unit);
            if (text.Length == 0)
                return string.Empty;

            string bestGroup = string.Empty;
            int bestScore = -1;

            if (rules != null && rules.Length > 0)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    var rule = rules[i];
                    if (rule == null || string.IsNullOrEmpty(rule.Keyword) || string.IsNullOrEmpty(rule.GroupName))
                        continue;

                    if (!ContainsKeyword(text, rule.Keyword))
                        continue;

                    int score = rule.Keyword.Length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestGroup = rule.GroupName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(bestGroup))
                return bestGroup;

            return MatchBuiltInGroup(channelName, channelCode, unit);
        }

        /// <summary>
        /// 判断一个模拟量通道是否可视为电压通道。
        /// 该判断尽量保守，宁可少识别，也尽量避免把电流或杂项通道误当成电压。
        /// </summary>
        public static bool IsVoltageChannel(string channelName, string channelCode, string unit)
        {
            string name = (channelName ?? string.Empty).Trim().ToUpperInvariant();
            string code = (channelCode ?? string.Empty).Trim().ToUpperInvariant();
            string unitText = (unit ?? string.Empty).Trim().ToUpperInvariant();
            string text = BuildMatchText(channelName, channelCode, unit);

            if (text.Length == 0)
                return false;

            // 先排除明显电流通道
            if (ContainsAny(name, CurrentKeywords) || ContainsAny(code, CurrentKeywords))
                return false;

            if (unitText == "A" || unitText == "KA" || unitText == "MA")
                return false;

            // 单位命中电压最可靠
            if (unitText == "V" || unitText == "KV" || unitText == "MV")
                return true;

            // 名称/编码命中电压关键字
            if (ContainsAny(name, VoltageNameKeywords))
                return true;

            if (ContainsAny(code, VoltageCodeKeywords))
                return true;

            // 没有明显电压特征，则不作为电压通道
            return false;
        }

        private static async Task EnsureCacheAsync(ApplicationDbContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var now = DateTime.UtcNow;
            if (IsCacheValid(now))
                return;

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                now = DateTime.UtcNow;
                if (IsCacheValid(now))
                    return;

                var rules = await context.ZwavSagChannelRules
                    .AsNoTracking()
                    .OrderBy(x => x.SeqNo)
                    .ThenBy(x => x.Id)
                    .Select(x => new
                    {
                        x.RuleName,
                        x.PhaseName,
                        x.PhaseType,
                        x.PhaseValue,
                        x.Enabled
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                Cache = rules
                    .Where(x => !string.IsNullOrWhiteSpace(x.RuleName) && !string.IsNullOrWhiteSpace(x.PhaseName))
                    .Select(x => new RuleItem
                    {
                        Keyword = NormalizeKeyword(x.RuleName),
                        PhaseName = NormalizePhaseName(x.PhaseName),
                        PhaseType = x.PhaseType,
                        PhaseValue = x.PhaseValue,
                        Enabled = x.Enabled
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Keyword) && !string.IsNullOrWhiteSpace(x.PhaseName))
                    .OrderByDescending(x => x.Keyword.Length)
                    .ToArray();

                try
                {
                    var groupRules = await context.ZwavSagGroupRules
                        .AsNoTracking()
                        .OrderBy(x => x.SeqNo)
                        .ThenBy(x => x.Id)
                        .Select(x => new
                        {
                            x.RuleName,
                            x.GroupName
                        })
                        .ToListAsync()
                        .ConfigureAwait(false);

                    GroupCache = groupRules
                        .Where(x => !string.IsNullOrWhiteSpace(x.RuleName) && !string.IsNullOrWhiteSpace(x.GroupName))
                        .Select(x => new GroupRuleItem
                        {
                            Keyword = NormalizeKeyword(x.RuleName),
                            GroupName = NormalizeGroupName(x.GroupName)
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Keyword) && !string.IsNullOrWhiteSpace(x.GroupName))
                        .OrderByDescending(x => x.Keyword.Length)
                        .ToArray();
                }
                catch
                {
                    GroupCache = Array.Empty<GroupRuleItem>();
                }

                CacheAtUtc = now;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static bool IsCacheValid(DateTime now)
        {
            return Cache.Length > 0 && (now - CacheAtUtc) <= CacheTtl;
        }

        private static string MatchBuiltInPhase(string text)
        {
            string bestPhase = string.Empty;
            int bestScore = -1;

            for (int i = 0; i < BuiltInPhasePatterns.Length; i++)
            {
                var item = BuiltInPhasePatterns[i];
                    if (ContainsKeyword(text, item.Keyword))
                    {
                    int score = item.Keyword.Length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPhase = item.Phase;
                    }
                }
            }

            return bestPhase;
        }

        private static string MatchBuiltInGroup(string channelName, string channelCode, string unit)
        {
            string raw = string.IsNullOrWhiteSpace(channelName)
                ? (channelCode ?? string.Empty)
                : channelName;

            raw = (raw ?? string.Empty).Trim();
            if (raw.Length == 0)
                raw = BuildMatchText(channelName, channelCode, unit);

            if (raw.Length == 0)
                return string.Empty;

            // 先识别低压分支
            var branchMatch = BranchRegex.Match(raw);
            if (branchMatch.Success)
            {
                string branchNo = branchMatch.Groups[1].Success
                    ? branchMatch.Groups[1].Value
                    : branchMatch.Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(branchNo))
                    return $"低压侧{branchNo}分支";
            }

            // 再识别高/中/低压侧
            if (HvRegex.IsMatch(raw))
                return "高压侧";

            if (MvRegex.IsMatch(raw))
                return "中压侧";

            if (LvRegex.IsMatch(raw))
                return "低压侧";

            // 再退一步：匹配内置固定关键字
            for (int i = 0; i < BuiltInGroupKeywords.Length; i++)
            {
                var keyword = BuiltInGroupKeywords[i];
                if (raw.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return keyword;
            }

            // 不再宽松 fallback 到“清洗后的任意文本”
            return string.Empty;
        }

        private static string NormalizeKeyword(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static bool ContainsAny(string text, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null || keywords.Length == 0)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                var keyword = keywords[i];
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
                return false;

            if (text.Contains(keyword, StringComparison.Ordinal))
                return true;

            var compactText = CompactMatchText(text);
            var compactKeyword = CompactMatchText(keyword);
            if (compactText.Length == 0 || compactKeyword.Length == 0)
                return false;

            return compactText.Contains(compactKeyword, StringComparison.Ordinal);
        }

        private static string CompactMatchText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-' && ch != '/' && ch != '\\')
                .ToArray())
                .ToUpperInvariant();
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

        private static string NormalizeGroupName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
