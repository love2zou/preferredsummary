using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 电压暂降分析器（单通道独立输出版）
    ///
    /// 【整体设计目标】
    /// 1、不做跨通道合并，不做跨侧合并；
    /// 2、每个电压通道独立识别暂降事件；
    /// 3、每个通道检测到的事件直接输出，避免多侧录波相互污染；
    /// 4、RMS 点全量输出，供前端完整展示波形与分析结果；
    /// 5、强化恢复判定与尾段稳态裁剪，避免波形已恢复却仍被识别为暂降；
    /// 6、结果按组（高压/中压/低压）+ 相序（A/B/C）+ 时间排序；
    /// 7、事件和 RMS 点都保留各自真实通道名称，避免被高压侧名称污染；
    /// 8、恢复阈值优先取前端传入 RecoverThresholdPct，未传时退回 SagThresholdPct + HysteresisPct。
    ///
    /// 【这版算法的核心思想】
    /// 传统做法经常把多相、多侧信号先合并再判断，这样容易出现两个问题：
    /// - 某一侧的参考值把另一侧“带偏”，造成中压/低压识别不到；
    /// - 某一通道明明已恢复，但因为合并窗口和阈值策略，尾巴还会被继续判成暂降。
    ///
    /// 因此本类采用“单通道独立分析”：
    /// - 每个通道先单独计算 RMS 序列；
    /// - 每个通道独立推断参考电压；
    /// - 每个通道独立判断事件进入、恢复、结束；
    /// - 最后只做结果排序，不做跨通道物理合并。
    ///
    /// 这样更符合你现在的业务诉求：单通道识别出来就直接输出，不再做复杂归并。
    /// </summary>
    public class ZwavSagAnalyzer : IZwavSagAnalyzer
    {
        /// <summary>
        /// 允许的模拟量最大绝对值，防止异常值、溢出值参与 RMS 计算。
        /// </summary>
        private const double MaxSupportedAnalogMagnitude = 999999999999d;

        /// <summary>
        /// decimal 安全上限，避免 double -> decimal 转换时溢出。
        /// </summary>
        private const decimal MaxSupportedDecimalMagnitude = 999999999999.999999m;

        /// <summary>
        /// 事件时长的安全上限，防止异常数据导致 duration 无穷大或过大。
        /// </summary>
        private const decimal MaxSupportedDurationMs = 999999999.999m;

        /// <summary>
        /// 分析入口。
        ///
        /// 流程概览：
        /// 1）校验上下文；
        /// 2）遍历每一个电压通道；
        /// 3）构建该通道的 RMS 序列；
        /// 4）为该通道独立推断参考电压；
        /// 5）生成标准化后的检测序列（RmsPct / DetectPct）；
        /// 6）输出 RMS 点；
        /// 7）执行单通道事件检测；
        /// 8）将检测窗口转成最终事件结果；
        /// 9）整体排序后返回。
        ///
        /// 注意：这里是“单通道独立输出”，没有任何跨通道事件融合逻辑。
        /// </summary>
        public Task<ZwavSagAnalyzeResult> AnalyzeAsync(ZwavSagAnalyzeContext context)
        {
            // 先做上下文校验，避免后面在深层方法中出现难定位的空引用或非法参数问题。
            ValidateContext(context);

            var result = new ZwavSagAnalyzeResult();

            // 没有电压通道，直接返回空结果。
            if (context.VoltageChannels == null || context.VoltageChannels.Length == 0)
                return Task.FromResult(result);

            // 按通道逐个分析。
            foreach (var channel in context.VoltageChannels)
            {
                if (channel == null)
                    continue;

                // 第一步：把单通道的原始采样数据压缩成 RMS 窗口序列。
                // 后续的暂降检测不再基于原始瞬时采样值，而是基于 RMS 序列进行。
                var rawSeries = BuildSingleChannelRawRmsSeries(context, channel);
                if (rawSeries == null || rawSeries.Points == null || rawSeries.Points.Count == 0)
                    continue;

                // 第二步：解析该通道的“锚定参考电压”。
                //
                // 优先逻辑：
                // - 如果前端传了 ReferenceVoltage，并且它与该通道量级兼容，则可采用；
                // - 否则退回通道自身稳态段推断值。
                //
                // 这样可以避免“高压侧参考值误用到中压/低压侧”造成整段误判。
                decimal anchorReferenceVoltage = ResolveChannelReferenceVoltage(
                    context,
                    rawSeries,
                    context.ReferenceVoltage);

                if (anchorReferenceVoltage <= 0m)
                    continue;

                // 第三步：生成检测用标准化序列。
                //
                // 这里会同时生成两套百分比：
                // 1）RmsPct：基于 anchorReferenceVoltage 计算，作为最终输出展示值；
                // 2）DetectPct：基于检测参考值计算，作为事件检测值。
                //
                // 为什么要分两套？
                // - 输出值要求稳定、直观；
                // - 检测值要求更鲁棒，必要时允许使用滑动参考。
                var series = BuildNormalizedSeries(context, rawSeries, anchorReferenceVoltage);

                // 第四步：把 RMS 点写入输出结果，供前端完整展示波形/RMS曲线。
                AppendRmsPointResults(new List<ChannelRmsSeries> { series }, result.RmsPoints);

                // 第五步：先判断该通道是否有分析价值。
                // 例如：全程几乎没有有效幅值、参考值异常、有效点太少等，都应跳过。
                if (!IsUsableVoltageSeriesRelaxed(series))
                    continue;

                // 第六步：进行单通道事件检测。
                var windows = DetectSingleChannelEvents(context, series);
                if (windows == null || windows.Count == 0)
                    continue;

                // 第七步：把检测窗口转成标准事件输出结构。
                for (int i = 0; i < windows.Count; i++)
                {
                    var evt = BuildSingleChannelEventResult(context, windows[i]);
                    if (evt != null)
                        result.Events.Add(evt);
                }
            }

            // 最后做统一排序，保证输出顺序可读且稳定：
            // 高压 -> 中压 -> 低压；A -> B -> C；再按时间排序。
            SortRmsPoints(result.RmsPoints);
            SortEvents(result.Events);

            return Task.FromResult(result);
        }

        #region 核心流程

        /// <summary>
        /// 把原始 RMS 序列转换成“检测标准化序列”。
        ///
        /// 关键点：
        /// 1）anchorReferenceVoltage：
        ///    用于输出展示的统一参考值；
        ///
        /// 2）detectReferenceVoltage：
        ///    用于检测时的局部参考值；
        ///    如果是 Sliding 模式，会随着局部稳态缓慢变化；
        ///
        /// 3）RmsPct：
        ///    面向展示，等于 p.Rms / anchorReferenceVoltage * 100；
        ///
        /// 4）DetectPct：
        ///    面向检测，等于 p.Rms / detectReferenceVoltage * 100。
        ///
        /// 这样可以避免慢漂移场景下，输出值和检测值使用同一套参考导致误判。
        /// </summary>
        private static ChannelRmsSeries BuildNormalizedSeries(
            ZwavSagAnalyzeContext context,
            ChannelRawRmsSeries rawSeries,
            decimal anchorReferenceVoltage)
        {
            // Declared 模式：DetectPct 与输出共用固定参考值；
            // Sliding 模式：DetectPct 跟随局部稳态变化，降低慢漂移带来的误判。
            bool useSlidingReference = UseSlidingReference(context);

            // 为每个 RMS 点生成一条“检测参考电压序列”。
            var detectReferences = BuildDetectReferenceSeries(rawSeries.Points, anchorReferenceVoltage, useSlidingReference);

            var series = new ChannelRmsSeries
            {
                ChannelIndex = rawSeries.ChannelIndex,
                GroupName = NormalizeGroupName(rawSeries.GroupName),
                ChannelName = NormalizeChannelName(rawSeries.ChannelName, rawSeries.ChannelIndex),
                Phase = NormalizePhase(rawSeries.Phase),
                ReferenceVoltage = anchorReferenceVoltage,
                UseSlidingReference = useSlidingReference
            };

            for (int i = 0; i < rawSeries.Points.Count; i++)
            {
                var p = rawSeries.Points[i];

                // 检测参考值优先取当前点的 detectReference；
                // 如果该值无效，则退回锚定参考值。
                decimal detectReferenceVoltage = detectReferences[i] > 0m
                    ? detectReferences[i]
                    : anchorReferenceVoltage;

                // 输出用百分比：始终基于 anchorReferenceVoltage。
                decimal rmsPct = anchorReferenceVoltage <= 0m
                    ? 0m
                    : decimal.Round(p.Rms / anchorReferenceVoltage * 100m, 3);

                // 检测用百分比：基于 detectReferenceVoltage。
                decimal detectPct = detectReferenceVoltage <= 0m
                    ? rmsPct
                    : decimal.Round(p.Rms / detectReferenceVoltage * 100m, 3);

                series.Points.Add(new RmsWindowPoint
                {
                    SampleNo = p.SampleNo,
                    TimeMs = p.TimeMs,
                    WindowStartMs = p.WindowStartMs,
                    WindowEndMs = p.WindowEndMs,
                    Rms = p.Rms,
                    RmsPct = rmsPct,
                    DetectPct = detectPct,

                    // DetectReferenceVoltage：真正参与检测的参考值。
                    DetectReferenceVoltage = detectReferenceVoltage,

                    // OutputReferenceVoltage：最终展示到前端/落库时使用的参考值。
                    // 在 Sliding 模式下，为了让前端看到更真实的“局部参考关系”，可输出 detectReferenceVoltage；
                    // 否则使用 anchorReferenceVoltage。
                    OutputReferenceVoltage = useSlidingReference
                        ? detectReferenceVoltage
                        : anchorReferenceVoltage
                });
            }

            // 对 DetectPct 做轻度平滑，减少毛刺对事件进入/恢复判断的影响。
            // 注意：这里只平滑 DetectPct，不改 RmsPct，因此不影响最终展示值。
            ApplyDetectionSmoothing(series);
            return series;
        }

        /// <summary>
        /// 对检测序列做轻度平滑，只用于事件检测，不影响最终输出数值。
        ///
        /// 思路：
        /// - 取当前点前后最多共 5 个点；
        /// - 先取中位数 median；
        /// - 再做 0.65 * median + 0.35 * raw 的加权；
        ///
        /// 这样既能抑制尖峰噪声，又不会把真实故障边沿过度抹平。
        /// </summary>
        private static void ApplyDetectionSmoothing(ChannelRmsSeries series)
        {
            if (series?.Points == null || series.Points.Count == 0)
                return;

            int n = series.Points.Count;
            var raw = series.Points.Select(x => x.DetectPct).ToArray();

            for (int i = 0; i < n; i++)
            {
                var vals = new List<decimal>(5);

                if (i - 2 >= 0) vals.Add(raw[i - 2]);
                if (i - 1 >= 0) vals.Add(raw[i - 1]);
                vals.Add(raw[i]);
                if (i + 1 < n) vals.Add(raw[i + 1]);
                if (i + 2 < n) vals.Add(raw[i + 2]);

                vals.Sort();
                decimal median = vals[vals.Count / 2];

                decimal detect = decimal.Round(median * 0.65m + raw[i] * 0.35m, 3);
                series.Points[i].DetectPct = detect;
            }
        }

        /// <summary>
        /// 单通道事件检测。
        ///
        /// 整体逻辑：
        /// 1）找到事件进入点；
        /// 2）进入事件后持续跟踪；
        /// 3）当连续若干点满足恢复条件时，认为事件结束；
        /// 4）对事件终点做二次回裁，去掉恢复后的拖尾；
        /// 5）若到序列尾部仍未恢复，再结合趋势判断和尾段稳态进行裁剪；
        /// 6）最终把过近窗口做轻量合并。
        ///
        /// 注意：这里的“合并”仅限同一通道、同一组、同一相、相距极短的两个小窗口，
        /// 不是跨通道合并。
        /// </summary>
        private static List<ChannelEventWindow> DetectSingleChannelEvents(
            ZwavSagAnalyzeContext context,
            ChannelRmsSeries series)
        {
            var result = new List<ChannelEventWindow>();
            if (series == null || series.Points == null || series.Points.Count == 0)
                return result;

            decimal sagThreshold = context.SagThresholdPct;

            // 恢复阈值优先使用前端传入 RecoverThresholdPct；
            // 如果没有，则用 SagThresholdPct + HysteresisPct。
            decimal recoverThreshold = ResolveRecoverThresholdPct(context);

            // 尝试从序列尾段估算“稳定恢复电平”。
            // 用于解决：虽然没严格到 recoverThreshold，但尾段已明显回到稳定稳态。
            decimal? tailStablePct = TryResolveTailStablePct(series, sagThreshold, recoverThreshold);

            bool inEvent = false;
            int startIndex = -1;
            int recoverStreak = 0;

            // 依据点数自动确定“连续几个恢复点才算恢复”，
            // 点数越少，要求越低；点数越多，要求越高。
            int recoverConfirmRows = ResolveRecoverConfirmRows(series);

            for (int i = 0; i < series.Points.Count; i++)
            {
                // 当前点是否具备“事件进入”的特征。
                bool shouldEnter = IsSagStartPoint(series, i, sagThreshold);

                // 当前点是否可视为“恢复点”。
                bool recovered = IsRecoveredPoint(series, i, recoverThreshold, tailStablePct);

                // 当前不在事件中时，只关心是否要进入事件。
                if (!inEvent)
                {
                    if (shouldEnter)
                    {
                        inEvent = true;

                        // 对起点做向前修正，尽量把事件开始点定位到真正跌破阈值的位置。
                        startIndex = RefineEventStartIndex(series, i, sagThreshold);
                        recoverStreak = 0;
                    }

                    continue;
                }

                // 已在事件中时，持续统计恢复连续点数。
                if (recovered)
                    recoverStreak++;
                else
                    recoverStreak = 0;

                // 连续恢复点数达到门槛，判定为事件结束。
                if (recoverStreak >= recoverConfirmRows)
                {
                    // 先得到原始终点：往前退 recoverConfirmRows 个点。
                    int rawEndIndex = Math.Max(startIndex, i - recoverConfirmRows);

                    // 再做一次“是否还真正处于故障”的回裁，避免恢复点被带入故障。
                    int endIndex = RefineEventEndIndex(series, rawEndIndex, recoverThreshold, tailStablePct);

                    // 进一步裁剪恢复尾巴，把已经恢复的稳定尾段去掉。
                    endIndex = TrimRecoveredTail(series, startIndex, endIndex, recoverThreshold, tailStablePct);

                    var evt = BuildSingleChannelWindow(context, series, startIndex, endIndex);
                    if (evt != null && evt.DurationMs >= context.MinDurationMs)
                        result.Add(evt);

                    // 清空状态，准备下一段事件。
                    inEvent = false;
                    startIndex = -1;
                    recoverStreak = 0;
                }
            }

            // 如果到序列末尾事件仍未结束，说明可能有两种情况：
            // 1）事件确实持续到最后；
            // 2）尾段已经恢复，但阈值没完全满足，导致没有触发 recoverConfirmRows。
            //
            // 这里再做两次裁剪：
            // - TrimRecoveredTail：去掉已恢复稳态的尾巴；
            // - TrimTailByRecoveryTrend：如果尾段整体呈恢复趋势，也提前截断。
            if (inEvent && startIndex >= 0)
            {
                int endIndex = series.Points.Count - 1;

                endIndex = TrimRecoveredTail(series, startIndex, endIndex, recoverThreshold, tailStablePct);
                endIndex = TrimTailByRecoveryTrend(series, startIndex, endIndex, sagThreshold, recoverThreshold, tailStablePct);

                var evt = BuildSingleChannelWindow(context, series, startIndex, endIndex);
                if (evt != null && evt.DurationMs >= context.MinDurationMs)
                    result.Add(evt);
            }

            // 对同一通道里“间隔极短”的碎片窗口做轻量合并。
            return MergeVeryCloseWindows(result);
        }

        /// <summary>
        /// 判断某个点是否可作为暂降起点。
        ///
        /// 判定较为宽松，是为了尽量“别漏掉真正的暂降”：
        /// 1）DetectPct 明显低于阈值；
        /// 2）RmsPct 明显低于阈值；
        /// 3）当前点和下一点连续都低于阈值。
        ///
        /// 这样比单纯 “当前点 < 阈值” 更稳健。
        /// </summary>
        private static bool IsSagStartPoint(ChannelRmsSeries series, int index, decimal sagThreshold)
        {
            if (series?.Points == null || index < 0 || index >= series.Points.Count)
                return false;

            var p = series.Points[index];

            // DetectPct 明显跌破阈值，优先判定进入。
            if (p.DetectPct <= sagThreshold - 1.0m)
                return true;

            // 原始输出百分比也明显跌落时，作为补充条件。
            if (p.RmsPct <= sagThreshold - 2.0m)
                return true;

            // 若当前点略低于阈值，但下一点也低于阈值，
            // 说明不是毛刺，而更像真实故障开始。
            if (index + 1 < series.Points.Count)
            {
                var next = series.Points[index + 1];
                if (p.DetectPct <= sagThreshold && next.DetectPct <= sagThreshold)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断某个点是否可作为“恢复点”。
        ///
        /// 标准恢复条件：
        /// - DetectPct >= recoverThreshold
        /// - 且 RmsPct >= recoverThreshold - 0.5
        ///
        /// 额外放宽：
        /// 如果尾段已识别出“稳态恢复电平”，则允许以这个稳态边界作为恢复判据，
        /// 解决“已经恢复到稳态，但因为没到理论阈值而迟迟不结束”的问题。
        /// </summary>
        private static bool IsRecoveredPoint(
            ChannelRmsSeries series,
            int index,
            decimal recoverThreshold,
            decimal? tailStablePct)
        {
            if (series?.Points == null || index < 0 || index >= series.Points.Count)
                return false;

            var p = series.Points[index];

            bool standardRecovered =
                p.DetectPct >= recoverThreshold &&
                p.RmsPct >= recoverThreshold - 0.5m;

            if (standardRecovered)
                return true;

            // 如果尾段已经呈现稳定恢复水平，则允许按接近稳态的边界判恢复。
            if (tailStablePct.HasValue)
            {
                decimal stablePct = tailStablePct.Value;

                // 给稳态边界留一点松弛量，防止尾段有极小抖动。
                decimal relaxedBoundary = Math.Max(0m, stablePct - 1.2m);

                bool nearStable =
                    p.RmsPct >= relaxedBoundary &&
                    p.DetectPct >= Math.Min(recoverThreshold, relaxedBoundary);

                if (nearStable)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 向前修正事件起点。
        ///
        /// 当前点可能只是“第一次明显被判成故障”的点，
        /// 但真正跌破阈值的边界可能更靠前。
        ///
        /// 因此这里向前回看：
        /// - 找到 “上一点正常、当前点故障” 的切换位置；
        /// - 或者找到更早的明显跌落点。
        /// </summary>
        private static int RefineEventStartIndex(ChannelRmsSeries series, int currentIndex, decimal sagThreshold)
        {
            if (series?.Points == null || currentIndex <= 0)
                return Math.Max(0, currentIndex);

            int start = currentIndex;

            for (int i = currentIndex; i > 0; i--)
            {
                var prev = series.Points[i - 1];
                var curr = series.Points[i];

                bool prevNormal = prev.DetectPct > sagThreshold;
                bool currSag = curr.DetectPct <= sagThreshold;

                // 找到阈值切换边界。
                if (prevNormal && currSag)
                {
                    start = i;
                    break;
                }

                // 如果当前点明显低于阈值，则也认为这里可以作为更靠前的候选起点。
                if (curr.RmsPct <= sagThreshold - 2m)
                    start = i;
            }

            return start;
        }

        /// <summary>
        /// 向前修正事件终点。
        ///
        /// 原始终点可能已经把部分恢复段带入进来，因此要从当前终点往前回看，
        /// 找到最后一个“仍处于故障”的点，作为更合理的事件终点。
        /// </summary>
        private static int RefineEventEndIndex(
            ChannelRmsSeries series,
            int currentEndIndex,
            decimal recoverThreshold,
            decimal? tailStablePct)
        {
            if (series?.Points == null || currentEndIndex < 0 || currentEndIndex >= series.Points.Count)
                return currentEndIndex;

            for (int i = currentEndIndex; i >= 0; i--)
            {
                var p = series.Points[i];

                bool stillSag =
                    p.DetectPct < recoverThreshold ||
                    p.RmsPct < recoverThreshold - 0.5m;

                // 若存在尾段稳态边界，则按更贴近实际的稳定边界判断是否仍处于暂降。
                if (tailStablePct.HasValue)
                {
                    decimal stableBoundary = Math.Max(0m, tailStablePct.Value - 1.2m);
                    stillSag =
                        p.DetectPct < Math.Min(recoverThreshold, stableBoundary) ||
                        p.RmsPct < stableBoundary;
                }

                if (stillSag)
                    return i;
            }

            return currentEndIndex;
        }

        /// <summary>
        /// 裁剪事件尾部已恢复的稳定区。
        ///
        /// 这个方法非常关键，主要解决：
        /// “波形最后已经回稳了，但还被算进暂降事件里”
        ///
        /// 典型原因：
        /// - RMS 是滑窗算出来的，会天然有“拖尾”；
        /// - 即使原始波形已经恢复，RMS 还会在后面几个窗口里逐步抬升；
        /// - 如果不做回裁，事件时长就会偏长。
        ///
        /// 实现方式：
        /// 从尾部往前扫描，统计连续满足“稳定恢复”的点数；
        /// 一旦达到 stableRows，就把这段尾巴剪掉。
        /// </summary>
        private static int TrimRecoveredTail(
            ChannelRmsSeries series,
            int startIndex,
            int endIndex,
            decimal recoverThreshold,
            decimal? tailStablePct)
        {
            if (series?.Points == null || series.Points.Count == 0)
                return endIndex;

            if (endIndex <= startIndex)
                return endIndex;

            int stableRows = ResolveStableTailRows(series);

            int candidate = endIndex;
            int stableCount = 0;

            for (int i = endIndex; i >= startIndex; i--)
            {
                var p = series.Points[i];

                bool stableRecovered =
                    p.DetectPct >= recoverThreshold &&
                    p.RmsPct >= recoverThreshold - 0.5m;

                // 若标准恢复不成立，但接近尾段稳态水平，也视为已恢复。
                if (!stableRecovered && tailStablePct.HasValue)
                {
                    decimal stableBoundary = Math.Max(0m, tailStablePct.Value - 1.2m);
                    stableRecovered =
                        p.RmsPct >= stableBoundary &&
                        p.DetectPct >= Math.Min(recoverThreshold, stableBoundary);
                }

                if (stableRecovered)
                {
                    stableCount++;

                    // 连续尾段恢复点数达到要求，则把尾巴剪掉。
                    if (stableCount >= stableRows)
                        candidate = i - 1;
                }
                else
                {
                    // 一旦遇到明显未恢复点，就停止往前剪。
                    break;
                }
            }

            if (candidate < startIndex)
                candidate = startIndex;

            return candidate;
        }

        /// <summary>
        /// 根据“恢复趋势”裁剪尾巴。
        ///
        /// 这个方法用于解决一种特殊情况：
        /// - 到序列尾部还没严格满足 recoverThreshold；
        /// - 但尾段已经整体呈明显回升趋势；
        /// - 且后面能看到接近稳态的点；
        ///
        /// 这种情况下，若还把整段尾巴都算进事件，会导致事件偏长。
        /// 因此这里通过“连续回升 + 后续接近稳态”的组合条件进行提前截断。
        /// </summary>
        private static int TrimTailByRecoveryTrend(
            ChannelRmsSeries series,
            int startIndex,
            int endIndex,
            decimal sagThreshold,
            decimal recoverThreshold,
            decimal? tailStablePct)
        {
            if (series?.Points == null || series.Points.Count == 0)
                return endIndex;

            if (endIndex <= startIndex)
                return endIndex;

            if (!tailStablePct.HasValue)
                return endIndex;

            decimal stablePct = tailStablePct.Value;

            // 近稳态边界：不强求必须达到 recoverThreshold，只要明显回到接近稳态即可。
            decimal nearStableBoundary = Math.Max(sagThreshold + 1.5m, stablePct - 2.0m);

            int stableNeed = ResolveStableTailRows(series);
            int risingNeed = Math.Max(2, stableNeed);

            // 先看尾段是否已有足够多的“近稳态”点。
            int stableTailCount = 0;
            for (int i = endIndex; i >= startIndex; i--)
            {
                var p = series.Points[i];
                bool stable =
                    p.RmsPct >= nearStableBoundary &&
                    p.DetectPct >= Math.Min(recoverThreshold, nearStableBoundary - 0.5m);

                if (stable)
                    stableTailCount++;
                else
                    break;
            }

            // 尾段近稳态点太少，不做趋势裁剪。
            if (stableTailCount < stableNeed)
                return endIndex;

            // 在事件尾部前一段里寻找“连续上升”的起点。
            for (int i = startIndex + 1; i <= endIndex - stableNeed; i++)
            {
                int risingCount = 0;

                for (int j = i; j < Math.Min(endIndex, i + 4); j++)
                {
                    var a = series.Points[j];
                    var b = series.Points[j + 1];

                    // 判断是否整体向上恢复。
                    if (b.RmsPct >= a.RmsPct - 0.3m && b.DetectPct >= a.DetectPct - 0.5m)
                        risingCount++;
                    else
                        break;
                }

                if (risingCount < risingNeed)
                    continue;

                // 连续上升后，若后面还能看到接近稳态的点，则说明故障主体已结束。
                bool laterNearStable = false;
                for (int k = i + risingNeed; k <= endIndex; k++)
                {
                    var p = series.Points[k];
                    if (p.RmsPct >= nearStableBoundary)
                    {
                        laterNearStable = true;
                        break;
                    }
                }

                if (laterNearStable)
                {
                    int candidate = i - 1;
                    if (candidate < startIndex)
                        candidate = startIndex;
                    return candidate;
                }
            }

            return endIndex;
        }

        /// <summary>
        /// 尝试从序列尾段推断“稳定恢复电平”。
        ///
        /// 逻辑：
        /// - 取最后 6 个点；
        /// - 计算中位数、最小值、最大值、离散度；
        /// - 若尾段整体电平足够高，且离散度足够小，则认为尾段已经进入稳定恢复状态；
        /// - 返回其稳定电平（中位数）；
        ///
        /// 否则返回 null。
        /// </summary>
        private static decimal? TryResolveTailStablePct(
            ChannelRmsSeries series,
            decimal sagThreshold,
            decimal recoverThreshold)
        {
            if (series?.Points == null || series.Points.Count < 6)
                return null;

            int tailTake = Math.Min(6, series.Points.Count);
            var tail = series.Points
                .Skip(series.Points.Count - tailTake)
                .Select(x => x.RmsPct)
                .Where(x => x > 0m)
                .ToList();

            if (tail.Count < 4)
                return null;

            tail.Sort();

            decimal median = Median(tail);
            decimal min = tail.First();
            decimal max = tail.Last();
            decimal spread = max - min;

            // 电平足够高：说明不是还在低谷。
            bool levelOk = median >= Math.Max(sagThreshold + 2.0m, recoverThreshold - 4.0m);

            // 足够稳定：波动范围不能过大。
            bool stableOk = spread <= Math.Max(2.5m, median * 0.03m);

            if (levelOk && stableOk)
                return decimal.Round(median, 3);

            return null;
        }

        /// <summary>
        /// 根据点数规模决定“连续多少个恢复点才确认恢复”。
        ///
        /// 点越少，要求越低；
        /// 点越多，要求越高。
        /// 这样可以在低采样和高采样场景下兼顾灵敏度与稳定性。
        /// </summary>
        private static int ResolveRecoverConfirmRows(ChannelRmsSeries series)
        {
            if (series?.Points == null)
                return 2;

            if (series.Points.Count <= 8)
                return 1;

            if (series.Points.Count <= 20)
                return 2;

            return 3;
        }

        /// <summary>
        /// 根据点数规模决定“尾段连续稳定点数”的门槛。
        /// 用于尾部恢复裁剪。
        /// </summary>
        private static int ResolveStableTailRows(ChannelRmsSeries series)
        {
            if (series?.Points == null)
                return 2;

            if (series.Points.Count <= 8)
                return 1;

            if (series.Points.Count <= 20)
                return 2;

            return 3;
        }

        /// <summary>
        /// 合并“非常接近”的事件窗口。
        ///
        /// 这里只做很轻量的修补性合并，适用于同一通道因噪声/抖动被切成两个极近窗口的情况。
        ///
        /// 合并条件：
        /// - 同一通道；
        /// - 同一相；
        /// - 同一组；
        /// - 两窗口间隔 gap <= 6ms；
        ///
        /// 注意：这不是你之前不需要的那种“多通道大合并”，
        /// 这里只是对同一条通道的碎片窗口做连续性修补。
        /// </summary>
        private static List<ChannelEventWindow> MergeVeryCloseWindows(List<ChannelEventWindow> windows)
        {
            if (windows == null || windows.Count <= 1)
                return windows ?? new List<ChannelEventWindow>();

            var ordered = windows
                .OrderBy(x => GroupSortOrder(x?.GroupName))
                .ThenBy(x => PhaseSortOrder(x?.Phase))
                .ThenBy(x => x.StartMs)
                .ThenBy(x => x.ChannelIndex)
                .ToList();

            var merged = new List<ChannelEventWindow>();
            ChannelEventWindow current = ordered[0];

            for (int i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                double gapMs = next.StartMs - current.EndMs;

                if (current.ChannelIndex == next.ChannelIndex &&
                    string.Equals(current.Phase, next.Phase, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.GroupName, next.GroupName, StringComparison.OrdinalIgnoreCase) &&
                    gapMs >= 0d &&
                    gapMs <= 6d)
                {
                    // 合并后终点取较大的那个。
                    current.EndMs = Math.Max(current.EndMs, next.EndMs);
                    current.DurationMs = SafeDuration(current.EndMs - current.StartMs);

                    // 残余电压更低的那个视为更严重的故障点。
                    if (next.ResidualVoltagePct < current.ResidualVoltagePct)
                    {
                        current.ResidualVoltagePct = next.ResidualVoltagePct;
                        current.ResidualVoltage = next.ResidualVoltage;
                        current.SagDepth = next.SagDepth;
                        current.SagPercent = next.SagPercent;
                        current.MinPointIndex = next.MinPointIndex;
                    }

                    continue;
                }

                merged.Add(current);
                current = next;
            }

            merged.Add(current);
            return merged;
        }

        /// <summary>
        /// 把单通道的事件窗口转为内部事件对象。
        ///
        /// 这里会计算：
        /// - 最低残余值；
        /// - 事件级参考电压；
        /// - 残余电压百分比；
        /// - 暂降深度、暂降百分比；
        /// - 事件起止时间（通过阈值交点插值估算）；
        ///
        /// 注意：
        /// 起止时间不是直接取窗口中心点，而是取“阈值交叉点插值结果”，
        /// 这样更接近真实故障边界。
        /// </summary>
        private static ChannelEventWindow BuildSingleChannelWindow(
            ZwavSagAnalyzeContext context,
            ChannelRmsSeries series,
            int startIndex,
            int endIndex)
        {
            if (series == null || series.Points == null || series.Points.Count == 0)
                return null;

            if (startIndex < 0 || endIndex < startIndex || endIndex >= series.Points.Count)
                return null;

            decimal minPct = decimal.MaxValue;
            decimal minRms = 0m;
            int minIndex = startIndex;

            // 找事件窗口内的最小 RMS 点，即故障最严重点。
            for (int i = startIndex; i <= endIndex; i++)
            {
                var p = series.Points[i];
                if (p.RmsPct < minPct)
                {
                    minPct = p.RmsPct;
                    minRms = p.Rms;
                    minIndex = i;
                }
            }

            // 事件级参考值尽量取故障前的局部稳态，而不是整段全局参考，
            // 这样更能反映真实故障前的基准水平。
            decimal eventReferenceVoltage = ResolveEventReferenceVoltage(context, series, startIndex, minIndex);
            if (eventReferenceVoltage <= 0m)
                eventReferenceVoltage = series.ReferenceVoltage;

            decimal eventResidualPct = eventReferenceVoltage <= 0m
                ? minPct
                : decimal.Round(minRms / eventReferenceVoltage * 100m, 3);

            // 起止时间通过阈值交点插值得到，尽量逼近真实边界。
            double startMs = EstimateEventStartMs(context, series, startIndex);
            double endMs = EstimateEventEndMs(context, series, endIndex);
            if (endMs < startMs)
                endMs = startMs;

            decimal durationMs = SafeDuration(endMs - startMs);

            decimal sagDepth = Math.Max(0m, decimal.Round(eventReferenceVoltage - minRms, 6));
            decimal sagPercent = Math.Max(0m, decimal.Round(100m - eventResidualPct, 3));

            return new ChannelEventWindow
            {
                ChannelIndex = series.ChannelIndex,
                GroupName = NormalizeGroupName(series.GroupName),
                ChannelName = NormalizeChannelName(series.ChannelName, series.ChannelIndex),
                Phase = NormalizePhase(series.Phase),
                StartMs = startMs,
                EndMs = endMs,
                DurationMs = durationMs,
                ReferenceVoltage = eventReferenceVoltage,
                ResidualVoltage = decimal.Round(minRms, 6),
                ResidualVoltagePct = eventResidualPct,
                SagDepth = sagDepth,
                SagPercent = sagPercent,
                MinPointIndex = minIndex
            };
        }

        /// <summary>
        /// 把内部事件窗口转成最终输出事件对象。
        ///
        /// 由于这版是“单通道独立输出”，所以：
        /// - TriggerPhase / EndPhase / WorstPhase 都直接指向当前通道名称；
        /// - Phases 集合只放 1 条当前通道记录；
        /// - IsMergedStatEvent = false；
        /// - RawEventCount = 1。
        /// </summary>
        private static ZwavSagEventResult BuildSingleChannelEventResult(
            ZwavSagAnalyzeContext context,
            ChannelEventWindow window)
        {
            if (context == null || window == null)
                return null;

            // 残余百分比低于中断阈值，则归类为 Interruption；否则为 Sag。
            bool isInterruption = window.ResidualVoltagePct <= context.InterruptThresholdPct;

            string groupName = NormalizeGroupName(window.GroupName);
            string channelName = NormalizeChannelName(window.ChannelName, window.ChannelIndex);
            string phase = NormalizePhase(window.Phase);

            var phaseResult = new ZwavSagEventPhaseResult
            {
                ChannelIndex = window.ChannelIndex,
                GroupName = groupName,
                ChannelName = channelName,
                Phase = phase,
                StartTimeUtc = ConvertToUtc(context, window.StartMs),
                EndTimeUtc = ConvertToUtc(context, window.EndMs),
                DurationMs = window.DurationMs,
                ReferenceType = context.ReferenceType,
                ReferenceVoltage = window.ReferenceVoltage,
                ResidualVoltage = window.ResidualVoltage,
                ResidualVoltagePct = window.ResidualVoltagePct,
                SagDepth = window.SagDepth,
                SagPercent = window.SagPercent,
                StartAngleDeg = null,
                PhaseJumpDeg = null,
                SagThresholdPct = context.SagThresholdPct,
                InterruptThresholdPct = context.InterruptThresholdPct,
                HysteresisPct = context.HysteresisPct,
                IsTriggerPhase = true,
                IsEndPhase = true,
                IsWorstPhase = true
            };

            return new ZwavSagEventResult
            {
                ChannelIndex = window.ChannelIndex,
                GroupName = groupName,
                ChannelName = channelName,
                EventType = isInterruption ? "Interruption" : "Sag",
                StartTimeUtc = ConvertToUtc(context, window.StartMs),
                EndTimeUtc = ConvertToUtc(context, window.EndMs),
                OccurTimeUtc = ConvertToUtc(context, window.StartMs),
                DurationMs = window.DurationMs,
                TriggerPhase = channelName,
                EndPhase = channelName,
                WorstPhase = channelName,
                ReferenceType = context.ReferenceType,
                ReferenceVoltage = window.ReferenceVoltage,
                ResidualVoltage = window.ResidualVoltage,
                ResidualVoltagePct = window.ResidualVoltagePct,
                SagDepth = window.SagDepth,
                SagPercent = window.SagPercent,
                StartAngleDeg = null,
                PhaseJumpDeg = null,
                SagThresholdPct = context.SagThresholdPct,
                InterruptThresholdPct = context.InterruptThresholdPct,
                HysteresisPct = context.HysteresisPct,
                IsMergedStatEvent = false,
                MergeGroupId = null,
                RawEventCount = 1,
                Phases = new List<ZwavSagEventPhaseResult> { phaseResult }
            };
        }

        /// <summary>
        /// 构建单通道原始 RMS 序列。
        ///
        /// 流程：
        /// 1）从上下文中取出该通道原始采样值；
        /// 2）按采样间隔自动分段，避免采样率变化时混算；
        /// 3）每段独立计算 RMS 点；
        /// 4）汇总后按时间排序返回。
        ///
        /// 这里的“按段计算”很重要：
        /// 如果录波文件中采样间隔前后不一致，直接整段一锅算会导致 RMS 失真。
        /// </summary>
        private static ChannelRawRmsSeries BuildSingleChannelRawRmsSeries(
            ZwavSagAnalyzeContext context,
            ZwavVoltageChannelContext channel)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (channel == null)
                return null;

            var samples = context.Samples;
            if (samples == null || samples.Count < 2)
                return null;

            double?[] values;

            // 如果外部已经预先缓存了该通道的序列，优先使用缓存，避免重复从字典逐点提取。
            if (context.ChannelSeriesMap != null &&
                context.ChannelSeriesMap.TryGetValue(channel.ChannelIndex, out var cachedValues) &&
                cachedValues != null &&
                cachedValues.Length == samples.Count)
            {
                values = cachedValues;
            }
            else
            {
                int sampleCount = samples.Count;
                values = new double?[sampleCount];

                // 从每个采样点的 ChannelValues 中提取当前通道值。
                for (int i = 0; i < sampleCount; i++)
                {
                    var row = samples[i];
                    if (row?.ChannelValues != null &&
                        row.ChannelValues.TryGetValue(channel.ChannelIndex, out var value) &&
                        value.HasValue)
                    {
                        values[i] = value.Value;
                    }
                    else
                    {
                        values[i] = null;
                    }
                }
            }

            // 自动基于采样间隔分段。
            var computedSegments = GetAnalyzableSegments(samples, context.FrequencyHz);

            // 如果自动分段结果为空，则退回上下文中已有分段。
            var segments = computedSegments != null && computedSegments.Count > 0
                ? computedSegments
                : context.AnalyzableSegments;

            if (segments == null || segments.Count == 0)
                return null;

            // 采样率切换时必须按段独立构建 RMS，避免不同采样步长混用。
            var rawPoints = new List<RawRmsWindowPoint>();

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                var segPoints = BuildSegmentRmsPoints(
                    samples,
                    values,
                    seg.StartIndex,
                    seg.EndIndex,
                    context.FrequencyHz,
                    seg.SampleIntervalMs);

                if (segPoints.Count > 0)
                    rawPoints.AddRange(segPoints);
            }

            if (rawPoints.Count == 0)
                return null;

            return new ChannelRawRmsSeries
            {
                ChannelIndex = channel.ChannelIndex,
                GroupName = NormalizeGroupName(channel.GroupName),
                ChannelName = NormalizeChannelName(channel.ChannelName, channel.ChannelIndex),
                Phase = ResolvePhaseName(channel, context.PhaseRules),
                Points = rawPoints.OrderBy(x => x.TimeMs).ToList()
            };
        }

        /// <summary>
        /// 在一个采样间隔稳定的分段内计算 RMS 点。
        ///
        /// 两种策略：
        /// 1）高采样场景（每周波点数 >= 8）：
        ///    用“1周波窗口 + 半周波更新”的标准 RMS；
        ///
        /// 2）低采样场景（每周波点数太少）：
        ///    无法支撑标准周波 RMS，只能退化成局部均方根近似。
        /// </summary>
        private static List<RawRmsWindowPoint> BuildSegmentRmsPoints(
            IReadOnlyList<ZwavSagSamplePoint> samples,
            double?[] values,
            int segStart,
            int segEnd,
            decimal frequencyHz,
            double sampleIntervalMs)
        {
            var result = new List<RawRmsWindowPoint>();
            if (samples == null || values == null || segEnd <= segStart)
                return result;

            double sampleRate = sampleIntervalMs > 0 ? 1000d / sampleIntervalMs : 0d;

            // 估算每周波点数。
            int samplesPerCycle = sampleRate > 0
                ? Math.Max(1, (int)Math.Round(sampleRate / (double)frequencyHz))
                : 1;

            int segLength = segEnd - segStart + 1;
            if (segLength <= 0)
                return result;

            // 高频场景：标准“1周波窗口 + 半周波更新”。
            if (samplesPerCycle >= 8)
            {
                int windowSize = samplesPerCycle;
                int stepSize = Math.Max(1, samplesPerCycle / 2);

                if (segLength < windowSize)
                    return result;

                double sumSq = 0d;
                int validCount = 0;

                int firstStart = segStart;
                int firstEnd = segStart + windowSize - 1;

                // 初始化第一个窗口。
                for (int i = firstStart; i <= firstEnd; i++)
                {
                    if (values[i].HasValue)
                    {
                        double v = values[i].Value;
                        if (!IsUsableAnalogValue(v))
                            continue;

                        sumSq += v * v;
                        validCount++;
                    }
                }

                // 有效点数达到 80% 以上才输出该 RMS 点，避免缺失值太多导致 RMS 不可靠。
                if (validCount >= Math.Max(1, (int)Math.Ceiling(windowSize * 0.8d)))
                    result.Add(CreateRawRmsWindowPoint(samples, firstStart, firstEnd, sumSq, validCount));

                // 后续窗口使用滑动更新，提高性能。
                for (int start = segStart + stepSize; start + windowSize - 1 <= segEnd; start += stepSize)
                {
                    int prevStart = start - stepSize;
                    int prevEnd = prevStart + windowSize - 1;
                    int end = start + windowSize - 1;

                    // 去掉离开窗口的点。
                    for (int i = prevStart; i < start; i++)
                    {
                        if (values[i].HasValue)
                        {
                            double v = values[i].Value;
                            if (!IsUsableAnalogValue(v))
                                continue;

                            sumSq -= v * v;
                            validCount--;
                        }
                    }

                    // 加入新进入窗口的点。
                    for (int i = prevEnd + 1; i <= end; i++)
                    {
                        if (values[i].HasValue)
                        {
                            double v = values[i].Value;
                            if (!IsUsableAnalogValue(v))
                                continue;

                            sumSq += v * v;
                            validCount++;
                        }
                    }

                    if (validCount >= Math.Max(1, (int)Math.Ceiling(windowSize * 0.8d)))
                        result.Add(CreateRawRmsWindowPoint(samples, start, end, sumSq, validCount));
                }

                return result;
            }

            // 低采样场景：退化为局部均方根近似。
            // 这里不能严格按周波做 RMS，只能在当前点附近做平滑 RMS 近似。
            int smoothWindow = samplesPerCycle <= 1 ? 3 : Math.Max(3, samplesPerCycle);
            int half = smoothWindow / 2;

            for (int i = segStart; i <= segEnd; i++)
            {
                double sumSq = 0d;
                int validCount = 0;

                int ws = Math.Max(segStart, i - half);
                int we = Math.Min(segEnd, i + half);

                for (int j = ws; j <= we; j++)
                {
                    if (!values[j].HasValue)
                        continue;

                    double v = values[j].Value;
                    if (!IsUsableAnalogValue(v))
                        continue;

                    sumSq += v * v;
                    validCount++;
                }

                if (validCount == 0)
                    continue;

                decimal rms = SafeMagnitudeFromSquareAverage(sumSq, validCount);

                result.Add(new RawRmsWindowPoint
                {
                    SampleNo = samples[i].SampleNo,
                    TimeMs = samples[i].TimeMs,
                    WindowStartMs = samples[ws].TimeMs,
                    WindowEndMs = samples[we].TimeMs,
                    Rms = rms
                });
            }

            return result;
        }

        /// <summary>
        /// 按采样间隔自动分段。
        ///
        /// 当录波文件在不同阶段使用了不同采样间隔时，
        /// 必须按段拆开处理，否则：
        /// - 周波点数估算会错；
        /// - RMS 窗口大小会错；
        /// - 结果会明显失真。
        /// </summary>
        private static List<ZwavSampleSegmentInfo> SplitBySampleInterval(
            IReadOnlyList<ZwavSagSamplePoint> samples,
            decimal frequencyHz)
        {
            var result = new List<ZwavSampleSegmentInfo>();
            if (samples == null || samples.Count < 2)
                return result;

            double cycleMs = 1000d / (double)frequencyHz;

            int segStart = 0;
            double prevDiff = SafePositiveDiff(samples[1].TimeMs - samples[0].TimeMs);

            if (prevDiff <= 0d)
                prevDiff = cycleMs / 24d;

            for (int i = 2; i < samples.Count; i++)
            {
                double diff = SafePositiveDiff(samples[i].TimeMs - samples[i - 1].TimeMs);
                if (diff <= 0d)
                    continue;

                bool changed =
                    diff > prevDiff * 1.8d ||
                    diff < prevDiff / 1.8d ||
                    Math.Abs(diff - prevDiff) > Math.Max(0.2d, prevDiff * 0.35d);

                if (changed)
                {
                    result.Add(new ZwavSampleSegmentInfo
                    {
                        StartIndex = segStart,
                        EndIndex = i - 1,
                        SampleIntervalMs = prevDiff
                    });

                    segStart = i - 1;
                    prevDiff = diff;
                }
                else
                {
                    // 平滑更新当前估计采样间隔。
                    prevDiff = (prevDiff + diff) / 2d;
                }
            }

            result.Add(new ZwavSampleSegmentInfo
            {
                StartIndex = segStart,
                EndIndex = samples.Count - 1,
                SampleIntervalMs = prevDiff
            });

            return result.Where(x => x.EndIndex > x.StartIndex).ToList();
        }

        /// <summary>
        /// 获取可分析分段。
        /// 这里会过滤掉长度过短的段，因为过短段不具备可靠的 RMS 计算意义。
        /// </summary>
        private static List<ZwavSampleSegmentInfo> GetAnalyzableSegments(
            IReadOnlyList<ZwavSagSamplePoint> samples,
            decimal frequencyHz)
        {
            var segments = SplitBySampleInterval(samples, frequencyHz);
            var result = new List<ZwavSampleSegmentInfo>();

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg == null)
                    continue;

                int len = seg.EndIndex - seg.StartIndex + 1;
                if (len < 3)
                    continue;

                result.Add(seg);
            }

            return result;
        }

        /// <summary>
        /// 根据给定窗口创建一个 RMS 点。
        ///
        /// 时间采用：
        /// - WindowStartMs：窗口起点时间；
        /// - WindowEndMs：窗口终点时间；
        /// - TimeMs：窗口中心时间；
        ///
        /// SampleNo 也取中心点的样本号，便于和前端/原始序列对应。
        /// </summary>
        private static RawRmsWindowPoint CreateRawRmsWindowPoint(
            IReadOnlyList<ZwavSagSamplePoint> samples,
            int start,
            int end,
            double sumSq,
            int validCount)
        {
            decimal rms = SafeMagnitudeFromSquareAverage(sumSq, validCount);

            int centerIndex = start + ((end - start) / 2);
            if (centerIndex < start) centerIndex = start;
            if (centerIndex > end) centerIndex = end;

            double windowStartMs = samples[start].TimeMs;
            double windowEndMs = samples[end].TimeMs;
            double centerTimeMs = (windowStartMs + windowEndMs) / 2d;

            return new RawRmsWindowPoint
            {
                SampleNo = samples[centerIndex].SampleNo,
                TimeMs = centerTimeMs,
                WindowStartMs = windowStartMs,
                WindowEndMs = windowEndMs,
                Rms = rms
            };
        }

        /// <summary>
        /// 把标准化后的 RMS 序列追加到最终结果中。
        ///
        /// 这里不做过滤，尽可能全量保留，用于：
        /// - 前端完整绘图；
        /// - 后续排查算法；
        /// - 分析报告可追溯。
        /// </summary>
        private static void AppendRmsPointResults(List<ChannelRmsSeries> seriesList, List<ZwavSagRmsPointResult> target)
        {
            int total = 0;
            for (int i = 0; i < seriesList.Count; i++)
                total += seriesList[i].Points.Count;

            if (total == 0)
                return;

            if (target.Capacity < target.Count + total)
                target.Capacity = target.Count + total;

            for (int i = 0; i < seriesList.Count; i++)
            {
                var series = seriesList[i];
                for (int j = 0; j < series.Points.Count; j++)
                {
                    var p = series.Points[j];
                    target.Add(new ZwavSagRmsPointResult
                    {
                        ChannelIndex = series.ChannelIndex,
                        GroupName = NormalizeGroupName(series.GroupName),
                        ChannelName = NormalizeChannelName(series.ChannelName, series.ChannelIndex),
                        Phase = NormalizePhase(series.Phase),
                        SampleNo = p.SampleNo,
                        TimeMs = p.TimeMs,
                        Rms = p.Rms,
                        RmsPct = p.RmsPct,
                        ReferenceVoltage = p.OutputReferenceVoltage > 0m
                            ? p.OutputReferenceVoltage
                            : series.ReferenceVoltage,
                        SeqNo = j + 1
                    });
                }
            }
        }

        #endregion

        #region 参考值与通道可用性

        /// <summary>
        /// 解析通道参考电压。
        ///
        /// 优先顺序：
        /// 1）先根据通道自身数据推断出 inferredReference；
        /// 2）如果前端传入 forcedReferenceVoltage 且量级兼容，则采用外部值；
        /// 3）否则使用 inferredReference。
        ///
        /// 这样可以避免外部统一参考值把不同电压等级的通道“拉偏”。
        /// </summary>
        private static decimal ResolveChannelReferenceVoltage(
            ZwavSagAnalyzeContext context,
            ChannelRawRmsSeries rawSeries,
            decimal? forcedReferenceVoltage)
        {
            if (rawSeries?.Points == null || rawSeries.Points.Count == 0)
                return 0m;

            // 先从通道自身推断参考值，作为真实量级基准。
            decimal inferredReference = InferChannelReferenceVoltage(rawSeries);
            if (inferredReference <= 0m)
                return 0m;

            if (forcedReferenceVoltage.HasValue && forcedReferenceVoltage.Value > 0m)
            {
                decimal forced = decimal.Round(forcedReferenceVoltage.Value, 6);

                // 只有外部参考值与该通道的量级接近时才允许采用。
                if (IsCompatibleDeclaredReference(forced, inferredReference))
                    return forced;
            }

            return inferredReference;
        }

        /// <summary>
        /// 从通道自身 RMS 序列中推断参考电压。
        ///
        /// 思路：
        /// - 优先看前 30% 区域，因为那里更可能是故障前稳态；
        /// - 再优先取偏高且稳定的部分；
        /// - 若仍不够，再退回全局高分位范围；
        /// - 最后兜底用中位数。
        /// </summary>
        private static decimal InferChannelReferenceVoltage(ChannelRawRmsSeries rawSeries)
        {
            if (rawSeries?.Points == null || rawSeries.Points.Count == 0)
                return 0m;

            var all = rawSeries.Points
                .Select(x => x.Rms)
                .Where(x => x > 0m)
                .OrderBy(x => x)
                .ToList();

            if (all.Count == 0)
                return 0m;

            if (all.Count < 8)
                return decimal.Round(Median(all), 6);

            int headTake = Math.Max(8, (int)Math.Ceiling(rawSeries.Points.Count * 0.30d));
            var headRaw = rawSeries.Points
                .Take(headTake)
                .Select(x => x.Rms)
                .Where(x => x > 0m)
                .ToList();

            if (headRaw.Count >= 6)
            {
                var stableHead = SelectStableHighValues(headRaw);
                if (stableHead.Count >= 4)
                {
                    stableHead.Sort();
                    var v = MedianOfPercentileRange(stableHead, 0.50d, 0.90d);
                    if (v > 0m)
                        return decimal.Round(v, 6);
                }

                headRaw.Sort();
                var headRef = MedianOfPercentileRange(headRaw, 0.70d, 0.95d);
                if (headRef > 0m)
                    return decimal.Round(headRef, 6);
            }

            decimal allRef = MedianOfPercentileRange(all, 0.78d, 0.96d);
            if (allRef > 0m)
                return decimal.Round(allRef, 6);

            return decimal.Round(Median(all), 6);
        }

        /// <summary>
        /// 判断外部声明参考值与通道推断参考值是否量级兼容。
        ///
        /// 兼容条件：
        /// - 最大/最小比值不能太离谱；
        /// - 相对差异不能太大。
        ///
        /// 用于防止把高压侧参考误套到中压/低压侧。
        /// </summary>
        private static bool IsCompatibleDeclaredReference(decimal forcedReference, decimal inferredReference)
        {
            if (forcedReference <= 0m || inferredReference <= 0m)
                return false;

            decimal max = Math.Max(forcedReference, inferredReference);
            decimal min = Math.Min(forcedReference, inferredReference);
            decimal ratio = min <= 0m ? decimal.MaxValue : max / min;
            decimal diffPct = inferredReference <= 0m
                ? decimal.MaxValue
                : Math.Abs(forcedReference - inferredReference) / inferredReference;

            return ratio <= 2.2m && diffPct <= 0.55m;
        }

        /// <summary>
        /// 是否使用滑动参考。
        ///
        /// 规则：
        /// - ReferenceType == Declared：固定参考；
        /// - 其他情况：滑动参考。
        /// </summary>
        private static bool UseSlidingReference(ZwavSagAnalyzeContext context)
        {
            return !string.Equals(
                (context?.ReferenceType ?? string.Empty).Trim(),
                "Declared",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 构建检测参考值序列。
        ///
        /// 固定参考模式：
        /// - 全部点使用 anchorReferenceVoltage。
        ///
        /// 滑动参考模式：
        /// - 每个点都根据前序局部窗口估算候选参考值；
        /// - 允许慢速跟踪稳态漂移；
        /// - 但禁止参考值在故障期跟着一起塌陷。
        /// </summary>
        private static decimal[] BuildDetectReferenceSeries(
            List<RawRmsWindowPoint> points,
            decimal anchorReferenceVoltage,
            bool useSlidingReference)
        {
            int count = points?.Count ?? 0;
            var result = new decimal[count];
            if (count == 0)
                return result;

            decimal currentReference = anchorReferenceVoltage;

            for (int i = 0; i < count; i++)
            {
                decimal detectReference = anchorReferenceVoltage;

                if (useSlidingReference)
                {
                    // 估算当前点之前局部窗口的候选参考值。
                    decimal candidate = ResolveRollingReferenceCandidate(points, i, currentReference > 0m ? currentReference : anchorReferenceVoltage);
                    if (candidate <= 0m)
                        candidate = currentReference > 0m ? currentReference : anchorReferenceVoltage;

                    // 不允许检测参考值突然大幅下跌，否则故障时参考值会被一起拉低，
                    // 导致 DetectPct 被“掩盖”，从而漏检。
                    if (currentReference > 0m && candidate > 0m)
                    {
                        decimal maxDrop = Math.Max(0.3m, currentReference * 0.005m);
                        decimal minAllowed = currentReference - maxDrop;
                        if (candidate < minAllowed)
                            candidate = minAllowed;
                    }

                    if (candidate > 0m)
                        currentReference = decimal.Round(candidate, 6);

                    detectReference = currentReference > 0m ? currentReference : anchorReferenceVoltage;
                }

                // 如果参考值无效，则用当前 RMS 自身兜底。
                if (detectReference <= 0m && points[i].Rms > 0m)
                    detectReference = points[i].Rms;

                result[i] = decimal.Round(detectReference, 6);
            }

            return result;
        }

        /// <summary>
        /// 解析滑动参考候选值。
        ///
        /// 从当前点之前最多 24 个点中提取 RMS：
        /// - 若点数太少，直接取中位数或 fallback；
        /// - 若点数足够，则优先选“偏高且稳定”的局部稳态；
        /// - 再退回高分位范围；
        /// - 最后兜底取中位数。
        /// </summary>
        private static decimal ResolveRollingReferenceCandidate(
            List<RawRmsWindowPoint> points,
            int index,
            decimal fallbackReference)
        {
            if (points == null || points.Count == 0)
                return fallbackReference;

            int start = Math.Max(0, index - 24);
            var values = new List<decimal>();

            for (int i = start; i < index; i++)
            {
                decimal rms = points[i].Rms;
                if (rms > 0m)
                    values.Add(rms);
            }

            if (values.Count == 0)
                return fallbackReference;

            values.Sort();

            if (values.Count < 4)
                return decimal.Round(Math.Max(fallbackReference, Median(values)), 6);

            var stable = SelectStableHighValues(values);
            if (stable.Count >= 4)
            {
                stable.Sort();
                decimal stableRef = MedianOfPercentileRange(stable, 0.55d, 0.90d);
                if (stableRef > 0m)
                    return decimal.Round(stableRef, 6);
            }

            decimal rollingRef = MedianOfPercentileRange(values, 0.70d, 0.95d);
            if (rollingRef > 0m)
                return decimal.Round(rollingRef, 6);

            return decimal.Round(Math.Max(fallbackReference, Median(values)), 6);
        }

        /// <summary>
        /// 解析事件级参考电压。
        ///
        /// 规则：
        /// 1）如果上下文强制传入了 ReferenceVoltage，则直接用；
        /// 2）若不是滑动参考模式，则使用通道统一参考值；
        /// 3）若是滑动参考模式，则优先取故障前局部稳态值；
        ///
        /// 这样做的好处是：
        /// 同一通道不同时间段若有慢漂移，事件级参考更贴近故障前真实电平。
        /// </summary>
        private static decimal ResolveEventReferenceVoltage(
            ZwavSagAnalyzeContext context,
            ChannelRmsSeries series,
            int startIndex,
            int minIndex)
        {
            if (context?.ReferenceVoltage.HasValue == true && context.ReferenceVoltage.Value > 0m)
                return decimal.Round(context.ReferenceVoltage.Value, 6);

            if (series == null || series.Points == null || series.Points.Count == 0)
                return 0m;

            if (!series.UseSlidingReference)
                return series.ReferenceVoltage;

            var candidates = new List<decimal>();
            int start = Math.Max(0, startIndex - 12);
            decimal stableBoundary = ResolveRecoverThresholdPct(context);

            // 优先收集故障前且处于稳定区的点。
            for (int i = start; i < startIndex; i++)
            {
                var p = series.Points[i];
                if (p.Rms > 0m && p.DetectPct >= stableBoundary)
                    candidates.Add(p.Rms);
            }

            // 若稳定点太少，则放宽为故障前局部所有正值点。
            if (candidates.Count < 4)
            {
                for (int i = Math.Max(0, minIndex - 16); i < startIndex; i++)
                {
                    var p = series.Points[i];
                    if (p.Rms > 0m)
                        candidates.Add(p.Rms);
                }
            }

            if (candidates.Count == 0)
                return series.Points[minIndex].DetectReferenceVoltage > 0m
                    ? series.Points[minIndex].DetectReferenceVoltage
                    : series.ReferenceVoltage;

            candidates.Sort();
            var stableCandidates = SelectStableHighValues(candidates);
            if (stableCandidates.Count >= 4)
            {
                stableCandidates.Sort();
                decimal localRef = MedianOfPercentileRange(stableCandidates, 0.55d, 0.90d);
                if (localRef > 0m)
                    return decimal.Round(localRef, 6);
            }

            decimal fallback = MedianOfPercentileRange(candidates, 0.60d, 0.95d);
            return fallback > 0m
                ? decimal.Round(fallback, 6)
                : series.ReferenceVoltage;
        }

        /// <summary>
        /// 估算事件起始时间。
        ///
        /// 不是直接用窗口中心点，而是在：
        /// - 前一个正常点
        /// - 当前故障点
        /// 之间做阈值交点插值。
        ///
        /// 这样得到的起点更接近真实故障发生时间。
        /// </summary>
        private static double EstimateEventStartMs(
            ZwavSagAnalyzeContext context,
            ChannelRmsSeries series,
            int startIndex)
        {
            if (series?.Points == null || startIndex < 0 || startIndex >= series.Points.Count)
                return 0d;

            var current = series.Points[startIndex];
            if (startIndex == 0)
                return current.WindowStartMs;

            var previous = series.Points[startIndex - 1];

            double interpolated = InterpolateCrossingTime(
                previous.TimeMs,
                previous.DetectPct,
                current.TimeMs,
                current.DetectPct,
                context.SagThresholdPct);

            return Clamp(interpolated, current.WindowStartMs, current.TimeMs);
        }

        /// <summary>
        /// 估算事件结束时间。
        ///
        /// 取“最后一个故障点”和“后一个恢复点”之间的恢复阈值交点插值。
        /// 这样得到的结束时间比直接用窗口中心点更贴近真实恢复边界。
        /// </summary>
        private static double EstimateEventEndMs(
            ZwavSagAnalyzeContext context,
            ChannelRmsSeries series,
            int endIndex)
        {
            if (series?.Points == null || endIndex < 0 || endIndex >= series.Points.Count)
                return 0d;

            var current = series.Points[endIndex];
            int nextIndex = Math.Min(series.Points.Count - 1, endIndex + 1);
            if (nextIndex == endIndex)
                return current.WindowEndMs;

            var next = series.Points[nextIndex];
            decimal recoverThreshold = ResolveRecoverThresholdPct(context);

            double interpolated = InterpolateCrossingTime(
                current.TimeMs,
                Math.Max(current.DetectPct, current.RmsPct),
                next.TimeMs,
                Math.Max(next.DetectPct, next.RmsPct),
                recoverThreshold);

            return Clamp(interpolated, current.TimeMs, next.WindowEndMs);
        }

        /// <summary>
        /// 线性插值计算两个点之间到达 targetValue 的时间。
        /// </summary>
        private static double InterpolateCrossingTime(
            double startMs,
            decimal startValue,
            double endMs,
            decimal endValue,
            decimal targetValue)
        {
            if (endMs <= startMs)
                return endMs;

            decimal delta = endValue - startValue;
            if (delta == 0m)
                return endMs;

            double ratio = (double)((targetValue - startValue) / delta);
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
                return endMs;

            return startMs + ((endMs - startMs) * ratio);
        }

        /// <summary>
        /// 从一批值中选出“偏高且稳定”的部分。
        ///
        /// 用途：
        /// 参考值不应该从低谷点里取，因此优先取中位数附近偏上的稳定区。
        /// </summary>
        private static List<decimal> SelectStableHighValues(List<decimal> values)
        {
            var result = new List<decimal>();
            if (values == null || values.Count == 0)
                return result;

            decimal localMedian = Median(values.OrderBy(x => x).ToList());
            decimal tolerance = Math.Max(0.5m, localMedian * 0.08m);

            for (int i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (v >= localMedian - tolerance)
                    result.Add(v);
            }

            return result;
        }

        /// <summary>
        /// 取有序值列表在某个分位范围内的中位数。
        /// 例如 70%~95% 区间，用于提取偏高稳态区域。
        /// </summary>
        private static decimal MedianOfPercentileRange(List<decimal> sortedValues, double fromPct, double toPct)
        {
            if (sortedValues == null || sortedValues.Count == 0)
                return 0m;

            int n = sortedValues.Count;
            int start = (int)Math.Floor(n * fromPct);
            int end = (int)Math.Floor(n * toPct);

            if (start >= n) start = n - 1;
            if (end < start) end = start;
            if (end >= n) end = n - 1;

            var slice = sortedValues.GetRange(start, end - start + 1);
            return Median(slice);
        }

        /// <summary>
        /// 判断通道序列是否可用于暂降分析。
        ///
        /// 宽松判断标准：
        /// - 至少有 3 个点；
        /// - 参考值有效；
        /// - 正值点比例不能太低；
        /// - 最大 RMS 不能长期过低；
        ///
        /// 目的是过滤掉明显无效或不具备分析意义的序列。
        /// </summary>
        private static bool IsUsableVoltageSeriesRelaxed(ChannelRmsSeries series)
        {
            if (series == null || series.Points == null || series.Points.Count < 3)
                return false;

            if (series.ReferenceVoltage <= 0.0001m)
                return false;

            int validCount = 0;
            int positiveCount = 0;
            decimal maxRms = 0m;

            foreach (var p in series.Points)
            {
                validCount++;
                if (p.Rms > 0m)
                    positiveCount++;

                if (p.Rms > maxRms)
                    maxRms = p.Rms;
            }

            if (validCount == 0)
                return false;

            decimal positiveRatio = (decimal)positiveCount / validCount;
            if (positiveRatio < 0.5m)
                return false;

            // 全程幅值很低的通道，继续分析往往只会制造整段误判。
            if (maxRms < Math.Max(1m, series.ReferenceVoltage * 0.05m))
                return false;

            return true;
        }

        #endregion

        #region 参数校验

        /// <summary>
        /// 分析前的上下文校验。
        /// 尽量在入口阶段把参数错误拦住，避免后续在算法深处抛异常。
        /// </summary>
        private static void ValidateContext(ZwavSagAnalyzeContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Samples == null || context.Samples.Count == 0)
                throw new ArgumentException("采样数据为空", nameof(context));

            if (context.VoltageChannels == null || context.VoltageChannels.Length == 0)
                throw new ArgumentException("电压通道为空", nameof(context));

            if (context.FrequencyHz <= 0)
                throw new ArgumentException("系统频率必须大于0", nameof(context));

            if (context.SagThresholdPct <= 0 || context.SagThresholdPct > 100)
                throw new ArgumentException("暂降阈值必须在0~100之间", nameof(context));

            if (context.RecoverThresholdPct.HasValue)
            {
                if (context.RecoverThresholdPct.Value <= 0 || context.RecoverThresholdPct.Value > 100)
                    throw new ArgumentException("恢复阈值必须在0~100之间", nameof(context));

                if (context.RecoverThresholdPct.Value < context.SagThresholdPct)
                    throw new ArgumentException("恢复阈值不能小于暂降阈值", nameof(context));
            }

            if (context.InterruptThresholdPct < 0 || context.InterruptThresholdPct > 100)
                throw new ArgumentException("中断阈值必须在0~100之间", nameof(context));

            if (context.HysteresisPct < 0 || context.HysteresisPct > 100)
                throw new ArgumentException("迟滞阈值必须在0~100之间", nameof(context));

            if (context.MinDurationMs < 0)
                throw new ArgumentException("最小时长不能小于0", nameof(context));
        }

        #endregion

        #region 排序/名称/分组辅助

        /// <summary>
        /// 对事件结果排序：
        /// 组 -> 相序 -> 开始时间 -> 通道顺序 -> 通道索引。
        /// </summary>
        private static void SortEvents(List<ZwavSagEventResult> events)
        {
            if (events == null || events.Count <= 1)
                return;

            events.Sort((a, b) =>
            {
                int c = GroupSortOrder(a?.GroupName).CompareTo(GroupSortOrder(b?.GroupName));
                if (c != 0) return c;

                c = PhaseSortOrder(a?.TriggerPhase).CompareTo(PhaseSortOrder(b?.TriggerPhase));
                if (c != 0) return c;

                c = (a?.StartTimeUtc ?? default).CompareTo(b?.StartTimeUtc ?? default);
                if (c != 0) return c;

                c = ChannelSortOrder(a?.ChannelName, a?.ChannelIndex ?? 0)
                    .CompareTo(ChannelSortOrder(b?.ChannelName, b?.ChannelIndex ?? 0));
                if (c != 0) return c;

                return (a?.ChannelIndex ?? 0).CompareTo(b?.ChannelIndex ?? 0);
            });

            // 同时保证每个事件内部的 phases 顺序也稳定。
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt?.Phases == null || evt.Phases.Count <= 1)
                    continue;

                evt.Phases = evt.Phases
                    .OrderBy(x => GroupSortOrder(x?.GroupName))
                    .ThenBy(x => PhaseSortOrder(x?.Phase))
                    .ThenBy(x => x?.StartTimeUtc ?? default)
                    .ThenBy(x => x?.ChannelIndex ?? 0)
                    .ToList();
            }
        }

        /// <summary>
        /// 对 RMS 点排序：
        /// 组 -> 相序 -> 通道 -> 时间 -> SampleNo。
        /// </summary>
        private static void SortRmsPoints(List<ZwavSagRmsPointResult> points)
        {
            if (points == null || points.Count <= 1)
                return;

            points.Sort((a, b) =>
            {
                int c = GroupSortOrder(a?.GroupName).CompareTo(GroupSortOrder(b?.GroupName));
                if (c != 0) return c;

                c = PhaseSortOrder(a?.Phase).CompareTo(PhaseSortOrder(b?.Phase));
                if (c != 0) return c;

                c = ChannelSortOrder(a?.ChannelName, a?.ChannelIndex ?? 0)
                    .CompareTo(ChannelSortOrder(b?.ChannelName, b?.ChannelIndex ?? 0));
                if (c != 0) return c;

                c = (a?.TimeMs ?? 0d).CompareTo(b?.TimeMs ?? 0d);
                if (c != 0) return c;

                return (a?.SampleNo ?? 0).CompareTo(b?.SampleNo ?? 0);
            });
        }

        /// <summary>
        /// 组排序优先级：
        /// 高压 -> 中压 -> 低压 -> 其他。
        /// </summary>
        private static int GroupSortOrder(string groupName)
        {
            string g = (groupName ?? string.Empty).Trim();

            if (ContainsAny(g, "高压", "高压侧", "HV", "110kV", "35kV"))
                return 1;

            if (ContainsAny(g, "中压", "中压侧", "MV", "10kV", "6kV"))
                return 2;

            if (ContainsAny(g, "低压", "低压侧", "LV", "0.4kV", "400V"))
                return 3;

            return 99;
        }

        /// <summary>
        /// 相序排序优先级。
        /// </summary>
        private static int PhaseSortOrder(string phase)
        {
            switch ((phase ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": return 1;
                case "B": return 2;
                case "C": return 3;
                case "AB": return 4;
                case "BC": return 5;
                case "CA": return 6;
                default: return 99;
            }
        }

        /// <summary>
        /// 通道排序优先级。
        /// 尽量把 A/B/C 相按人类习惯顺序排列。
        /// </summary>
        private static int ChannelSortOrder(string channelName, int channelIndex)
        {
            string name = (channelName ?? string.Empty).Trim().ToUpperInvariant();

            if (name.Contains("A相") || name.EndsWith("A") || name.Contains(" PHASE A") || name.Contains("_A"))
                return 1000 + channelIndex;

            if (name.Contains("B相") || name.EndsWith("B") || name.Contains(" PHASE B") || name.Contains("_B"))
                return 2000 + channelIndex;

            if (name.Contains("C相") || name.EndsWith("C") || name.Contains(" PHASE C") || name.Contains("_C"))
                return 3000 + channelIndex;

            return 9000 + channelIndex;
        }

        /// <summary>
        /// 标准化组名，空值时返回“未分组”。
        /// </summary>
        private static string NormalizeGroupName(string groupName)
        {
            return string.IsNullOrWhiteSpace(groupName) ? "未分组" : groupName.Trim();
        }

        /// <summary>
        /// 标准化通道名，空值时按 CH{index} 兜底。
        /// </summary>
        private static string NormalizeChannelName(string channelName, int channelIndex)
        {
            return string.IsNullOrWhiteSpace(channelName)
                ? $"CH{channelIndex}"
                : channelName.Trim();
        }

        /// <summary>
        /// 标准化相序名，统一转大写。
        /// </summary>
        private static string NormalizePhase(string phase)
        {
            return string.IsNullOrWhiteSpace(phase)
                ? string.Empty
                : phase.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// 判断字符串中是否包含任一关键字。
        /// </summary>
        private static bool ContainsAny(string source, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(source) || keywords == null || keywords.Length == 0)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                var k = keywords[i];
                if (!string.IsNullOrWhiteSpace(k) &&
                    source.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 解析恢复阈值。
        ///
        /// 规则：
        /// 1）如果前端/请求显式传入 RecoverThresholdPct，则优先使用；
        /// 2）否则退回 SagThresholdPct + HysteresisPct；
        /// 3）最终保证结果位于 [SagThresholdPct, 100] 范围内。
        ///
        /// 这是你前端“恢复阈值可配置”需求对应的关键接入点。
        /// </summary>
        private static decimal ResolveRecoverThresholdPct(ZwavSagAnalyzeContext context)
        {
            if (context == null)
                return 100m;

            decimal recoverThreshold;

            if (context.RecoverThresholdPct.HasValue && context.RecoverThresholdPct.Value > 0m)
            {
                recoverThreshold = context.RecoverThresholdPct.Value;
            }
            else
            {
                recoverThreshold = context.SagThresholdPct + context.HysteresisPct;
            }

            if (recoverThreshold > 100m)
                recoverThreshold = 100m;

            if (recoverThreshold < context.SagThresholdPct)
                recoverThreshold = context.SagThresholdPct;

            return decimal.Round(recoverThreshold, 3);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 解析通道相序名称。
        ///
        /// 优先级：
        /// 1）如果 channel.Phase 已经是标准相序（A/B/C/AB/BC/CA），直接用；
        /// 2）否则尝试根据规则匹配器从通道名称/编码/单位中识别；
        /// 3）再不行则退回 CH{index}。
        /// </summary>
        private static string ResolvePhaseName(
            ZwavVoltageChannelContext channel,
            Preferred.Api.Services.ZwavSagVoltageChannelRuleMatcher.RuleItem[] phaseRules)
        {
            var raw = (channel?.Phase ?? string.Empty).Trim().ToUpperInvariant();

            switch (raw)
            {
                case "A":
                case "B":
                case "C":
                case "AB":
                case "BC":
                case "CA":
                    return raw;
                default:
                    var matched = Preferred.Api.Services.ZwavSagVoltageChannelRuleMatcher.MatchPhase(
                        channel?.ChannelName,
                        channel?.ChannelCode,
                        channel?.Unit,
                        phaseRules);

                    if (!string.IsNullOrWhiteSpace(matched))
                        return matched;

                    return $"CH{channel?.ChannelIndex ?? 0}";
            }
        }

        /// <summary>
        /// 获取安全正差值。
        /// 非法值一律按 0 处理。
        /// </summary>
        private static double SafePositiveDiff(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value)
                ? value
                : 0d;
        }

        /// <summary>
        /// 把数值限制在 [min, max] 范围内。
        /// 非法值时返回 min。
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        /// <summary>
        /// 生成安全时长值。
        /// 非法或负值时返回 0。
        /// 超过上限时裁到 MaxSupportedDurationMs。
        /// </summary>
        private static decimal SafeDuration(double durationMs)
        {
            if (durationMs <= 0d || double.IsNaN(durationMs) || double.IsInfinity(durationMs))
                return 0m;

            if (durationMs > (double)MaxSupportedDurationMs)
                return MaxSupportedDurationMs;

            return decimal.Round((decimal)durationMs, 3);
        }

        /// <summary>
        /// 判断模拟量是否可参与计算。
        /// 排除 NaN/Infinity/极端异常值。
        /// </summary>
        private static bool IsUsableAnalogValue(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && Math.Abs(value) <= MaxSupportedAnalogMagnitude;
        }

        /// <summary>
        /// 根据平方和与有效点数，计算 RMS。
        /// 内部做了多层安全防护，防止出现异常值、NaN、Infinity、decimal 溢出。
        /// </summary>
        private static decimal SafeMagnitudeFromSquareAverage(double sumSq, int validCount)
        {
            if (validCount <= 0 || sumSq <= 0d || double.IsNaN(sumSq) || double.IsInfinity(sumSq))
                return 0m;

            double meanSquare = sumSq / validCount;
            if (meanSquare <= 0d || double.IsNaN(meanSquare) || double.IsInfinity(meanSquare))
                return 0m;

            double rms = Math.Sqrt(meanSquare);
            if (double.IsNaN(rms) || double.IsInfinity(rms))
                return 0m;

            if (rms > (double)MaxSupportedDecimalMagnitude)
                rms = (double)MaxSupportedDecimalMagnitude;

            return decimal.Round((decimal)rms, 6);
        }

        /// <summary>
        /// 把相对毫秒时间转换为绝对 UTC 时间。
        /// </summary>
        private static DateTime ConvertToUtc(ZwavSagAnalyzeContext context, double timeMs)
        {
            return context.WaveStartTimeUtc.AddMilliseconds(timeMs);
        }

        /// <summary>
        /// 计算中位数。
        /// 要求输入最好为已排序列表；未排序也能算，但语义上建议传有序值。
        /// </summary>
        private static decimal Median(List<decimal> sortedValues)
        {
            if (sortedValues == null || sortedValues.Count == 0)
                return 0m;

            int n = sortedValues.Count;
            int mid = n / 2;
            return n % 2 == 1
                ? sortedValues[mid]
                : (sortedValues[mid - 1] + sortedValues[mid]) / 2m;
        }

        #endregion

        #region 内部类

        /// <summary>
        /// 单通道事件窗口内部结构。
        /// 对应一段已经识别出的暂降区间。
        /// </summary>
        private sealed class ChannelEventWindow
        {
            /// <summary>通道索引。</summary>
            public int ChannelIndex { get; set; }

            /// <summary>分组名，例如高压侧/中压侧/低压侧。</summary>
            public string GroupName { get; set; }

            /// <summary>通道名称。</summary>
            public string ChannelName { get; set; }

            /// <summary>相序。</summary>
            public string Phase { get; set; }

            /// <summary>事件开始时间（相对 ms）。</summary>
            public double StartMs { get; set; }

            /// <summary>事件结束时间（相对 ms）。</summary>
            public double EndMs { get; set; }

            /// <summary>时长 ms。</summary>
            public decimal DurationMs { get; set; }

            /// <summary>参考电压。</summary>
            public decimal ReferenceVoltage { get; set; }

            /// <summary>残余电压。</summary>
            public decimal ResidualVoltage { get; set; }

            /// <summary>残余电压百分比。</summary>
            public decimal ResidualVoltagePct { get; set; }

            /// <summary>暂降深度（参考值 - 残余值）。</summary>
            public decimal SagDepth { get; set; }

            /// <summary>暂降百分比（100 - 残余百分比）。</summary>
            public decimal SagPercent { get; set; }

            /// <summary>故障最严重点在 RMS 序列中的索引。</summary>
            public int MinPointIndex { get; set; }
        }

        /// <summary>
        /// 单通道原始 RMS 序列。
        /// 此时还未生成 DetectPct / RmsPct。
        /// </summary>
        private sealed class ChannelRawRmsSeries
        {
            /// <summary>通道索引。</summary>
            public int ChannelIndex { get; set; }

            /// <summary>分组名。</summary>
            public string GroupName { get; set; }

            /// <summary>相序。</summary>
            public string Phase { get; set; }

            /// <summary>通道名称。</summary>
            public string ChannelName { get; set; }

            /// <summary>原始 RMS 点集合。</summary>
            public List<RawRmsWindowPoint> Points { get; set; } = new List<RawRmsWindowPoint>();
        }

        /// <summary>
        /// 标准化后的通道 RMS 序列。
        /// 在原始 RMS 基础上增加：
        /// - ReferenceVoltage
        /// - DetectPct
        /// - RmsPct
        /// - DetectReferenceVoltage
        /// - OutputReferenceVoltage
        /// </summary>
        private sealed class ChannelRmsSeries
        {
            /// <summary>通道索引。</summary>
            public int ChannelIndex { get; set; }

            /// <summary>分组名。</summary>
            public string GroupName { get; set; }

            /// <summary>相序。</summary>
            public string Phase { get; set; }

            /// <summary>通道名称。</summary>
            public string ChannelName { get; set; }

            /// <summary>通道锚定参考电压。</summary>
            public decimal ReferenceVoltage { get; set; }

            /// <summary>是否使用滑动参考。</summary>
            public bool UseSlidingReference { get; set; }

            /// <summary>标准化后的 RMS 点集合。</summary>
            public List<RmsWindowPoint> Points { get; } = new List<RmsWindowPoint>();
        }

        /// <summary>
        /// 原始 RMS 点。
        /// </summary>
        private sealed class RawRmsWindowPoint
        {
            /// <summary>窗口中心点对应的样本号。</summary>
            public int SampleNo { get; set; }

            /// <summary>窗口中心时间（ms）。</summary>
            public double TimeMs { get; set; }

            /// <summary>窗口起始时间（ms）。</summary>
            public double WindowStartMs { get; set; }

            /// <summary>窗口结束时间（ms）。</summary>
            public double WindowEndMs { get; set; }

            /// <summary>窗口 RMS 值。</summary>
            public decimal Rms { get; set; }
        }

        /// <summary>
        /// 标准化 RMS 点。
        /// 在原始 RMS 点基础上，增加各种百分比和参考值信息。
        /// </summary>
        private sealed class RmsWindowPoint
        {
            /// <summary>样本号。</summary>
            public int SampleNo { get; set; }

            /// <summary>中心时间（ms）。</summary>
            public double TimeMs { get; set; }

            /// <summary>窗口起始时间（ms）。</summary>
            public double WindowStartMs { get; set; }

            /// <summary>窗口结束时间（ms）。</summary>
            public double WindowEndMs { get; set; }

            /// <summary>RMS 实际值。</summary>
            public decimal Rms { get; set; }

            /// <summary>输出用 RMS 百分比（相对于锚定参考电压）。</summary>
            public decimal RmsPct { get; set; }

            /// <summary>检测用百分比（相对于检测参考值，可能被平滑）。</summary>
            public decimal DetectPct { get; set; }

            /// <summary>当前点参与检测的参考电压。</summary>
            public decimal DetectReferenceVoltage { get; set; }

            /// <summary>当前点输出到前端/结果中的参考电压。</summary>
            public decimal OutputReferenceVoltage { get; set; }
        }

        #endregion
    }
}