using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Zwav.Application.Parsing
{
    public class CfgParser
    {
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

            // 7) start/trigger time
            res.StartTimeRaw = idx < lines.Count ? lines[idx++] : null;
            res.TriggerTimeRaw = idx < lines.Count ? lines[idx++] : null;

            // 8) data format: ASCII / BINARY / BINARY32 / FLOAT32
            // 兼容： "BINARY" 或 "BINARY,1" 或 "ASCII" 等
            string fmtLine = idx < lines.Count ? lines[idx++] : null;
            string fmt = null;
            decimal? timeMul = null;

            if (!string.IsNullOrWhiteSpace(fmtLine))
            {
                var fmtParts = SplitCsv(fmtLine);
                fmt = NormalizeFormat(Get(fmtParts, 0));

                // 同行 timemult：BINARY,1
                if (fmtParts.Count > 1)
                    timeMul = TryDec(Get(fmtParts, 1));
            }

            res.FormatType = fmt; // 建议你后续按这四种值判断

            // 9) time mul（可选：标准是下一行）
            if (!timeMul.HasValue && idx < lines.Count)
            {
                // 只有当这一行能解析成数字，才吃掉作为 timemult
                var maybe = TryDec(lines[idx]);
                if (maybe.HasValue)
                {
                    timeMul = maybe;
                    idx++;
                }
            }

            res.TimeMul = timeMul;

            // digital words (16bits per word)
            res.DigitalWords = (res.DigitalCount + 15) / 16;

            // DataType：不再写死 INT16，按 FormatType 推导（供 DAT 解析使用）
            res.DataType = fmt switch
            {
                "ASCII" => "ASCII",
                "BINARY" => "INT16",
                "BINARY32" => "INT32",
                "FLOAT32" => "FLOAT32",
                _ => "INT16"
            };

            return res;
        }

        private static string NormalizeFormat(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            s = s.Trim().ToUpperInvariant();

            // 兼容一些厂家写法
            if (s == "BIN") return "BINARY";
            if (s == "BINARY16") return "BINARY";
            if (s == "FLOAT") return "FLOAT32";
            if (s == "REAL32") return "FLOAT32";

            // 标准集合
            if (s == "ASCII" || s == "BINARY" || s == "BINARY32" || s == "FLOAT32")
                return s;

            return s; // 未知就原样返回
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

            if (start < text.Length)
            {
                var line = text.Substring(start).Trim();
                if (line.Length > 0) list.Add(line);
            }

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