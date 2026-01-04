using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
    public static class DatMetaCalculator
    {
        public enum AnalogEncoding
        {
            Int16,
            Int32,
            Float32
        }

        public sealed class DatLayoutInfo
        {
            public int StartOffset { get; set; }
            public AnalogEncoding AnalogEncoding { get; set; }
            public int AnalogBytesPerSample { get; set; }  // 2 or 4
            public int DvWords { get; set; }               // number of 16-bit words
            public int RecordSize { get; set; }            // bytes per record
            public int Score { get; set; }                 // diagnostic
            public string Reason { get; set; }             // diagnostic
        }

        // =========================
        // 你已有的 InspectDat（保留）
        // 注意：你原 InspectDat 返回顺序是 (dvWords, recordSize, totalRecords)
        // =========================
        public static (int dvWords, int recordSize, int totalRecords) InspectDat(
            byte[] datBytes, int analogCount, int digitalCount)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));

            int dvWords = (digitalCount + 15) / 16; // ceil(D/16)
            int recordSize = 4 + 4 + (analogCount * 2) + (dvWords * 2);
            int totalRecords = recordSize <= 0 ? 0 : (datBytes.Length / recordSize);
            return (dvWords, recordSize, totalRecords);
        }

        // 强化版 Inspect（可选：按 analogBytesPerSample 计算 recordSize）
        private static (int dvWords, int recordSize, int totalRecords) InspectDat(
            ReadOnlySpan<byte> datBytes, int analogCount, int digitalCount, int analogBytesPerSample, int startOffset, int dvWordsOverride)
        {
            int dvWords = dvWordsOverride >= 0 ? dvWordsOverride : (digitalCount + 15) / 16;
            int recordSize = 4 + 4 + (analogCount * analogBytesPerSample) + (dvWords * 2);

            int available = Math.Max(0, datBytes.Length - Math.Max(0, startOffset));
            int totalRecords = recordSize <= 0 ? 0 : (available / recordSize);

            return (dvWords, recordSize, totalRecords);
        }

        /// <summary>
        /// 更强的布局探测：起始偏移 + 模拟量编码 + dvWords 搜索。
        /// </summary>
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

            int[] offsets =
            {
                0,2,4,6,8,10,12,14,16,20,24,28,32,40,48,56,64,96,128,256
            };

            var encodings = new[]
            {
                AnalogEncoding.Int16,
                AnalogEncoding.Int32,
                AnalogEncoding.Float32
            };

            int dvMin, dvMax;
            int dvCeil = (digitalCount + 15) / 16;

            if (digitalCount == 0)
            {
                dvMin = 0;
                dvMax = 0;
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

                    // 粗过滤：过大 record 直接跳过
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

            if (best == null)
            {
                best = new DatLayoutInfo
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

            return best;
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

                // 模拟量粗验（抽样少量通道）
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

                // 数字字粗验
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
        // 这是你要的：ParseDatAllChannels —— 完整代码（强 DetectLayout 用法）
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

            ReadOnlySpan<byte> span = datBytes;

            // ===== 1) 预取系数：A 默认 1，B 默认 0 =====
            double[] aCoef = new double[aCount];
            double[] bCoef = new double[aCount];
            for (int k = 0; k < aCount; k++)
            {
                if (cfg.Channels != null && k < cfg.Channels.Count && cfg.Channels[k] != null)
                {
                    aCoef[k] = (double)(cfg.Channels[k].A ?? 1m);
                    bCoef[k] = (double)(cfg.Channels[k].B ?? 0m);
                }
                else
                {
                    aCoef[k] = 1d;
                    bCoef[k] = 0d;
                }
            }

            // ===== 2) 强布局探测 =====
            int dvHint = (dCount + 15) / 16;
            var layout = DetectLayoutStrong(span, aCount, dCount, dvWordsHint: (dCount > 0 ? (int?)dvHint : 0), maxCheckRecords: 40);

            int dvWords = layout.DvWords;
            int recordSize = layout.RecordSize;
            int startOffset = layout.StartOffset;
            int analogBytesPerSample = layout.AnalogBytesPerSample;
            var analogEnc = layout.AnalogEncoding;

            // ===== 3) 计算记录数 =====
            var (dvWords2, recordSize2, totalRecords) = InspectDat(span, aCount, dCount, analogBytesPerSample, startOffset, dvWords);
            // 强制以探测结果为准（防止 Inspect 与探测不一致）
            dvWords = dvWords2;
            recordSize = recordSize2;

            int take = Math.Min(maxRecords, totalRecords);
            var rows = new List<DatRowAll>(capacity: take);

            int analogBytes = aCount * analogBytesPerSample;
            int digitalBytes = dvWords * 2;

            // ===== 4) 逐条解析 =====
            int offset = startOffset;

            for (int i = 0; i < take; i++)
            {
                if (offset + recordSize > span.Length) break;

                // sampleNo/timeRaw：依旧按小端 int32 读
                int sampleNoRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
                int timeRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + 4, 4));
                offset += 8;

                // 业务需求：样本号按解析序号从 1 开始
                int sampleNo = i + 1;

                // ===== 模拟量换算 =====
                var analogVals = new double[aCount];
                int analogBase = offset;

                for (int k = 0; k < aCount; k++)
                {
                    int pos = analogBase + (k * analogBytesPerSample);
                    if (pos + analogBytesPerSample > analogBase + analogBytes) break;

                    double rawVal;

                    switch (analogEnc)
                    {
                        case AnalogEncoding.Int16:
                            rawVal = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(pos, 2));
                            break;

                        case AnalogEncoding.Int32:
                            rawVal = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4));
                            break;

                        case AnalogEncoding.Float32:
                            {
                                int bits = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4));
                                rawVal = BitConverter.Int32BitsToSingle(bits);
                                break;
                            }

                        default:
                            rawVal = 0d;
                            break;
                    }

                    analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                }

                offset += analogBytes;

                // ===== 数字量：读取原始字节（用于落库 VARBINARY）=====
                byte[] digitalWords = null;

                if (dvWords > 0)
                {
                    if (offset + digitalBytes > span.Length) break;

                    digitalWords = new byte[digitalBytes];
                    span.Slice(offset, digitalBytes).CopyTo(digitalWords);

                    // 如你后续确实需要 digitalBits(0/1)，建议放到前端或按需计算，避免这里每条都展开影响性能
                    // short[] digitalBits = ExpandDigitalBits(digitalWords, dCount);
                }

                offset += digitalBytes;

                rows.Add(new DatRowAll
                {
                    Index = i + 1,
                    SampleNo = sampleNo,
                    TimeRaw = timeRaw,
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
                // 你若希望把 layout 诊断信息带出去，可在 WaveDataParseResult 新增字段
                // LayoutScore = layout.Score,
                // LayoutReason = layout.Reason,
                // LayoutStartOffset = layout.StartOffset,
                // AnalogEncoding = layout.AnalogEncoding.ToString(),
            };
        }

        // （可选）如果你需要展开 digitalBits，可提供工具函数
        private static short[] ExpandDigitalBits(byte[] digitalWords, int digitalCount)
        {
            if (digitalWords == null || digitalWords.Length == 0 || digitalCount <= 0) return Array.Empty<short>();

            int dvWords = digitalWords.Length / 2;
            var bits = new short[digitalCount];

            int bitIndex = 0;
            for (int w = 0; w < dvWords && bitIndex < digitalCount; w++)
            {
                int bi = w * 2;
                ushort word = (ushort)(digitalWords[bi] | (digitalWords[bi + 1] << 8));

                for (int b = 0; b < 16 && bitIndex < digitalCount; b++, bitIndex++)
                {
                    bits[bitIndex] = (short)(((word >> b) & 1) == 1 ? 1 : 0);
                }
            }
            return bits;
        }
    }
}