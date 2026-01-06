using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;

namespace Zwav.Application.Parsing
{
    public static class DatMetaCalculator
    {
        public enum AnalogEncoding { Int16, Int32, Float32 }

        public enum DatDataType
        {
            ASCII,
            BINARY,     // 16-bit analog
            BINARY32,   // 32-bit analog (int32)
            FLOAT32,    // 32-bit analog (float)
            Unknown
        }

        public sealed class DatLayoutInfo
        {
            public int StartOffset { get; set; }
            public AnalogEncoding AnalogEncoding { get; set; }
            public int AnalogBytesPerSample { get; set; }
            public int DvWords { get; set; }
            public int RecordSize { get; set; }
            public int Score { get; set; }
            public string Reason { get; set; }
        }

        // =========================
        // InspectDat（保留）
        // =========================
        public static (int dvWords, int recordSize, int totalRecords) InspectDat(
            byte[] datBytes, int analogCount, int digitalCount)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));

            int dvWords = (digitalCount + 15) / 16;
            int recordSize = 4 + 4 + (analogCount * 2) + (dvWords * 2);
            int totalRecords = recordSize <= 0 ? 0 : (datBytes.Length / recordSize);
            return (dvWords, recordSize, totalRecords);
        }

        private static (int dvWords, int recordSize, int totalRecords) InspectDat(
            ReadOnlySpan<byte> datBytes, int analogCount, int digitalCount, int analogBytesPerSample, int startOffset, int dvWordsOverride)
        {
            int dvWords = dvWordsOverride >= 0 ? dvWordsOverride : (digitalCount + 15) / 16;
            int recordSize = 4 + 4 + (analogCount * analogBytesPerSample) + (dvWords * 2);

            int available = Math.Max(0, datBytes.Length - Math.Max(0, startOffset));
            int totalRecords = recordSize <= 0 ? 0 : (available / recordSize);

            return (dvWords, recordSize, totalRecords);
        }

        private static (AnalogEncoding enc, int bytesPerSample) GetAnalogEncodingFromCfg(DatDataType dt)
        {
            return dt switch
            {
                DatDataType.BINARY => (AnalogEncoding.Int16, 2),
                DatDataType.BINARY32 => (AnalogEncoding.Int32, 4),
                DatDataType.FLOAT32 => (AnalogEncoding.Float32, 4),
                _ => (AnalogEncoding.Int16, 2)
            };
        }

        // =========================
        // 关键：按你现有 CfgParseResult 解析 DataType/TimeMul
        // =========================
        private static DatDataType ResolveDatDataType(CfgParseResult cfg)
        {
            if (cfg == null) return DatDataType.Unknown;

            var s = (cfg.FormatType ?? string.Empty).Trim().ToUpperInvariant();

            // 兼容一些厂家写法
            if (s == "BIN") s = "BINARY";
            if (s == "BINARY16") s = "BINARY";
            if (s == "FLOAT") s = "FLOAT32";
            if (s == "REAL32") s = "FLOAT32";

            return s switch
            {
                "ASCII" => DatDataType.ASCII,
                "BINARY" => DatDataType.BINARY,
                "BINARY32" => DatDataType.BINARY32,
                "FLOAT32" => DatDataType.FLOAT32,
                _ => DatDataType.Unknown
            };
        }

        private static double ResolveTimeMultiplier(CfgParseResult cfg)
        {
            if (cfg?.TimeMul is decimal m && m > 0) return (double)m;
            return 1d;
        }

        // ==========================================================
        // DetectLayoutStrong / ScoreLayout（保持你原逻辑即可，这里省略：若你已有就不动）
        // 你可以继续用你现有版本的 DetectLayoutStrong / ScoreLayout
        // ==========================================================

        public static DatLayoutInfo DetectLayoutStrong(
            ReadOnlySpan<byte> dat,
            int analogCount,
            int digitalCount,
            int? dvWordsHint = null,
            int maxCheckRecords = 40)
        {
            if (analogCount < 0) throw new ArgumentOutOfRangeException(nameof(analogCount));
            if (digitalCount < 0) throw new ArgumentOutOfRangeException(nameof(digitalCount));
            if (dat.Length < 32) throw new ArgumentException("DAT too small.", nameof(dat));

            int[] offsets = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 20, 24, 28, 32, 40, 48, 56, 64, 96, 128, 256 };
            var encodings = new[] { AnalogEncoding.Int16, AnalogEncoding.Int32, AnalogEncoding.Float32 };

            int dvMin, dvMax;
            int dvCeil = (digitalCount + 15) / 16;

            if (digitalCount == 0)
            {
                dvMin = 0; dvMax = 0;
            }
            else
            {
                int baseDv = dvWordsHint.HasValue && dvWordsHint.Value > 0 ? dvWordsHint.Value : dvCeil;
                dvMin = Math.Max(1, baseDv - 8);
                dvMax = Math.Min(Math.Max(baseDv + 8, dvCeil + 8), 128);
            }

            DatLayoutInfo best = null;

            foreach (var start in offsets)
            {
                if (start < 0 || start >= dat.Length) continue;

                foreach (var enc in encodings)
                {
                    int analogBytesPerSample = enc == AnalogEncoding.Int16 ? 2 : 4;

                    if (analogCount > 0 && (8 + analogCount * analogBytesPerSample) > 65535)
                        continue;

                    for (int dv = dvMin; dv <= dvMax; dv++)
                    {
                        int recordSize = 8 + (analogCount * analogBytesPerSample) + (dv * 2);
                        if (recordSize <= 0 || recordSize > 65535) continue;

                        int available = dat.Length - start;
                        int possibleRecords = available / recordSize;
                        if (possibleRecords < 6) continue;

                        int check = Math.Min(maxCheckRecords, possibleRecords);

                        int score = ScoreLayout(dat, start, recordSize, analogCount, analogBytesPerSample, dv, enc, check, out string reason);
                        if (best == null || score > best.Score)
                        {
                            best = new DatLayoutInfo
                            {
                                StartOffset = start,
                                AnalogEncoding = enc,
                                AnalogBytesPerSample = analogBytesPerSample,
                                DvWords = dv,
                                RecordSize = recordSize,
                                Score = score,
                                Reason = reason
                            };
                        }
                    }
                }
            }

            return best ?? new DatLayoutInfo
            {
                StartOffset = 0,
                AnalogEncoding = AnalogEncoding.Int16,
                AnalogBytesPerSample = 2,
                DvWords = dvCeil,
                RecordSize = 8 + analogCount * 2 + dvCeil * 2,
                Score = int.MinValue,
                Reason = "No layout matched. Consider non-standard DAT or ASCII format."
            };
        }

        private static int ScoreLayout(
            ReadOnlySpan<byte> dat,
            int start,
            int recordSize,
            int analogCount,
            int analogBytesPerSample,
            int dvWords,
            AnalogEncoding enc,
            int checkRecords,
            out string reason)
        {
            int score = 0;
            int off = start;

            int lastSample = 0;
            int lastTime = 0;

            int sampleIncOk = 0;
            int sampleNonNeg = 0;
            int timeIncOk = 0;
            int timeNonNeg = 0;

            List<int> dtList = new List<int>(checkRecords);

            int analogFiniteOk = 0;
            int analogCrazy = 0;

            int digitalNotAllSame = 0;

            int firstSample = 0;
            int firstTime = 0;

            for (int i = 0; i < checkRecords; i++)
            {
                if (off + 8 > dat.Length) break;

                int sample = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(off, 4));
                int time = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(off + 4, 4));

                if (i == 0)
                {
                    firstSample = sample;
                    firstTime = time;
                }

                if (sample >= 0) sampleNonNeg++;
                if (time >= 0) timeNonNeg++;

                if (i > 0)
                {
                    if (sample == lastSample + 1) sampleIncOk++;
                    else if (sample > lastSample) score += 1;
                    else score -= 6;

                    int dt = time - lastTime;
                    if (dt > 0) { timeIncOk++; dtList.Add(dt); }
                    else score -= 3;
                }

                if (analogCount > 0)
                {
                    int analogBase = off + 8;
                    int[] ks = analogCount >= 3 ? new[] { 0, 1, analogCount - 1 } : Enumerable.Range(0, analogCount).ToArray();

                    foreach (var k in ks)
                    {
                        int pos = analogBase + k * analogBytesPerSample;
                        if (pos + analogBytesPerSample > off + recordSize) break;

                        double val;
                        if (enc == AnalogEncoding.Int16)
                        {
                            short raw = BinaryPrimitives.ReadInt16LittleEndian(dat.Slice(pos, 2));
                            val = raw;
                        }
                        else if (enc == AnalogEncoding.Int32)
                        {
                            int raw = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(pos, 4));
                            val = raw;
                        }
                        else
                        {
                            int bits = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(pos, 4));
                            float f = BitConverter.Int32BitsToSingle(bits);
                            val = f;
                        }

                        if (double.IsNaN(val) || double.IsInfinity(val))
                        {
                            analogCrazy++;
                        }
                        else
                        {
                            analogFiniteOk++;
                            if (Math.Abs(val) > 1e9) analogCrazy++;
                        }
                    }
                }

                if (dvWords > 0)
                {
                    int digitalBase = off + 8 + analogCount * analogBytesPerSample;
                    int digitalBytes = dvWords * 2;

                    if (digitalBase + digitalBytes <= off + recordSize && digitalBase + digitalBytes <= dat.Length)
                    {
                        byte b0 = dat[digitalBase];
                        byte b1 = dat[digitalBase + 1];
                        byte bL0 = dat[digitalBase + digitalBytes - 2];
                        byte bL1 = dat[digitalBase + digitalBytes - 1];

                        if (!(b0 == bL0 && b1 == bL1)) digitalNotAllSame++;
                    }
                }

                lastSample = sample;
                lastTime = time;

                off += recordSize;
            }

            score += sampleIncOk * 12;
            score += timeIncOk * 6;

            score += sampleNonNeg * 1;
            score += timeNonNeg * 1;

            if (firstSample == 1) score += 20;
            if (firstTime == 0) score += 10;

            if (dtList.Count >= 8)
            {
                dtList.Sort();
                int median = dtList[dtList.Count / 2];
                int stable = 0;
                int tol = Math.Max(2, median / 50);
                foreach (var dt in dtList)
                {
                    if (Math.Abs(dt - median) <= tol) stable++;
                }
                score += stable * 2;
            }

            score += analogFiniteOk * 1;
            score -= analogCrazy * 8;

            score += digitalNotAllSame * 1;

            reason = $"sampleIncOk={sampleIncOk}, timeIncOk={timeIncOk}, analogFiniteOk={analogFiniteOk}, analogCrazy={analogCrazy}, digitalNotAllSame={digitalNotAllSame}";
            return score;
        }

        // ==========================================================
        // ParseDatAllChannels —— 按你当前 CfgParseResult 修正版本
        // ==========================================================
        public static WaveDataParseResult ParseDatAllChannels(
            byte[] datBytes,
            CfgParseResult cfg,
            int maxRecords = 20)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (cfg.AnalogCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.AnalogCount));
            if (cfg.DigitalCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.DigitalCount));
            if (maxRecords <= 0) maxRecords = 1;

            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            // 1) 只从 Analog 通道提取 A/B（避免 Digital/Virtual 干扰）
            var analogCh = (cfg.Channels ?? new List<ChannelDef>())
                .Where(c => c != null && string.Equals(c.ChannelType, "Analog", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.ChannelIndex)
                .ToList();

            double[] aCoef = new double[aCount];
            double[] bCoef = new double[aCount];
            for (int k = 0; k < aCount; k++)
            {
                if (k < analogCh.Count)
                {
                    aCoef[k] = (double)(analogCh[k].A ?? 1m);
                    bCoef[k] = (double)(analogCh[k].B ?? 0m);
                }
                else
                {
                    aCoef[k] = 1d;
                    bCoef[k] = 0d;
                }
            }

            // 2) DataType / TimeMult（直接用 cfg.FormatType + cfg.TimeMul）
            DatDataType dt = ResolveDatDataType(cfg);
            double timeMult = ResolveTimeMultiplier(cfg);

            if (dt == DatDataType.ASCII)
            {
                return ParseAsciiDat(datBytes, cfg, aCoef, bCoef, timeMult, maxRecords);
            }

            ReadOnlySpan<byte> span = datBytes;

            // 3) 二进制解析：优先用 cfg.FormatType，只有 Unknown 才强探测
            (AnalogEncoding encFromCfg, int analogBytesPerSampleFromCfg) = GetAnalogEncodingFromCfg(dt);

            int dvWordsCeil = (dCount + 15) / 16;
            int dvWords = dvWordsCeil;
            int startOffset = 0;

            AnalogEncoding analogEnc = encFromCfg;
            int analogBytesPerSample = analogBytesPerSampleFromCfg;

            if (dt == DatDataType.Unknown)
            {
                var layout = DetectLayoutStrong(span, aCount, dCount, dvWordsHint: (dCount > 0 ? (int?)dvWordsCeil : 0), maxCheckRecords: 40);
                dvWords = layout.DvWords;
                startOffset = layout.StartOffset;
                analogEnc = layout.AnalogEncoding;
                analogBytesPerSample = layout.AnalogBytesPerSample;
            }

            var (dvWords2, recordSize2, totalRecords) = InspectDat(span, aCount, dCount, analogBytesPerSample, startOffset, dvWords);
            dvWords = dvWords2;
            int recordSize = recordSize2;

            int take = Math.Min(maxRecords, totalRecords);
            var rows = new List<DatRowAll>(capacity: take);

            int analogBytes = aCount * analogBytesPerSample;
            int digitalBytes = dvWords * 2;

            int offset = startOffset;

            for (int i = 0; i < take; i++)
            {
                if (offset + recordSize > span.Length) break;

                int sampleNoRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
                int timeRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + 4, 4));
                offset += 8;

                var analogVals = new double[aCount];
                int analogBase = offset;

                for (int k = 0; k < aCount; k++)
                {
                    int pos = analogBase + (k * analogBytesPerSample);
                    if (pos + analogBytesPerSample > analogBase + analogBytes) break;

                    double rawVal = analogEnc switch
                    {
                        AnalogEncoding.Int16 => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(pos, 2)),
                        AnalogEncoding.Int32 => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4)),
                        AnalogEncoding.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4))),
                        _ => 0d
                    };

                    analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                }

                offset += analogBytes;

                byte[] digitalWords = null;
                if (dvWords > 0)
                {
                    if (offset + digitalBytes > span.Length) break;
                    digitalWords = new byte[digitalBytes];
                    span.Slice(offset, digitalBytes).CopyTo(digitalWords);
                }
                offset += digitalBytes;

                long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                double timeMs = timeUs / 1000.0;

                rows.Add(new DatRowAll
                {
                    Index = i + 1,
                    SampleNo = sampleNoRaw,
                    TimeRaw = timeRaw,
                    TimeMs = timeMs,
                    Channels = analogVals,
                    DigitalWords = digitalWords
                });
            }

            return new WaveDataParseResult
            {
                Rows = rows,
                DigitalWords = dvWords,
                RecordSize = recordSize,
                TotalRecords = totalRecords
            };
        }

        // ==========================================================
        // ASCII DAT 解析（鹏城站关键）
        // ==========================================================
        private static WaveDataParseResult ParseAsciiDat(
            byte[] datBytes,
            CfgParseResult cfg,
            double[] aCoef,
            double[] bCoef,
            double timeMult,
            int maxRecords)
        {
            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            string text;
            try { text = System.Text.Encoding.ASCII.GetString(datBytes); }
            catch { text = System.Text.Encoding.GetEncoding("GBK").GetString(datBytes); }

            var rows = new List<DatRowAll>(capacity: Math.Min(256, maxRecords));
            using var sr = new StringReader(text);

            string line;
            int outIdx = 0;

            while (outIdx < maxRecords && (line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 去掉潜在的 \0 / 控制字符
                line = line.Trim().TrimEnd('\0');

                // 优先按逗号（标准 COMTRADE ASCII）
                string[] parts = line.Contains(',')
                    ? line.Split(',')
                    : line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries); // fallback：空白分隔的非标准

                int minCols = 2 + aCount;
                if (parts.Length < minCols) continue;

                if (!TryParseInt32Invariant(parts[0], out int sampleNo)) continue;
                if (!TryParseInt32Invariant(parts[1], out int timeRaw)) continue;

                var analogVals = new double[aCount];
                for (int k = 0; k < aCount; k++)
                {
                    int col = 2 + k;
                    if (col >= parts.Length) break;

                    if (TryParseDoubleInvariant(parts[col], out double rawVal))
                        analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                    else
                        analogVals[k] = 0d;
                }

                byte[] digitalWords = null;
                if (dCount > 0 && parts.Length >= 2 + aCount + dCount)
                {
                    digitalWords = PackAsciiDigitalBits(parts, startIndex: 2 + aCount, digitalCount: dCount);
                }

                long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                double timeMs = timeUs / 1000.0;

                rows.Add(new DatRowAll
                {
                    Index = outIdx + 1,
                    SampleNo = sampleNo,
                    TimeRaw = timeRaw,
                    TimeMs = timeMs,
                    Channels = analogVals,
                    DigitalWords = digitalWords
                });

                outIdx++;
            }

            int dvWords = (dCount + 15) / 16;

            return new WaveDataParseResult
            {
                Rows = rows,
                DigitalWords = dvWords,
                RecordSize = 0,
                TotalRecords = rows.Count
            };
        }

        private static bool TryParseInt32Invariant(string s, out int v)
        {
            s = (s ?? string.Empty).Trim();
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        private static bool TryParseDoubleInvariant(string s, out double v)
        {
            s = (s ?? string.Empty).Trim();
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);
        }

        private static byte[] PackAsciiDigitalBits(string[] parts, int startIndex, int digitalCount)
        {
            int dvWords = (digitalCount + 15) / 16;
            var bytes = new byte[dvWords * 2];

            int bitIndex = 0;
            for (int w = 0; w < dvWords; w++)
            {
                ushort word = 0;
                for (int b = 0; b < 16 && bitIndex < digitalCount; b++, bitIndex++)
                {
                    int col = startIndex + bitIndex;
                    int bit = 0;
                    if (col < parts.Length && TryParseInt32Invariant(parts[col], out int tmp) && tmp != 0)
                        bit = 1;

                    if (bit == 1) word |= (ushort)(1 << b);
                }

                bytes[w * 2] = (byte)(word & 0xFF);
                bytes[w * 2 + 1] = (byte)((word >> 8) & 0xFF);
            }

            return bytes;
        }
    }
}