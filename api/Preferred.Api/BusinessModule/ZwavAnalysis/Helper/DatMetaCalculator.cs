using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Zwav.Application.Parsing
{
    /// <summary>
    /// DAT 解析工具：
    /// 1) ParseDatAllChannels(byte[]...)：用于“预览/抽样”（保留 maxRecords）
    /// 2) ParseDatAllChannelsStream(Stream...)：用于“全量入库/全量扫描”（批回调）
    /// </summary>
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

        public sealed class WaveDataBatch
        {
            public List<DatRowAll> Rows { get; set; }
            public int DigitalWords { get; set; }
            public int RecordSize { get; set; }
            public long TotalRecords { get; set; }          // ASCII 下可能为 -1（未知）
            public long BatchStartIndex { get; set; }
            public int BatchCount { get; set; }
        }

        // ---------------------------
        // Public: 全量（流式、批回调）
        // ---------------------------
        public static void ParseDatAllChannelsStream(
            Stream datStream,
            CfgParseResult cfg,
            int batchSize,
            Action<WaveDataBatch> onBatch)
        {
            if (datStream == null) throw new ArgumentNullException(nameof(datStream));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (onBatch == null) throw new ArgumentNullException(nameof(onBatch));
            if (!datStream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(datStream));
            if (batchSize <= 0) batchSize = 5000;

            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;
            if (aCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.AnalogCount));
            if (dCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.DigitalCount));

            // 系数：只取 Analog 通道的 A/B
            GetAnalogCoefficients(cfg, aCount, out var aCoef, out var bCoef);

            // DataType / TimeMul（增强：会 probe 头部判断 ASCII）
            DatDataType dt = ResolveDatDataTypeStrong(datStream, cfg);
            double timeMult = ResolveTimeMultiplier(cfg);

            // ASCII：全量流式逐行解析（已实现）
            if (dt == DatDataType.ASCII)
            {
                ParseAsciiDatStream(datStream, cfg, aCoef, bCoef, timeMult, batchSize, onBatch);
                return;
            }

            // 二进制：优先用 cfg.FormatType，Unknown 才探测布局
            (AnalogEncoding analogEnc, int analogBytesPerSample) = GetAnalogEncodingFromCfg(dt);

            int dvWordsCeil = (dCount + 15) / 16;
            int dvWords = dvWordsCeil;
            int startOffset = 0;

            if (dt == DatDataType.Unknown)
            {
                ReadOnlySpan<byte> head = ReadProbe(datStream, maxBytes: 1024 * 1024);

                var layout = DetectLayoutStrong(
                    head,
                    aCount,
                    dCount,
                    dvWordsHint: dCount > 0 ? (int?)dvWordsCeil : 0,
                    maxCheckRecords: 40);

                dvWords = layout.DvWords;
                startOffset = layout.StartOffset;
                analogEnc = layout.AnalogEncoding;
                analogBytesPerSample = layout.AnalogBytesPerSample;
            }

            // 计算 recordSize / totalRecords（基于 stream.Length）
            var meta = InspectDat(datStream, aCount, dCount, analogBytesPerSample, startOffset, dvWords);
            dvWords = meta.dvWords;
            int recordSize = meta.recordSize;
            long totalRecords = meta.totalRecords;

            int analogBytes = aCount * analogBytesPerSample;
            int digitalBytes = dvWords * 2;

            // 定位到 startOffset 并开始解析
            datStream.Position = startOffset;

            // 批缓冲：一次读 N 条记录
            checked { _ = batchSize * recordSize; }
            byte[] buf = new byte[batchSize * recordSize];

            long globalIndex = 0;

            // 自愈：当 sampleNo 不递增时改用序号
            bool normalizeSampleNo = false;
            int lastSample = 0;
            int badSampleCount = 0;

            while (globalIndex < totalRecords)
            {
                int take = (int)Math.Min(batchSize, totalRecords - globalIndex);
                int bytesToRead = take * recordSize;

                ReadExactly(datStream, buf, bytesToRead, globalIndex);

                var rows = new List<DatRowAll>(take);
                int offset = 0;

                for (int i = 0; i < take; i++)
                {
                    int sampleNoRaw = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset, 4));
                    int timeRaw = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset + 4, 4));
                    int analogBase = offset + 8;

                    // 早期判断 sampleNo 是否可靠
                    if (!normalizeSampleNo)
                    {
                        if (globalIndex == 0 && i == 0)
                        {
                            lastSample = sampleNoRaw;
                        }
                        else
                        {
                            if (sampleNoRaw != lastSample + 1) badSampleCount++;
                            lastSample = sampleNoRaw;

                            // 只要出现多次不递增，就认为该文件 sampleNo 字段布局不可信（常见：偏移/字节宽度不匹配）
                            if (badSampleCount >= 3) normalizeSampleNo = true;
                        }
                    }

                    int sampleNo = normalizeSampleNo ? (int)Math.Min(int.MaxValue, globalIndex + i + 1) : sampleNoRaw;

                    var analogVals = new double[aCount];
                    for (int k = 0; k < aCount; k++)
                    {
                        int pos = analogBase + k * analogBytesPerSample;

                        double rawVal = analogEnc switch
                        {
                            AnalogEncoding.Int16 => BinaryPrimitives.ReadInt16LittleEndian(buf.AsSpan(pos, 2)),
                            AnalogEncoding.Int32 => BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(pos, 4)),
                            AnalogEncoding.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(pos, 4))),
                            _ => 0d
                        };

                        analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                    }

                    byte[] digitalWords = null;
                    if (dvWords > 0)
                    {
                        int digitalBase = analogBase + analogBytes;
                        digitalWords = new byte[digitalBytes];
                        Buffer.BlockCopy(buf, digitalBase, digitalWords, 0, digitalBytes);
                    }

                    long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                    double timeMs = timeUs / 1000.0;

                    rows.Add(new DatRowAll
                    {
                        Index = (int)Math.Min(int.MaxValue, globalIndex + i + 1),
                        SampleNo = sampleNo,
                        TimeRaw = timeRaw,
                        TimeMs = timeMs,
                        Channels = analogVals,
                        DigitalWords = digitalWords
                    });

                    offset += recordSize;
                }

                onBatch(new WaveDataBatch
                {
                    Rows = rows,
                    RecordSize = recordSize,
                    DigitalWords = dvWords,
                    TotalRecords = totalRecords,
                    BatchStartIndex = globalIndex,
                    BatchCount = take
                });

                globalIndex += take;
            }
        }

        // ---------------------------
        // Public: 元信息（流式）
        // ---------------------------
        public static (int dvWords, int recordSize, long totalRecords) InspectDat(
            Stream datStream,
            int analogCount,
            int digitalCount,
            int analogBytesPerSample,
            int startOffset,
            int dvWordsOverride)
        {
            if (datStream == null) throw new ArgumentNullException(nameof(datStream));
            if (!datStream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(datStream));

            int dvWords = dvWordsOverride >= 0 ? dvWordsOverride : (digitalCount + 15) / 16;
            int recordSize = 8 + (analogCount * analogBytesPerSample) + (dvWords * 2);

            long available = Math.Max(0, datStream.Length - Math.Max(0, startOffset));
            long totalRecords = recordSize <= 0 ? 0 : (available / recordSize);

            return (dvWords, recordSize, totalRecords);
        }

        // ---------------------------
        // Public: 预览/抽样（byte[]）
        // ---------------------------
        public static WaveDataParseResult ParseDatAllChannels(byte[] datBytes, CfgParseResult cfg, int maxRecords = 20)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (cfg.AnalogCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.AnalogCount));
            if (cfg.DigitalCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.DigitalCount));
            if (maxRecords <= 0) maxRecords = 1;

            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            GetAnalogCoefficients(cfg, aCount, out var aCoef, out var bCoef);

            DatDataType dt = ResolveDatDataType(cfg);
            double timeMult = ResolveTimeMultiplier(cfg);

            // 预览：如果 cfg 不准，也做一次“头部探测”
            if (dt == DatDataType.Unknown)
            {
                if (LooksLikeAscii(datBytes.AsSpan(0, Math.Min(datBytes.Length, 256 * 1024))))
                    dt = DatDataType.ASCII;
            }

            if (dt == DatDataType.ASCII)
                return ParseAsciiDat(datBytes, cfg, aCoef, bCoef, timeMult, maxRecords);

            ReadOnlySpan<byte> span = datBytes;

            (AnalogEncoding analogEnc, int analogBytesPerSample) = GetAnalogEncodingFromCfg(dt);

            int dvWordsCeil = (dCount + 15) / 16;
            int dvWords = dvWordsCeil;
            int startOffset = 0;

            if (dt == DatDataType.Unknown)
            {
                var layout = DetectLayoutStrong(span, aCount, dCount,
                    dvWordsHint: dCount > 0 ? (int?)dvWordsCeil : 0,
                    maxCheckRecords: 40);

                dvWords = layout.DvWords;
                startOffset = layout.StartOffset;
                analogEnc = layout.AnalogEncoding;
                analogBytesPerSample = layout.AnalogBytesPerSample;
            }

            var meta = InspectDat(span, aCount, dCount, analogBytesPerSample, startOffset, dvWords);
            dvWords = meta.dvWords;
            int recordSize = meta.recordSize;
            int totalRecords = meta.totalRecords;

            int take = Math.Min(maxRecords, totalRecords);
            var rows = new List<DatRowAll>(take);

            int analogBytes = aCount * analogBytesPerSample;
            int digitalBytes = dvWords * 2;

            int offset = startOffset;

            bool normalizeSampleNo = false;
            int lastSample = 0;
            int badSampleCount = 0;

            for (int i = 0; i < take; i++)
            {
                if (offset + recordSize > span.Length) break;

                int sampleNoRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
                int timeRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + 4, 4));
                int analogBase = offset + 8;

                if (!normalizeSampleNo)
                {
                    if (i == 0) lastSample = sampleNoRaw;
                    else
                    {
                        if (sampleNoRaw != lastSample + 1) badSampleCount++;
                        lastSample = sampleNoRaw;
                        if (badSampleCount >= 3) normalizeSampleNo = true;
                    }
                }

                int sampleNo = normalizeSampleNo ? (i + 1) : sampleNoRaw;

                var analogVals = new double[aCount];
                for (int k = 0; k < aCount; k++)
                {
                    int pos = analogBase + k * analogBytesPerSample;

                    double rawVal = analogEnc switch
                    {
                        AnalogEncoding.Int16 => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(pos, 2)),
                        AnalogEncoding.Int32 => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4)),
                        AnalogEncoding.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos, 4))),
                        _ => 0d
                    };

                    analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                }

                byte[] digitalWords = null;
                if (dvWords > 0)
                {
                    int digitalBase = analogBase + analogBytes;
                    if (digitalBase + digitalBytes > span.Length) break;

                    digitalWords = new byte[digitalBytes];
                    span.Slice(digitalBase, digitalBytes).CopyTo(digitalWords);
                }

                long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                double timeMs = timeUs / 1000.0;

                rows.Add(new DatRowAll
                {
                    Index = i + 1,
                    SampleNo = sampleNo,
                    TimeRaw = timeRaw,
                    TimeMs = timeMs,
                    Channels = analogVals,
                    DigitalWords = digitalWords
                });

                offset += recordSize;
            }

            return new WaveDataParseResult
            {
                Rows = rows,
                DigitalWords = dvWords,
                RecordSize = recordSize,
                TotalRecords = totalRecords
            };
        }

        // ---------------------------
        // DetectLayoutStrong（保持原逻辑）
        // ---------------------------
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

            int dvCeil = (digitalCount + 15) / 16;
            int dvMin, dvMax;

            if (digitalCount == 0) { dvMin = 0; dvMax = 0; }
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
                Reason = "No layout matched."
            };
        }

        // ---------------------------
        // Private: InspectDat（byte[]）
        // ---------------------------
        private static (int dvWords, int recordSize, int totalRecords) InspectDat(
            ReadOnlySpan<byte> datBytes, int analogCount, int digitalCount, int analogBytesPerSample, int startOffset, int dvWordsOverride)
        {
            int dvWords = dvWordsOverride >= 0 ? dvWordsOverride : (digitalCount + 15) / 16;
            int recordSize = 8 + (analogCount * analogBytesPerSample) + (dvWords * 2);

            int available = Math.Max(0, datBytes.Length - Math.Max(0, startOffset));
            int totalRecords = recordSize <= 0 ? 0 : (available / recordSize);

            return (dvWords, recordSize, totalRecords);
        }

        private static void GetAnalogCoefficients(CfgParseResult cfg, int aCount, out double[] aCoef, out double[] bCoef)
        {
            var analogCh = (cfg.Channels ?? new List<ChannelDef>())
                .Where(c => c != null && string.Equals(c.ChannelType, "Analog", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.ChannelIndex)
                .ToList();

            aCoef = new double[aCount];
            bCoef = new double[aCount];

            for (int k = 0; k < aCount; k++)
            {
                if (k < analogCh.Count)
                {
                    aCoef[k] = (double)(analogCh[k].A ?? 1m);
                    bCoef[k] = (double)(analogCh[k].B ?? 0m);
                }
                else { aCoef[k] = 1d; bCoef[k] = 0d; }
            }
        }

        private static (AnalogEncoding enc, int bytesPerSample) GetAnalogEncodingFromCfg(DatDataType dt) => dt switch
        {
            DatDataType.BINARY => (AnalogEncoding.Int16, 2),
            DatDataType.BINARY32 => (AnalogEncoding.Int32, 4),
            DatDataType.FLOAT32 => (AnalogEncoding.Float32, 4),
            _ => (AnalogEncoding.Int16, 2)
        };

        private static DatDataType ResolveDatDataType(CfgParseResult cfg)
        {
            var s = (cfg?.FormatType ?? string.Empty).Trim().ToUpperInvariant();

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

        /// <summary>
        /// 增强：当 cfg.FormatType 不可靠时，通过 dat 头部 probe 判断 ASCII
        /// </summary>
        private static DatDataType ResolveDatDataTypeStrong(Stream datStream, CfgParseResult cfg)
        {
            var dt = ResolveDatDataType(cfg);
            if (dt != DatDataType.Unknown) return dt;

            ReadOnlySpan<byte> head = ReadProbe(datStream, maxBytes: 256 * 1024);
            if (LooksLikeAscii(head)) return DatDataType.ASCII;

            return DatDataType.Unknown;
        }

        private static double ResolveTimeMultiplier(CfgParseResult cfg)
        {
            if (cfg?.TimeMul is decimal m && m > 0) return (double)m;
            return 1d;
        }

        private static ReadOnlySpan<byte> ReadProbe(Stream s, int maxBytes)
        {
            long oldPos = s.Position;
            try
            {
                s.Position = 0;
                int toRead = (int)Math.Min(maxBytes, s.Length);
                byte[] buf = new byte[toRead];

                int read = 0;
                while (read < toRead)
                {
                    int n = s.Read(buf, read, toRead - read);
                    if (n == 0) break;
                    read += n;
                }

                return buf.AsSpan(0, read);
            }
            finally
            {
                s.Position = oldPos;
            }
        }

        private static void ReadExactly(Stream s, byte[] buffer, int bytesToRead, long recordIndexForError)
        {
            int readTotal = 0;
            while (readTotal < bytesToRead)
            {
                int n = s.Read(buffer, readTotal, bytesToRead - readTotal);
                if (n == 0) throw new EndOfStreamException($"Unexpected EOF at record {recordIndexForError}.");
                readTotal += n;
            }
        }

        // ---------------------------
        // ASCII：全量流式（新增）
        // ---------------------------
        private static void ParseAsciiDatStream(
            Stream datStream,
            CfgParseResult cfg,
            double[] aCoef,
            double[] bCoef,
            double timeMult,
            int batchSize,
            Action<WaveDataBatch> onBatch)
        {
            // 归零，从头读
            datStream.Position = 0;

            // 编码：先尝试 UTF8（带 BOM），失败再回退 GBK
            // 实战里 DAT ASCII 很多就是纯 ASCII/ANSI；这里用“尽量不抛异常”的方式处理。
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(datStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256 * 1024, leaveOpen: true);
            }
            catch
            {
                sr = new StreamReader(datStream, Encoding.GetEncoding("GBK"), detectEncodingFromByteOrderMarks: false, bufferSize: 256 * 1024, leaveOpen: true);
            }

            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            long globalIndex = 0;
            var rows = new List<DatRowAll>(batchSize);

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                line = line.Trim().TrimEnd('\0');

                // 分隔：逗号优先，否则空白
                string[] parts = line.Contains(',')
                    ? line.Split(',')
                    : line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                int minCols = 2 + aCount;
                if (parts.Length < minCols) continue;

                if (!TryParseInt32Invariant(parts[0], out int sampleNoRaw)) continue;
                if (!TryParseInt32Invariant(parts[1], out int timeRaw)) continue;

                // ASCII 下 sampleNo 也可能不从 1 开始：保持原值，但 Index 用 globalIndex+1
                int sampleNo = sampleNoRaw;
                if (sampleNo <= 0) sampleNo = (int)Math.Min(int.MaxValue, globalIndex + 1);

                var analogVals = new double[aCount];
                for (int k = 0; k < aCount; k++)
                {
                    int col = 2 + k;
                    if (col >= parts.Length) break;

                    if (TryParseDoubleInvariant(parts[col], out double rawVal))
                        analogVals[k] = aCoef[k] * rawVal + bCoef[k];
                }

                byte[] digitalWords = null;
                if (dCount > 0)
                {
                    int digitalStart = 2 + aCount;
                    if (parts.Length >= digitalStart + dCount)
                        digitalWords = PackAsciiDigitalBits(parts, digitalStart, dCount);
                }

                long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                double timeMs = timeUs / 1000.0;

                rows.Add(new DatRowAll
                {
                    Index = (int)Math.Min(int.MaxValue, globalIndex + 1),
                    SampleNo = sampleNo,
                    TimeRaw = timeRaw,
                    TimeMs = timeMs,
                    Channels = analogVals,
                    DigitalWords = digitalWords
                });

                globalIndex++;

                if (rows.Count >= batchSize)
                {
                    onBatch(new WaveDataBatch
                    {
                        Rows = rows,
                        RecordSize = 0, // ASCII 无固定 recordSize
                        DigitalWords = (dCount + 15) / 16,
                        TotalRecords = -1, // ASCII 全量未知；如需可二次计数
                        BatchStartIndex = globalIndex - rows.Count,
                        BatchCount = rows.Count
                    });

                    rows = new List<DatRowAll>(batchSize);
                }
            }

            if (rows.Count > 0)
            {
                onBatch(new WaveDataBatch
                {
                    Rows = rows,
                    RecordSize = 0,
                    DigitalWords = (dCount + 15) / 16,
                    TotalRecords = globalIndex, // 最后一批时可给出已解析总数
                    BatchStartIndex = globalIndex - rows.Count,
                    BatchCount = rows.Count
                });
            }
        }

        // ---------------------------
        // DetectLayoutStrong 的评分（原逻辑）
        // ---------------------------
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
            int timeIncOk = 0;

            int analogFiniteOk = 0;
            int analogCrazy = 0;

            int digitalNotAllSame = 0;

            var dtList = new List<int>(checkRecords);

            int firstSample = 0;
            int firstTime = 0;

            for (int i = 0; i < checkRecords; i++)
            {
                if (off + 8 > dat.Length) break;

                int sample = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(off, 4));
                int time = BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(off + 4, 4));

                if (i == 0) { firstSample = sample; firstTime = time; }

                if (i > 0)
                {
                    if (sample == lastSample + 1) sampleIncOk++;
                    int dt = time - lastTime;
                    if (dt > 0) { timeIncOk++; dtList.Add(dt); }
                }

                if (analogCount > 0)
                {
                    int analogBase = off + 8;
                    int[] ks = analogCount >= 3 ? new[] { 0, 1, analogCount - 1 } : Enumerable.Range(0, analogCount).ToArray();

                    foreach (var k in ks)
                    {
                        int pos = analogBase + k * analogBytesPerSample;
                        if (pos + analogBytesPerSample > off + recordSize) break;

                        double val = enc switch
                        {
                            AnalogEncoding.Int16 => BinaryPrimitives.ReadInt16LittleEndian(dat.Slice(pos, 2)),
                            AnalogEncoding.Int32 => BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(pos, 4)),
                            _ => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(dat.Slice(pos, 4)))
                        };

                        if (double.IsNaN(val) || double.IsInfinity(val)) analogCrazy++;
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

            if (firstSample == 1) score += 20;
            if (firstTime == 0) score += 10;

            if (dtList.Count >= 8)
            {
                dtList.Sort();
                int median = dtList[dtList.Count / 2];
                int tol = Math.Max(2, median / 50);
                int stable = 0;
                foreach (var dt in dtList) if (Math.Abs(dt - median) <= tol) stable++;
                score += stable * 2;
            }

            score += analogFiniteOk;
            score -= analogCrazy * 8;
            score += digitalNotAllSame;

            reason = $"sampleIncOk={sampleIncOk}, timeIncOk={timeIncOk}, analogFiniteOk={analogFiniteOk}, analogCrazy={analogCrazy}, digitalNotAllSame={digitalNotAllSame}";
            return score;
        }

        // ---------------------------
        // ASCII 预览解析（你原逻辑保留）
        // ---------------------------
        private static WaveDataParseResult ParseAsciiDat(byte[] datBytes, CfgParseResult cfg, double[] aCoef, double[] bCoef, double timeMult, int maxRecords)
        {
            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            string text;
            try { text = Encoding.UTF8.GetString(datBytes); }
            catch { text = Encoding.GetEncoding("GBK").GetString(datBytes); }

            var rows = new List<DatRowAll>(Math.Min(256, maxRecords));
            using var sr = new StringReader(text);

            string line;
            int outIdx = 0;

            while (outIdx < maxRecords && (line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                line = line.Trim().TrimEnd('\0');

                string[] parts = line.Contains(',')
                    ? line.Split(',')
                    : line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

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
                }

                byte[] digitalWords = null;
                if (dCount > 0 && parts.Length >= 2 + aCount + dCount)
                    digitalWords = PackAsciiDigitalBits(parts, 2 + aCount, dCount);

                long timeUs = (long)Math.Round(timeRaw * timeMult, MidpointRounding.AwayFromZero);
                double timeMs = timeUs / 1000.0;

                rows.Add(new DatRowAll
                {
                    Index = outIdx + 1,
                    SampleNo = sampleNo <= 0 ? outIdx + 1 : sampleNo,
                    TimeRaw = timeRaw,
                    TimeMs = timeMs,
                    Channels = analogVals,
                    DigitalWords = digitalWords
                });

                outIdx++;
            }

            return new WaveDataParseResult
            {
                Rows = rows,
                DigitalWords = (dCount + 15) / 16,
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

        // ---------------------------
        // ASCII 探测（新增）
        // ---------------------------
        private static bool LooksLikeAscii(ReadOnlySpan<byte> head)
        {
            if (head.Length < 64) return false;

            int printable = 0;
            int zeros = 0;
            int newlines = 0;
            int commas = 0;
            int spaces = 0;

            int n = Math.Min(head.Length, 128 * 1024);
            for (int i = 0; i < n; i++)
            {
                byte b = head[i];
                if (b == 0) { zeros++; continue; }

                if (b == (byte)'\n' || b == (byte)'\r') newlines++;
                if (b == (byte)',') commas++;
                if (b == (byte)' ' || b == (byte)'\t') spaces++;

                // 可打印字符（含常见控制符：\t \r \n）
                if (b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126)) printable++;
            }

            // 二进制 DAT 常有大量 0；ASCII 一般 0 很少
            if (zeros > n / 20) return false;

            double printableRatio = printable / (double)n;

            // 至少有换行；且可打印比例足够高；且存在分隔符特征（逗号或空白）
            if (newlines >= 2 && printableRatio >= 0.92 && (commas > 2 || spaces > 10))
                return true;

            return false;
        }
    }
}