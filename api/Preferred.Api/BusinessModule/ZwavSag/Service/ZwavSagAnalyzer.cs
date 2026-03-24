using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 电压暂降分析器（优化版）
    /// 方案：
    /// 1）RMS窗口：1周波
    /// 2）更新步长：半周波
    /// 3）参考电压：优先传入，其次采用稳态高分位估算
    /// 4）事件判定：进入阈值 / 恢复阈值（含迟滞）
    /// 5）时长计算：按窗口覆盖区间
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

            AppendRmsPointResults(seriesList, result.RmsPoints);

            var alignedRows = AlignRmsSeries(seriesList);
            if (alignedRows.Count == 0)
                return Task.FromResult(result);

            var events = DetectEvents(context, alignedRows);
            if (events.Count > 0)
                result.Events.AddRange(events);

            return Task.FromResult(result);
        }

        #region 核心流程

        private static List<ChannelRmsSeries> BuildRmsSeries(ZwavSagAnalyzeContext context)
        {
            var result = new List<ChannelRmsSeries>(context.VoltageChannels.Length);

            double avgSampleIntervalMs = EstimateSampleIntervalMs(context.Samples);
            if (avgSampleIntervalMs <= 0)
                throw new InvalidOperationException("无法估算采样间隔，请检查 TimeMs 是否为毫秒");

            double cycleMs = 1000d / (double)context.FrequencyHz;
            double windowMs = cycleMs;
            double stepMs = cycleMs / 2d;

            int windowSize = Math.Max(1, (int)Math.Round(windowMs / avgSampleIntervalMs));
            int stepSize = Math.Max(1, (int)Math.Round(stepMs / avgSampleIntervalMs));

            var channels = context.VoltageChannels;
            for (int i = 0; i < channels.Length; i++)
            {
                var series = BuildSingleChannelRmsSeries(context, channels[i], windowSize, stepSize);
                if (series != null && series.Points.Count > 0)
                    result.Add(series);
            }

            return result;
        }

        private static ChannelRmsSeries BuildSingleChannelRmsSeries(
            ZwavSagAnalyzeContext context,
            ZwavVoltageChannelContext channel,
            int windowSize,
            int stepSize)
        {
            if (channel == null)
                return null;

            var samples = context.Samples;
            if (samples == null || samples.Count < windowSize)
                return null;

            int estimatedCount = ((samples.Count - windowSize) / stepSize) + 1;
            if (estimatedCount < 1) estimatedCount = 1;

            var rawRmsPoints = new List<RawRmsWindowPoint>(estimatedCount);

            for (int start = 0; start + windowSize <= samples.Count; start += stepSize)
            {
                int end = start + windowSize - 1;
                double sumSq = 0d;
                int validCount = 0;

                for (int i = start; i <= end; i++)
                {
                    var sample = samples[i];
                    if (sample?.ChannelValues == null)
                        continue;

                    if (sample.ChannelValues.TryGetValue(channel.ChannelIndex, out var value) && value.HasValue)
                    {
                        var v = value.Value;
                        sumSq += v * v;
                        validCount++;
                    }
                }

                if (validCount == 0)
                    continue;

                var rms = decimal.Round((decimal)Math.Sqrt(sumSq / validCount), 6);

                rawRmsPoints.Add(new RawRmsWindowPoint
                {
                    SampleNo = samples[end].SampleNo,
                    TimeMs = samples[end].TimeMs,
                    WindowStartMs = samples[start].TimeMs,
                    WindowEndMs = samples[end].TimeMs,
                    Rms = rms
                });
            }

            if (rawRmsPoints.Count == 0)
                return null;

            var referenceVoltage = ResolveReferenceVoltage(context, rawRmsPoints);
            if (referenceVoltage <= 0)
                return null;

            var series = new ChannelRmsSeries
            {
                ChannelIndex = channel.ChannelIndex,
                Phase = ResolvePhaseName(channel),
                ReferenceVoltage = referenceVoltage
            };

            for (int i = 0; i < rawRmsPoints.Count; i++)
            {
                var p = rawRmsPoints[i];
                var rmsPct = decimal.Round(p.Rms / referenceVoltage * 100m, 3);

                series.Points.Add(new RmsWindowPoint
                {
                    SampleNo = p.SampleNo,
                    TimeMs = p.TimeMs,
                    WindowStartMs = p.WindowStartMs,
                    WindowEndMs = p.WindowEndMs,
                    Rms = p.Rms,
                    RmsPct = rmsPct
                });
            }

            return series;
        }

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
                        Phase = series.Phase,
                        SampleNo = p.SampleNo,
                        TimeMs = p.TimeMs,
                        Rms = p.Rms,
                        RmsPct = p.RmsPct,
                        ReferenceVoltage = series.ReferenceVoltage,
                        SeqNo = j + 1
                    });
                }
            }
        }

        private static List<AlignedRmsRow> AlignRmsSeries(List<ChannelRmsSeries> seriesList)
        {
            if (seriesList == null || seriesList.Count == 0)
                return new List<AlignedRmsRow>();

            int minCount = int.MaxValue;
            for (int i = 0; i < seriesList.Count; i++)
            {
                var count = seriesList[i].Points.Count;
                if (count < minCount)
                    minCount = count;
            }

            if (minCount <= 0 || minCount == int.MaxValue)
                return new List<AlignedRmsRow>();

            var rows = new List<AlignedRmsRow>(minCount);

            for (int i = 0; i < minCount; i++)
            {
                var first = seriesList[0].Points[i];
                var row = new AlignedRmsRow
                {
                    SampleNo = first.SampleNo,
                    TimeMs = first.TimeMs,
                    WindowStartMs = first.WindowStartMs,
                    WindowEndMs = first.WindowEndMs,
                    Phases = new List<AlignedRmsPhaseRow>(seriesList.Count)
                };

                for (int j = 0; j < seriesList.Count; j++)
                {
                    var series = seriesList[j];
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

        private static List<ZwavSagEventResult> DetectEvents(
            ZwavSagAnalyzeContext context,
            List<AlignedRmsRow> alignedRows)
        {
            var results = new List<ZwavSagEventResult>();
            if (alignedRows == null || alignedRows.Count == 0)
                return results;

            decimal sagThreshold = context.SagThresholdPct;
            decimal recoverThreshold = context.SagThresholdPct + context.HysteresisPct;

            bool inEvent = false;
            int eventStartRow = -1;

            for (int i = 0; i < alignedRows.Count; i++)
            {
                var phases = alignedRows[i].Phases;
                bool anyBelowSag = false;
                bool allRecovered = phases.Count > 0;

                for (int j = 0; j < phases.Count; j++)
                {
                    var rmsPct = phases[j].RmsPct;
                    if (rmsPct <= sagThreshold)
                        anyBelowSag = true;

                    if (rmsPct < recoverThreshold)
                        allRecovered = false;
                }

                if (!inEvent)
                {
                    if (anyBelowSag)
                    {
                        inEvent = true;
                        eventStartRow = i;
                    }

                    continue;
                }

                if (allRecovered)
                {
                    int eventEndRow = Math.Max(eventStartRow, i - 1);
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

        private static ZwavSagEventResult BuildEventResult(
            ZwavSagAnalyzeContext context,
            List<AlignedRmsRow> rows,
            int startRow,
            int endRow)
        {
            if (startRow < 0 || endRow < startRow || endRow >= rows.Count)
                return null;

            var firstRow = rows[startRow];
            var lastRow = rows[endRow];

            double eventStartMs = firstRow.WindowStartMs;
            double eventEndMs = lastRow.WindowEndMs;
            decimal eventDurationMs = SafeDuration(eventEndMs - eventStartMs);

            var phaseInfoMap = new Dictionary<string, PhaseAggregate>(StringComparer.OrdinalIgnoreCase);

            for (int rowIndex = startRow; rowIndex <= endRow; rowIndex++)
            {
                var row = rows[rowIndex];
                int localIndex = rowIndex - startRow;

                for (int phaseIndex = 0; phaseIndex < row.Phases.Count; phaseIndex++)
                {
                    var phaseRow = row.Phases[phaseIndex];
                    if (string.IsNullOrWhiteSpace(phaseRow.Phase))
                        continue;

                    if (!phaseInfoMap.TryGetValue(phaseRow.Phase, out var agg))
                    {
                        agg = new PhaseAggregate
                        {
                            Phase = phaseRow.Phase,
                            ReferenceVoltage = decimal.Round(phaseRow.ReferenceVoltage, 6),
                            MinRms = decimal.Round(phaseRow.Rms, 6),
                            MinRmsPct = decimal.Round(phaseRow.RmsPct, 3),
                            MinRowIndex = localIndex,
                            StartIndex = -1,
                            EndIndex = -1
                        };
                        phaseInfoMap.Add(phaseRow.Phase, agg);
                    }

                    if (phaseRow.RmsPct <= context.SagThresholdPct)
                    {
                        if (agg.StartIndex < 0)
                            agg.StartIndex = localIndex;

                        agg.EndIndex = localIndex;
                    }

                    if (phaseRow.RmsPct < agg.MinRmsPct ||
                        (phaseRow.RmsPct == agg.MinRmsPct && localIndex < agg.MinRowIndex))
                    {
                        agg.MinRmsPct = decimal.Round(phaseRow.RmsPct, 3);
                        agg.MinRms = decimal.Round(phaseRow.Rms, 6);
                        agg.MinRowIndex = localIndex;
                        agg.ReferenceVoltage = decimal.Round(phaseRow.ReferenceVoltage, 6);
                    }
                }
            }

            if (phaseInfoMap.Count == 0)
                return null;

            var phaseResults = new List<ZwavSagEventPhaseResult>(phaseInfoMap.Count);
            var phaseWindows = new List<PhaseWindowInfo>(phaseInfoMap.Count);

            foreach (var kv in phaseInfoMap)
            {
                var agg = kv.Value;
                if (agg.StartIndex < 0 || agg.EndIndex < 0)
                    continue;

                var phaseStartMs = rows[startRow + agg.StartIndex].WindowStartMs;
                var phaseEndMs = rows[startRow + agg.EndIndex].WindowEndMs;
                var phaseDurationMs = SafeDuration(phaseEndMs - phaseStartMs);

                var residualVoltage = agg.MinRms;
                var residualVoltagePct = agg.MinRmsPct;
                var referenceVoltage = agg.ReferenceVoltage;
                var sagDepth = Math.Max(0m, decimal.Round(referenceVoltage - residualVoltage, 6));
                var sagPercent = Math.Max(0m, decimal.Round(100m - residualVoltagePct, 3));

                phaseWindows.Add(new PhaseWindowInfo
                {
                    Phase = agg.Phase,
                    StartIndex = agg.StartIndex,
                    EndIndex = agg.EndIndex,
                    ResidualVoltagePct = residualVoltagePct,
                    DurationMs = phaseDurationMs
                });

                phaseResults.Add(new ZwavSagEventPhaseResult
                {
                    Phase = agg.Phase,
                    StartTimeUtc = ConvertToUtc(context, phaseStartMs),
                    EndTimeUtc = ConvertToUtc(context, phaseEndMs),
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

            PhaseWindowInfo triggerInfo = null;
            PhaseWindowInfo endInfo = null;
            PhaseWindowInfo worstInfo = null;

            for (int i = 0; i < phaseWindows.Count; i++)
            {
                var info = phaseWindows[i];

                if (triggerInfo == null ||
                    info.StartIndex < triggerInfo.StartIndex ||
                    (info.StartIndex == triggerInfo.StartIndex &&
                     string.Compare(info.Phase, triggerInfo.Phase, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    triggerInfo = info;
                }

                if (endInfo == null ||
                    info.EndIndex > endInfo.EndIndex ||
                    (info.EndIndex == endInfo.EndIndex &&
                     string.Compare(info.Phase, endInfo.Phase, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    endInfo = info;
                }

                if (worstInfo == null ||
                    info.ResidualVoltagePct < worstInfo.ResidualVoltagePct ||
                    (info.ResidualVoltagePct == worstInfo.ResidualVoltagePct && info.DurationMs > worstInfo.DurationMs) ||
                    (info.ResidualVoltagePct == worstInfo.ResidualVoltagePct &&
                     info.DurationMs == worstInfo.DurationMs &&
                     string.Compare(info.Phase, worstInfo.Phase, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    worstInfo = info;
                }
            }

            ZwavSagEventPhaseResult triggerPhase = null;
            ZwavSagEventPhaseResult endPhase = null;
            ZwavSagEventPhaseResult worstPhase = null;

            for (int i = 0; i < phaseResults.Count; i++)
            {
                var item = phaseResults[i];

                if (triggerInfo != null && string.Equals(item.Phase, triggerInfo.Phase, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsTriggerPhase = true;
                    triggerPhase = item;
                }

                if (endInfo != null && string.Equals(item.Phase, endInfo.Phase, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsEndPhase = true;
                    endPhase = item;
                }

                if (worstInfo != null && string.Equals(item.Phase, worstInfo.Phase, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsWorstPhase = true;
                    worstPhase = item;
                }
            }

            bool isInterruption = false;
            for (int i = 0; i < phaseResults.Count; i++)
            {
                if (phaseResults[i].ResidualVoltagePct <= context.InterruptThresholdPct)
                {
                    isInterruption = true;
                    break;
                }
            }

            var worst = worstPhase ?? phaseResults
                .OrderBy(x => x.ResidualVoltagePct)
                .ThenByDescending(x => x.DurationMs)
                .First();

            return new ZwavSagEventResult
            {
                EventType = isInterruption ? "Interruption" : "Sag",
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

        #endregion

        #region 参考电压

        private static decimal ResolveReferenceVoltage(
            ZwavSagAnalyzeContext context,
            List<RawRmsWindowPoint> rmsPoints)
        {
            if (context.ReferenceVoltage.HasValue && context.ReferenceVoltage.Value > 0)
                return decimal.Round(context.ReferenceVoltage.Value, 6);

            return EstimateReferenceVoltage(rmsPoints);
        }

        private static decimal EstimateReferenceVoltage(List<RawRmsWindowPoint> rmsPoints)
        {
            if (rmsPoints == null || rmsPoints.Count == 0)
                return 0m;

            var values = new List<decimal>(rmsPoints.Count);
            for (int i = 0; i < rmsPoints.Count; i++)
            {
                if (rmsPoints[i].Rms > 0)
                    values.Add(rmsPoints[i].Rms);
            }

            if (values.Count == 0)
                return 0m;

            values.Sort();
            int index = (int)Math.Round((values.Count - 1) * 0.95d);
            if (index < 0) index = 0;
            if (index >= values.Count) index = values.Count - 1;

            return decimal.Round(values[index], 6);
        }

        #endregion

        #region 参数校验

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

        #endregion

        #region 工具方法

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
                    return $"CH{channel?.ChannelIndex ?? 0}";
            }
        }

        private static decimal SafeDuration(double durationMs)
        {
            return durationMs <= 0
                ? 0m
                : decimal.Round((decimal)durationMs, 3);
        }

        private static DateTime ConvertToUtc(ZwavSagAnalyzeContext context, double timeMs)
        {
            return context.WaveStartTimeUtc.AddMilliseconds(timeMs);
        }

        #endregion

        #region 内部类

        private sealed class ChannelRmsSeries
        {
            public int ChannelIndex { get; set; }
            public string Phase { get; set; }
            public decimal ReferenceVoltage { get; set; }
            public List<RmsWindowPoint> Points { get; } = new List<RmsWindowPoint>();
        }

        private sealed class RawRmsWindowPoint
        {
            public int SampleNo { get; set; }
            public double TimeMs { get; set; }
            public double WindowStartMs { get; set; }
            public double WindowEndMs { get; set; }
            public decimal Rms { get; set; }
        }

        private sealed class RmsWindowPoint
        {
            public int SampleNo { get; set; }
            public double TimeMs { get; set; }
            public double WindowStartMs { get; set; }
            public double WindowEndMs { get; set; }
            public decimal Rms { get; set; }
            public decimal RmsPct { get; set; }
        }

        private sealed class AlignedRmsRow
        {
            public int SampleNo { get; set; }
            public double TimeMs { get; set; }
            public double WindowStartMs { get; set; }
            public double WindowEndMs { get; set; }
            public List<AlignedRmsPhaseRow> Phases { get; set; }
        }

        private sealed class AlignedRmsPhaseRow
        {
            public int ChannelIndex { get; set; }
            public string Phase { get; set; }
            public decimal Rms { get; set; }
            public decimal RmsPct { get; set; }
            public decimal ReferenceVoltage { get; set; }
        }

        private sealed class PhaseWindowInfo
        {
            public string Phase { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public decimal ResidualVoltagePct { get; set; }
            public decimal DurationMs { get; set; }
        }

        private sealed class PhaseAggregate
        {
            public string Phase { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public decimal ReferenceVoltage { get; set; }
            public decimal MinRms { get; set; }
            public decimal MinRmsPct { get; set; }
            public int MinRowIndex { get; set; }
        }

        #endregion
    }
}