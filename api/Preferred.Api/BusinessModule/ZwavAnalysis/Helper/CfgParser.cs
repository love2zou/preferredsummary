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

        private static string[] SplitCsv(string line)
            => (line ?? "").Split(',').Select(x => x.Trim()).ToArray();

        private static void ParseADCount(string token, out int total)
        {
            total = 0;
            if (string.IsNullOrWhiteSpace(token)) return;
            int.TryParse(token.Trim(), out total);
        }

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
    }
}