using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Zwav.Application.Parsing
{
    public class CfgParser
    {
        public CfgParseResult Parse2(string cfgText)
        {
            var lines = cfgText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (lines.Count < 5) throw new Exception("CFG content too short.");

            int idx = 0;

            // 1) station, device, rev
            var head = SplitCsv(lines[idx++]);
            var res = new CfgParseResult
            {
                FullText = cfgText,
                StationName = head.ElementAtOrDefault(0),
                DeviceId = head.ElementAtOrDefault(1),
                Revision = head.ElementAtOrDefault(2)
            };

            // 2) channel counts: e.g. "6,3A,3D"
            var cc = SplitCsv(lines[idx++]);
            ParseADCount(cc.FirstOrDefault(), out var totalCh);
            int analog = 0, digital = 0;
            foreach (var token in cc.Skip(1))
            {
                var t = token.Trim().ToUpperInvariant();
                if (t.EndsWith("A") && int.TryParse(t.TrimEnd('A'), out var a)) analog = a;
                if (t.EndsWith("D") && int.TryParse(t.TrimEnd('D'), out var d)) digital = d;
            }
            res.AnalogCount = analog;
            res.DigitalCount = digital;

            // 3) analog channel lines
            for (int i = 0; i < analog; i++)
            {
                var parts = SplitCsv(lines[idx++]);
                // 常见格式: index, name, phase, ccbm, unit, a, b, skew, min, max, primary, secondary, ps
                // 你前端是偏“显示+换算”，这里也做 A/B
                var chIndex = TryInt(parts.ElementAtOrDefault(0), i + 1);
                var nameOrCode = parts.ElementAtOrDefault(1);
                var phase = parts.ElementAtOrDefault(2);
                var unit = parts.ElementAtOrDefault(4);
                var a = TryDec(parts.ElementAtOrDefault(5));
                var b = TryDec(parts.ElementAtOrDefault(6));
                var skew = TryDec(parts.ElementAtOrDefault(7));

                res.Channels.Add(new ChannelDef
                {
                    ChannelIndex = chIndex,
                    ChannelType = "Analog",
                    Code = nameOrCode,
                    Name = nameOrCode,
                    Phase = phase,
                    Unit = unit,
                    A = a,
                    B = b,
                    Skew = skew
                });
            }

            // 4) digital channel lines（可只保留定义，不做波形）
            for (int i = 0; i < digital; i++)
            {
                var parts = SplitCsv(lines[idx++]);
                // 常见格式: index, name, phase, ccbm, ...
                var chIndex = TryInt(parts.ElementAtOrDefault(0), analog + i + 1);
                var nameOrCode = parts.ElementAtOrDefault(1);
                var phase = parts.ElementAtOrDefault(2);

                res.Channels.Add(new ChannelDef
                {
                    ChannelIndex = chIndex,
                    ChannelType = "Digital",
                    Code = nameOrCode,
                    Name = nameOrCode,
                    Phase = phase,
                    Unit = null
                });
            }

            // 5) line frequency
            res.FrequencyHz = TryDec(lines[idx++]);

            // 6) sample rate blocks count
            int nrates = TryInt(lines[idx++], 1);
            var rates = new List<object>();
            for (int i = 0; i < nrates; i++)
            {
                var r = SplitCsv(lines[idx++]);
                // 常见: sampleRate, endSample
                rates.Add(new
                {
                    sampleRate = TryDec(r.ElementAtOrDefault(0)),
                    endSample = TryLong(r.ElementAtOrDefault(1))
                });
            }
            res.SampleRateJson = JsonSerializer.Serialize(rates);

            // 7) start/trigger time
            res.StartTimeRaw = lines.ElementAtOrDefault(idx++);
            res.TriggerTimeRaw = lines.ElementAtOrDefault(idx++);

            // 8) data format: ASCII/BINARY
            res.FormatType = lines.ElementAtOrDefault(idx++)?.ToUpperInvariant();

            // 9) time mul（可能存在，也可能没有）
            if (idx < lines.Count)
                res.TimeMul = TryDec(lines[idx]);

            // digital words (16bits per word)
            res.DigitalWords = (int)Math.Ceiling(res.DigitalCount / 16.0);

            // 虚拟通道示例：3I0（你前端也有）
            res.Channels.Add(new ChannelDef
            {
                ChannelIndex = 0,
                ChannelType = "Virtual",
                Code = "3I0",
                Name = "3I0",
                Phase = "N",
                Unit = null
            });

            // data type 简化：目前按常见 INT16
            res.DataType = "INT16";
            return res;
        }

        // private static string[] SplitCsv(string line)
        //     => (line ?? "").Split(',').Select(x => x.Trim()).ToArray();

        private static void ParseADCount(string token, out int total)
        {
            total = 0;
            if (string.IsNullOrWhiteSpace(token)) return;
            int.TryParse(token.Trim(), out total);
        }

        // private static int TryInt(string s, int def)
        //     => int.TryParse(s, out var v) ? v : def;

        // private static long TryLong(string s)
        //     => long.TryParse(s, out var v) ? v : 0;

        // private static decimal? TryDec(string s)
        // {
        //     if (string.IsNullOrWhiteSpace(s)) return null;
        //     if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        //     if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out v)) return v;
        //     return null;
        // }


        // 你的表字段上限
        // private const int MaxAnalog = 70;
        // private const int MaxDigital = 700;

        public CfgParseResult Parse(string cfgText)
        {
            if (string.IsNullOrWhiteSpace(cfgText))
                throw new ArgumentException("CFG content is empty.", nameof(cfgText));

            var lines = ReadNonEmptyTrimmedLines(cfgText);
            if (lines.Count < 5)
                throw new Exception("CFG content too short.");

            int idx = 0;

            // 1) station, device, rev
            var head = SplitCsv(lines[idx++]);
            var res = new CfgParseResult
            {
                FullText = cfgText,
                StationName = head.Count > 0 ? head[0] : null,
                DeviceId = head.Count > 1 ? head[1] : null,
                Revision = head.Count > 2 ? head[2] : null
            };

            // 2) channel counts: e.g. "6,3A,3D"
            var cc = SplitCsv(lines[idx++]);

            int analog = 0, digital = 0;
            for (int i = 1; i < cc.Count; i++)
            {
                var t = (cc[i] ?? string.Empty).Trim().ToUpperInvariant();
                if (t.EndsWith("A", StringComparison.Ordinal))
                {
                    if (int.TryParse(t.Substring(0, t.Length - 1), out var a))
                        analog = a;
                }
                else if (t.EndsWith("D", StringComparison.Ordinal))
                {
                    if (int.TryParse(t.Substring(0, t.Length - 1), out var d))
                        digital = d;
                }
            }

            // === 强制上限校验（你第 2 点要求）===
            if (analog > ZwavConstants.MaxAnalog)
                throw new InvalidOperationException($"CFG analog channel count = {analog} exceeds supported max {ZwavConstants.MaxAnalog}. Storage schema has only {ZwavConstants.MaxAnalog} analog fields.");

            if (digital > ZwavConstants.MaxDigital)
                throw new InvalidOperationException($"CFG digital channel count = {digital} exceeds supported max {ZwavConstants.MaxDigital}. Storage schema has only {ZwavConstants.MaxDigital} digital fields.");

            res.AnalogCount = analog;
            res.DigitalCount = digital;

            // 3) analog channel lines
            EnsureEnoughLines(lines, idx, analog, "analog channel lines");
            for (int i = 0; i < analog; i++)
            {
                var parts = SplitCsv(lines[idx++]);
                // 常见格式: index, name, phase, ccbm, unit, a, b, skew, ...
                var chIndex = TryInt(Get(parts, 0), i + 1);
                var nameOrCode = Get(parts, 1);
                var phase = Get(parts, 2);
                var unit = Get(parts, 4);
                var a = TryDec(Get(parts, 5));
                var b = TryDec(Get(parts, 6));
                var skew = TryDec(Get(parts, 7));

                res.Channels.Add(new ChannelDef
                {
                    ChannelIndex = chIndex,
                    ChannelType = "Analog",
                    Code = nameOrCode,
                    Name = nameOrCode,
                    Phase = phase,
                    Unit = unit,
                    A = a,
                    B = b,
                    Skew = skew
                });
            }

            // 4) digital channel lines
            EnsureEnoughLines(lines, idx, digital, "digital channel lines");
            for (int i = 0; i < digital; i++)
            {
                var parts = SplitCsv(lines[idx++]);
                var chIndex = TryInt(Get(parts, 0), analog + i + 1);
                var nameOrCode = Get(parts, 1);
                var phase = Get(parts, 2);

                res.Channels.Add(new ChannelDef
                {
                    ChannelIndex = chIndex,
                    ChannelType = "Digital",
                    Code = nameOrCode,
                    Name = nameOrCode,
                    Phase = phase,
                    Unit = null
                });
            }

            // 5) line frequency
            EnsureEnoughLines(lines, idx, 1, "frequency");
            res.FrequencyHz = TryDec(lines[idx++]);

            // 6) sample rate blocks count
            EnsureEnoughLines(lines, idx, 1, "nrates");
            int nrates = TryInt(lines[idx++], 1);

            // nrates 行必须存在
            EnsureEnoughLines(lines, idx, nrates, "sample rate blocks");

            var rates = new List<SampleRateBlock>(capacity: nrates);
            for (int i = 0; i < nrates; i++)
            {
                var r = SplitCsv(lines[idx++]);
                rates.Add(new SampleRateBlock
                {
                    sampleRate = TryDec(Get(r, 0)),
                    endSample = TryLong(Get(r, 1))
                });
            }
            res.SampleRateJson = JsonSerializer.Serialize(rates);

            // 7) start/trigger time（允许缺失，但最好保护）
            res.StartTimeRaw = idx < lines.Count ? lines[idx++] : null;
            res.TriggerTimeRaw = idx < lines.Count ? lines[idx++] : null;

            // 8) data format: ASCII/BINARY
            res.FormatType = idx < lines.Count ? (lines[idx++]?.ToUpperInvariant()) : null;

            // 9) time mul（可选）
            if (idx < lines.Count)
                res.TimeMul = TryDec(lines[idx]);

            // digital words (16bits per word)
            res.DigitalWords = (res.DigitalCount + 15) / 16;

            // 虚拟通道示例：3I0（如不需要可移除；这里保留你原意）
            res.Channels.Add(new ChannelDef
            {
                ChannelIndex = 0,
                ChannelType = "Virtual",
                Code = "3I0",
                Name = "3I0",
                Phase = "N",
                Unit = null
            });

            // data type 简化：目前按常见 INT16
            res.DataType = "INT16";
            return res;
        }

        private static void EnsureEnoughLines(List<string> lines, int idx, int need, string sectionName)
        {
            if (idx + need > lines.Count)
                throw new Exception($"CFG content incomplete: missing {sectionName}. Need {need} line(s) but only {lines.Count - idx} remaining.");
        }

        private static List<string> ReadNonEmptyTrimmedLines(string text)
        {
            var list = new List<string>(capacity: 256);

            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    var line = text.Substring(start, i - start).Trim();
                    if (line.Length > 0) list.Add(line);
                    start = i + 1;
                }
            }

            // last line
            if (start < text.Length)
            {
                var line = text.Substring(start).Trim();
                if (line.Length > 0) list.Add(line);
            }

            // 兼容 \r\n：上面 Trim 已处理 \r
            return list;
        }

        private static List<string> SplitCsv(string line)
        {
            var result = new List<string>(16);
            if (line == null) return result;

            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i == line.Length || line[i] == ',')
                {
                    var part = line.Substring(start, i - start).Trim();
                    result.Add(part);
                    start = i + 1;
                }
            }
            return result;
        }

        private static string Get(List<string> parts, int index)
            => (parts != null && index >= 0 && index < parts.Count) ? parts[index] : null;

        private static int TryInt(string s, int def)
            => int.TryParse(s, out var v) ? v : def;

        private static long TryLong(string s)
            => long.TryParse(s, out var v) ? v : 0;

        private static decimal? TryDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out v)) return v;
            return null;
        }

        private sealed class SampleRateBlock
        {
            public decimal? sampleRate { get; set; }
            public long endSample { get; set; }
        }
    }
}