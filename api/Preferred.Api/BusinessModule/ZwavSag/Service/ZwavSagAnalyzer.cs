using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 电压暂降分析器 V1
    /// 目标：
    /// 1. 基于电压通道原始波形计算 RMS 序列
    /// 2. 按阈值检测统一暂降事件窗口
    /// 3. 输出事件汇总和各相明细
    ///
    /// V1 暂不实现：
    /// - 1分钟归并
    /// - 相位跳变
    /// - 起始角
    /// - 真正的滑动参考电压
    /// </summary>
    public class ZwavSagAnalyzer : IZwavSagAnalyzer
    {
        public Task<ZwavSagAnalyzeResult> AnalyzeAsync(ZwavSagAnalyzeContext context)
        {
            ValidateContext(context);

            var result = new ZwavSagAnalyzeResult();

            var seriesList = BuildRmsSeries(context);
            if (seriesList.Count == 0)
                return Task.FromResult(result);

            var alignedRows = AlignRmsSeries(seriesList);
            if (alignedRows.Count == 0)
                return Task.FromResult(result);

            result.RmsPoints.AddRange(
                BuildRmsPointResults(context, seriesList));

            var events = DetectEvents(context, alignedRows);
            if (events.Count > 0)
                result.Events.AddRange(events);
            
            // 如果检测到了事件，但是没有生成结果，可能是阈值设置问题或者数据问题
            // 可以在这里打日志或者断点调试
            
            return Task.FromResult(result);
        }

        private static void ValidateContext(ZwavSagAnalyzeContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Samples == null || context.Samples.Count == 0)
                throw new ArgumentException("采样数据为空", nameof(context));

            if (context.VoltageChannels == null || context.VoltageChannels.Length == 0)
                throw new ArgumentException("电压通道为空", nameof(context));

            if (context.FrequencyHz <= 0)
                throw new ArgumentException("系统频率必须大于 0", nameof(context));

            if (context.SagThresholdPct <= 0 || context.SagThresholdPct > 100)
                throw new ArgumentException("暂降阈值必须在 0~100 之间", nameof(context));

            if (context.InterruptThresholdPct < 0 || context.InterruptThresholdPct > 100)
                throw new ArgumentException("中断阈值必须在 0~100 之间", nameof(context));

            if (context.HysteresisPct < 0 || context.HysteresisPct > 100)
                throw new ArgumentException("迟滞阈值必须在 0~100 之间", nameof(context));

            if (context.MinDurationMs < 0)
                throw new ArgumentException("最小时长不能小于 0", nameof(context));
        }

        private List<ChannelRmsSeries> BuildRmsSeries(ZwavSagAnalyzeContext context)
        {
            var result = new List<ChannelRmsSeries>();

            double avgSampleIntervalMs = EstimateSampleIntervalMs(context.Samples);
            if (avgSampleIntervalMs <= 0)
                throw new InvalidOperationException("无法估算采样间隔");

            double cycleMs = 1000d / (double)context.FrequencyHz;
            double windowMs = cycleMs;
            double stepMs = string.Equals(context.RmsMode, "OneCycle", StringComparison.OrdinalIgnoreCase)
                ? cycleMs
                : cycleMs / 2d;

            int windowSize = Math.Max(1, (int)Math.Round(windowMs / avgSampleIntervalMs));
            int stepSize = Math.Max(1, (int)Math.Round(stepMs / avgSampleIntervalMs));

            foreach (var channel in context.VoltageChannels)
            {
                decimal referenceVoltage = ResolveReferenceVoltage(context, channel.ChannelIndex);
                if (referenceVoltage <= 0)
                    continue;

                var series = new ChannelRmsSeries
                {
                    ChannelIndex = channel.ChannelIndex,
                    Phase = ResolvePhaseName(channel),
                    ReferenceVoltage = referenceVoltage
                };

                for (int start = 0; start + windowSize <= context.Samples.Count; start += stepSize)
                {
                    int end = start + windowSize - 1;

                    double sumSq = 0d;
                    int validCount = 0;

                    for (int i = start; i <= end; i++)
                    {
                        var sample = context.Samples[i];
                        if (sample.ChannelValues == null)
                            continue;

                        if (sample.ChannelValues.TryGetValue(channel.ChannelIndex, out var value) && value.HasValue)
                        {
                            double v = value.Value;
                            sumSq += v * v;
                            validCount++;
                        }
                    }

                    if (validCount == 0)
                        continue;

                    decimal rms = (decimal)Math.Sqrt(sumSq / validCount);
                    decimal rmsRounded = decimal.Round(rms, 6);
                    decimal rmsPct = referenceVoltage <= 0
                        ? 0m
                        : decimal.Round(rmsRounded / referenceVoltage * 100m, 3);

                    series.Points.Add(new RmsPoint
                    {
                        SampleNo = context.Samples[end].SampleNo,
                        TimeMs = context.Samples[end].TimeMs,
                        Rms = rmsRounded,
                        RmsPct = rmsPct
                    });
                }

                if (series.Points.Count > 0)
                    result.Add(series);
            }

            return result;
        }

        private List<ZwavSagRmsPointResult> BuildRmsPointResults(
            ZwavSagAnalyzeContext context,
            List<ChannelRmsSeries> seriesList)
        {
            var result = new List<ZwavSagRmsPointResult>();

            foreach (var series in seriesList)
            {
                for (int i = 0; i < series.Points.Count; i++)
                {
                    var p = series.Points[i];
                    result.Add(new ZwavSagRmsPointResult
                    {
                        ChannelIndex = series.ChannelIndex,
                        Phase = series.Phase,
                        SampleNo = p.SampleNo,
                        TimeMs = p.TimeMs,
                        Rms = p.Rms,
                        RmsPct = p.RmsPct,
                        ReferenceVoltage = series.ReferenceVoltage,
                        SeqNo = i + 1
                    });
                }
            }

            return result;
        }

        private List<AlignedRmsRow> AlignRmsSeries(List<ChannelRmsSeries> seriesList)
        {
            if (seriesList == null || seriesList.Count == 0)
                return new List<AlignedRmsRow>();

            int minCount = seriesList.Min(x => x.Points.Count);
            if (minCount <= 0)
                return new List<AlignedRmsRow>();

            var rows = new List<AlignedRmsRow>(minCount);

            for (int i = 0; i < minCount; i++)
            {
                var row = new AlignedRmsRow
                {
                    TimeMs = seriesList[0].Points[i].TimeMs,
                    SampleNo = seriesList[0].Points[i].SampleNo
                };

                foreach (var series in seriesList)
                {
                    var point = series.Points[i];
                    row.Phases.Add(new AlignedRmsPhaseRow
                    {
                        ChannelIndex = series.ChannelIndex,
                        Phase = series.Phase,
                        Rms = point.Rms,
                        RmsPct = point.RmsPct,
                        ReferenceVoltage = series.ReferenceVoltage
                    });
                }

                rows.Add(row);
            }

            return rows;
        }

        private List<ZwavSagEventResult> DetectEvents(
            ZwavSagAnalyzeContext context,
            List<AlignedRmsRow> alignedRows)
        {
            var results = new List<ZwavSagEventResult>();

            decimal sagThreshold = context.SagThresholdPct;
            decimal recoverThreshold = context.SagThresholdPct + context.HysteresisPct;

            bool inEvent = false;
            int eventStartRow = -1;

            for (int i = 0; i < alignedRows.Count; i++)
            {
                var row = alignedRows[i];

                bool anyBelowSag = row.Phases.Any(p => p.RmsPct < sagThreshold);
                bool allRecovered = row.Phases.All(p => p.RmsPct >= recoverThreshold);

                if (!inEvent && anyBelowSag)
                {
                    inEvent = true;
                    eventStartRow = i;
                    continue;
                }

                if (inEvent && allRecovered)
                {
                    int eventEndRow = i;
                    var evt = BuildEventResult(context, alignedRows, eventStartRow, eventEndRow);

                    if (evt != null && evt.DurationMs >= context.MinDurationMs)
                        results.Add(evt);

                    inEvent = false;
                    eventStartRow = -1;
                }
            }

            if (inEvent && eventStartRow >= 0)
            {
                var evt = BuildEventResult(context, alignedRows, eventStartRow, alignedRows.Count - 1);

                if (evt != null && evt.DurationMs >= context.MinDurationMs)
                    results.Add(evt);
            }

            return results;
        }

        private ZwavSagEventResult BuildEventResult(
            ZwavSagAnalyzeContext context,
            List<AlignedRmsRow> rows,
            int startRow,
            int endRow)
        {
            if (startRow < 0 || endRow < startRow || endRow >= rows.Count)
                return null;

            int windowCount = endRow - startRow + 1;
            if (windowCount <= 0)
                return null;

            var window = rows.Skip(startRow).Take(windowCount).ToList();
            if (window.Count == 0)
                return null;

            double eventStartMs = window.First().TimeMs;
            double eventEndMs = window.Last().TimeMs;
            decimal eventDurationMs = SafeDuration(eventEndMs - eventStartMs);

            var phaseWindows = new List<PhaseWindowInfo>();
            var phaseResults = new List<ZwavSagEventPhaseResult>();

            var phaseNames = window.First().Phases
                .Select(x => x.Phase)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var phaseName in phaseNames)
            {
                var phaseRows = window
                    .Select((row, index) => new
                    {
                        RowIndex = index,
                        TimeMs = row.TimeMs,
                        Data = row.Phases.FirstOrDefault(p =>
                            string.Equals(p.Phase, phaseName, StringComparison.OrdinalIgnoreCase))
                    })
                    .Where(x => x.Data != null)
                    .ToList();

                if (phaseRows.Count == 0)
                    continue;

                var belowThresholdRows = phaseRows
                    .Where(x => x.Data.RmsPct < context.SagThresholdPct)
                    .ToList();

                if (belowThresholdRows.Count == 0)
                    continue;

                int phaseStartIndex = belowThresholdRows.First().RowIndex;
                int phaseEndIndex = belowThresholdRows.Last().RowIndex;

                var minRow = phaseRows
                    .OrderBy(x => x.Data.RmsPct)
                    .ThenBy(x => x.RowIndex)
                    .First();

                decimal referenceVoltage = decimal.Round(phaseRows.First().Data.ReferenceVoltage, 6);
                decimal residualVoltage = decimal.Round(minRow.Data.Rms, 6);
                decimal residualVoltagePct = decimal.Round(minRow.Data.RmsPct, 3);
                decimal sagDepth = Math.Max(0m, decimal.Round(referenceVoltage - residualVoltage, 6));
                decimal sagPercent = Math.Max(0m, decimal.Round(100m - residualVoltagePct, 3));

                DateTime phaseStartTimeUtc = ConvertToUtc(context, window[phaseStartIndex].TimeMs);
                DateTime phaseEndTimeUtc = ConvertToUtc(context, window[phaseEndIndex].TimeMs);
                decimal phaseDurationMs = SafeDuration(
                    window[phaseEndIndex].TimeMs - window[phaseStartIndex].TimeMs);

                phaseWindows.Add(new PhaseWindowInfo
                {
                    Phase = phaseName,
                    StartIndex = phaseStartIndex,
                    EndIndex = phaseEndIndex,
                    ResidualVoltagePct = residualVoltagePct,
                    DurationMs = phaseDurationMs
                });

                phaseResults.Add(new ZwavSagEventPhaseResult
                {
                    Phase = phaseName,
                    StartTimeUtc = phaseStartTimeUtc,
                    EndTimeUtc = phaseEndTimeUtc,
                    DurationMs = phaseDurationMs,
                    ReferenceType = context.ReferenceType,
                    ReferenceVoltage = referenceVoltage,
                    ResidualVoltage = residualVoltage,
                    ResidualVoltagePct = residualVoltagePct,
                    SagDepth = sagDepth,
                    SagPercent = sagPercent,
                    StartAngleDeg = null,
                    PhaseJumpDeg = null,
                    SagThresholdPct = context.SagThresholdPct,
                    InterruptThresholdPct = context.InterruptThresholdPct,
                    HysteresisPct = context.HysteresisPct,
                    IsTriggerPhase = false,
                    IsEndPhase = false,
                    IsWorstPhase = false
                });
            }

            if (phaseResults.Count == 0)
                return null;

            var triggerInfo = phaseWindows
                .OrderBy(x => x.StartIndex)
                .ThenBy(x => x.Phase, StringComparer.OrdinalIgnoreCase)
                .First();

            var endInfo = phaseWindows
                .OrderByDescending(x => x.EndIndex)
                .ThenBy(x => x.Phase, StringComparer.OrdinalIgnoreCase)
                .First();

            var worstInfo = phaseWindows
                .OrderBy(x => x.ResidualVoltagePct)
                .ThenByDescending(x => x.DurationMs)
                .ThenBy(x => x.Phase, StringComparer.OrdinalIgnoreCase)
                .First();

            var triggerPhase = phaseResults.FirstOrDefault(x =>
                string.Equals(x.Phase, triggerInfo.Phase, StringComparison.OrdinalIgnoreCase));
            var endPhase = phaseResults.FirstOrDefault(x =>
                string.Equals(x.Phase, endInfo.Phase, StringComparison.OrdinalIgnoreCase));
            var worstPhase = phaseResults.FirstOrDefault(x =>
                string.Equals(x.Phase, worstInfo.Phase, StringComparison.OrdinalIgnoreCase));

            if (triggerPhase != null) triggerPhase.IsTriggerPhase = true;
            if (endPhase != null) endPhase.IsEndPhase = true;
            if (worstPhase != null) worstPhase.IsWorstPhase = true;

            bool isInterruption = phaseResults.Any(x => x.ResidualVoltagePct < context.InterruptThresholdPct);
            string eventType = isInterruption ? "Interruption" : "Sag";

            var worst = worstPhase ?? phaseResults
                .OrderBy(x => x.ResidualVoltagePct)
                .ThenByDescending(x => x.DurationMs)
                .First();

            return new ZwavSagEventResult
            {
                EventType = eventType,
                StartTimeUtc = ConvertToUtc(context, eventStartMs),
                EndTimeUtc = ConvertToUtc(context, eventEndMs),
                OccurTimeUtc = ConvertToUtc(context, eventStartMs),
                DurationMs = eventDurationMs,
                TriggerPhase = triggerPhase?.Phase,
                EndPhase = endPhase?.Phase,
                WorstPhase = worst.Phase,
                ReferenceType = worst.ReferenceType,
                ReferenceVoltage = worst.ReferenceVoltage,
                ResidualVoltage = worst.ResidualVoltage,
                ResidualVoltagePct = worst.ResidualVoltagePct,
                SagDepth = worst.SagDepth,
                SagPercent = worst.SagPercent,
                StartAngleDeg = null,
                PhaseJumpDeg = null,
                SagThresholdPct = context.SagThresholdPct,
                InterruptThresholdPct = context.InterruptThresholdPct,
                HysteresisPct = context.HysteresisPct,
                IsMergedStatEvent = false,
                MergeGroupId = null,
                RawEventCount = 1,
                Phases = phaseResults
            };
        }

        private decimal ResolveReferenceVoltage(ZwavSagAnalyzeContext context, int channelIndex)
        {
            if (string.Equals(context.ReferenceType, "Declared", StringComparison.OrdinalIgnoreCase))
            {
                if (context.ReferenceVoltage.HasValue && context.ReferenceVoltage.Value > 0)
                    return decimal.Round(context.ReferenceVoltage.Value, 6);

                return EstimateReferenceVoltage(context, channelIndex);
            }

            if (string.Equals(context.ReferenceType, "Sliding", StringComparison.OrdinalIgnoreCase))
            {
                if (context.ReferenceVoltage.HasValue && context.ReferenceVoltage.Value > 0)
                    return decimal.Round(context.ReferenceVoltage.Value, 6);

                return EstimateReferenceVoltage(context, channelIndex);
            }

            return EstimateReferenceVoltage(context, channelIndex);
        }

        private decimal EstimateReferenceVoltage(ZwavSagAnalyzeContext context, int channelIndex)
        {
            int limit = Math.Min(context.Samples.Count, 2000);

            double sumSq = 0d;
            int validCount = 0;

            for (int i = 0; i < limit; i++)
            {
                var sample = context.Samples[i];
                if (sample.ChannelValues == null)
                    continue;

                if (sample.ChannelValues.TryGetValue(channelIndex, out var value) && value.HasValue)
                {
                    double v = value.Value;
                    sumSq += v * v;
                    validCount++;
                }
            }

            if (validCount == 0)
                return 0m;

            return decimal.Round((decimal)Math.Sqrt(sumSq / validCount), 6);
        }

        private static double EstimateSampleIntervalMs(IReadOnlyList<ZwavSagSamplePoint> samples)
        {
            if (samples == null || samples.Count < 2)
                return 0d;

            double total = 0d;
            int count = 0;

            for (int i = 1; i < samples.Count; i++)
            {
                double diff = samples[i].TimeMs - samples[i - 1].TimeMs;
                if (diff > 0)
                {
                    total += diff;
                    count++;
                }
            }

            return count == 0 ? 0d : total / count;
        }

        private static string ResolvePhaseName(ZwavVoltageChannelContext channel)
        {
            var raw = channel?.Phase;

            if (string.IsNullOrWhiteSpace(raw))
                raw = channel?.ChannelCode;

            if (string.IsNullOrWhiteSpace(raw))
                raw = channel?.ChannelName;

            if (string.IsNullOrWhiteSpace(raw))
                return $"CH{channel.ChannelIndex}";

            raw = raw.Trim().ToUpperInvariant();

            if (raw == "A" || raw == "B" || raw == "C" ||
                raw == "AB" || raw == "BC" || raw == "CA")
                return raw;

            if (raw.Contains("A") && !raw.Contains("B") && !raw.Contains("C")) return "A";
            if (raw.Contains("B") && !raw.Contains("A") && !raw.Contains("C")) return "B";
            if (raw.Contains("C") && !raw.Contains("A") && !raw.Contains("B")) return "C";
            if (raw.Contains("A") && raw.Contains("B") && !raw.Contains("C")) return "AB";
            if (raw.Contains("B") && raw.Contains("C") && !raw.Contains("A")) return "BC";
            if (raw.Contains("C") && raw.Contains("A") && !raw.Contains("B")) return "CA";

            return raw;
        }

        private static decimal SafeDuration(double durationMs)
        {
            if (durationMs <= 0)
                return 0m;

            return decimal.Round((decimal)durationMs, 3);
        }

        private static DateTime ConvertToUtc(ZwavSagAnalyzeContext context, double timeMs)
        {
            return context.WaveStartTimeUtc.AddMilliseconds(timeMs);
        }

        private class AlignedRmsRow
        {
            public int SampleNo { get; set; }
            public double TimeMs { get; set; }
            public List<AlignedRmsPhaseRow> Phases { get; set; } = new List<AlignedRmsPhaseRow>();
        }

        private class AlignedRmsPhaseRow
        {
            public int ChannelIndex { get; set; }
            public string Phase { get; set; }
            public decimal Rms { get; set; }
            public decimal RmsPct { get; set; }
            public decimal ReferenceVoltage { get; set; }
        }

        private class PhaseWindowInfo
        {
            public string Phase { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public decimal ResidualVoltagePct { get; set; }
            public decimal DurationMs { get; set; }
        }
    }
}