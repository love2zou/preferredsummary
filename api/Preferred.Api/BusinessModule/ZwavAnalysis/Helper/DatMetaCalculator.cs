using System;
using System.IO;
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

        /// <summary>
        /// .NET Core 3.1：解析 DAT 前 N 条记录，但解析“全部模拟通道”（不限制前三路）。
        /// 可选 includeDigitals=true 则返回每条记录的数字字（ushort[]）。
        /// </summary>
        public static WaveDataParseResult ParseDatAllChannels(byte[] datBytes, CfgParseResult cfg, int maxRecords = 20, bool includeDigitals = false)
        {
            if (datBytes == null) throw new ArgumentNullException(nameof(datBytes));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (cfg.AnalogCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.AnalogCount));
            if (cfg.DigitalCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.DigitalCount));
            if (maxRecords <= 0) maxRecords = 1;

            var (dvWords, recordSize, totalRecords) = InspectDat(datBytes, cfg.AnalogCount, cfg.DigitalCount);

            int take = Math.Min(maxRecords, totalRecords);
            var rows = new List<DatRowAll>(capacity: take);

            // 预取系数，避免循环内多次判空
            double[] aCoef = new double[cfg.AnalogCount];
            double[] bCoef = new double[cfg.AnalogCount];
            for (int k = 0; k < cfg.AnalogCount; k++)
            {
                // JS: cfg.Channels[k]?.a ?? 1; cfg.Channels[k]?.b ?? 0;
                if (cfg.Channels != null && k < cfg.Channels.Count && cfg.Channels[k] != null)
                {
                    aCoef[k] = (double)(cfg.Channels[k].A ?? 0m);
                    bCoef[k] = (double)(cfg.Channels[k].B ?? 0m);
                }
                else
                {
                    aCoef[k] = (double)(cfg.Channels[k].A ?? 0m);
                    bCoef[k] = (double)(cfg.Channels[k].B ?? 0m);
                }
            }

            // 小端读取（与你 JS 的 getInt32(..., true) / getInt16(..., true) 一致）:contentReference[oaicite:2]{index=2}
            ReadOnlySpan<byte> span = datBytes;

            int offset = 0;
            for (int i = 0; i < take; i++)
            {
                // 边界保护：避免异常数据导致越界
                if (offset + recordSize > span.Length) break;

                int sampleNo = BitConverter.ToInt32(span.Slice(offset, 4));
                if (BitConverter.IsLittleEndian == false)
                {
                    // 如果不是小端，则手动反转字节序
                    byte[] tmp = span.Slice(offset, 4).ToArray();
                    Array.Reverse(tmp);
                    sampleNo = BitConverter.ToInt32(tmp, 0);
                }
                offset += 4;

                int timeRaw = BitConverter.ToInt32(span.Slice(offset, 4));
                if (BitConverter.IsLittleEndian == false)
                {
                    // 如果不是小端，则手动反转字节序
                    byte[] tmp = span.Slice(offset, 4).ToArray();
                    Array.Reverse(tmp);
                    timeRaw = BitConverter.ToInt32(tmp, 0);
                }
                offset += 4;

                // 全部模拟量
                var analogVals = new double[cfg.AnalogCount];
                for (int k = 0; k < cfg.AnalogCount; k++)
                {
                    short raw = BitConverter.ToInt16(span.Slice(offset + k * 2, 2));
                    if (BitConverter.IsLittleEndian == false)
                    {
                        // 如果不是小端，则手动反转字节序
                        byte[] tmp = span.Slice(offset + k * 2, 2).ToArray();
                        Array.Reverse(tmp);
                        raw = BitConverter.ToInt16(tmp, 0);
                    }
                    analogVals[k] = aCoef[k] * raw + bCoef[k];
                }
                offset += cfg.AnalogCount * 2;

                // 数字量：按 dvWords 跳过（可选读取）
                short[] digitals = null;
                if (includeDigitals && dvWords > 0)
                {
                    digitals = new short[dvWords];
                    for (int w = 0; w < dvWords; w++)
                    {
                        // JS 用 getInt16 读取并跳过；这里用 ushort 更贴近“16位字”
                        digitals[w] = BitConverter.ToInt16(span.Slice(offset + w * 2, 2));
                    }
                }
                offset += dvWords * 2;

                rows.Add(new DatRowAll
                {
                    Index = i + 1,
                    SampleNo = sampleNo,
                    TimeRaw = timeRaw,
                    Channels = analogVals,
                    Digitals = digitals
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