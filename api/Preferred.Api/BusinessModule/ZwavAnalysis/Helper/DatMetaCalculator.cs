using System;
using System.IO;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
    public static class DatMetaCalculator
    {
          /// <summary>
        /// 等价于 inspectDat(buf, A, D)
        /// </summary>
        public static (int dvWords, int recordSize, int totalRecords) InspectDat(byte[] datBytes, int analogCount, int digitalCount)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));

            int dvWords = (digitalCount + 15) / 16; // ceil(D/16)
            int recordSize = 4 + 4 + (analogCount * 2) + (dvWords * 2);
            int totalRecords = recordSize <= 0 ? 0 : (datBytes.Length / recordSize);
            return (dvWords, recordSize, totalRecords);
        }

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

            var (dvWords, recordSize, totalRecords) = InspectDat(datBytes, cfg.AnalogCount, cfg.DigitalCount);

            int take = Math.Min(maxRecords, totalRecords);
            var rows = new List<DatRowAll>(capacity: take);

            int aCount = cfg.AnalogCount;
            int dCount = cfg.DigitalCount;

            // 预取系数：A 默认 1，B 默认 0
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

            ReadOnlySpan<byte> span = datBytes;

            int offset = 0;
            int analogBytes = aCount * 2;
            int digitalBytes = dvWords * 2;

            for (int i = 0; i < take; i++)
            {
                // 保护整条记录长度
                if (offset + recordSize > span.Length) break;

                int sampleNo = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
                offset += 4;

                int timeRaw = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
                offset += 4;

                // ===== 模拟量换算 =====
                var analogVals = new double[aCount];
                int analogBase = offset;
                for (int k = 0; k < aCount; k++)
                {
                    int pos = analogBase + (k * 2);
                    short raw = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(pos, 2));
                    analogVals[k] = aCoef[k] * raw + bCoef[k];
                }
                offset += analogBytes;

                // ===== 数字量：读取原始字节 + 计算每路 0/1 =====
                byte[] digitalWords = null;
                short[] digitalBits = null;

                if (dvWords > 0)
                {
                    if (offset + digitalBytes > span.Length) break;

                    // 原始数字字（用于落库 VARBINARY）
                    digitalWords = new byte[digitalBytes];
                    span.Slice(offset, digitalBytes).CopyTo(digitalWords);

                    // 计算每路开关量（0/1），长度 = cfg.DigitalCount
                    if (dCount > 0)
                    {
                        digitalBits = new short[dCount];

                        // 逐 word（16bit）展开
                        int bitIndex = 0;
                        for (int w = 0; w < dvWords && bitIndex < dCount; w++)
                        {
                            int bi = (w * 2);

                            // 小端拼 word
                            ushort word = (ushort)(digitalWords[bi] | (digitalWords[bi + 1] << 8));

                            for (int b = 0; b < 16 && bitIndex < dCount; b++, bitIndex++)
                            {
                                digitalBits[bitIndex] = (short)(((word >> b) & 1) == 1 ? 1 : 0);
                            }
                        }
                    }
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
            };
        }
    }
}