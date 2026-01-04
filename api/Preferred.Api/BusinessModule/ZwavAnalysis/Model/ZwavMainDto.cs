using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
    public sealed class UploadZwavFileResponse
    {
        public int FileId { get; set; }
        public string OriginalName { get; set; }
        public string StoragePath { get; set; }
        public long FileSizeBytes { get; set; }   // 建议对外用 bytes
        public string Ext { get; set; }
        public DateTime CrtTimeUtc { get; set; }
    }

    public sealed class UploadZwavFileResult
    {
        public int FileId { get; set; }
        public string StoragePath { get; set; }
        public string OriginalName { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CrtTimeUtc { get; set; }
    }

    public sealed class CreateAnalysisByFileIdRequest
    {
        public int FileId { get; set; }

        // 可选：是否强制重新解析（若该文件已有解析任务）
        public bool ForceRecreate { get; set; } = false;
    }
    //队列消息模型
    public sealed class ZwavAnalysisQueueItem
    {
        public string AnalysisGuid { get; set; }
        public int FileId { get; set; }
        public string OriginalName { get; set; }
        public string StoragePath { get; set; }
        public string ExtractPath { get; set; } // 可为空
    }

    public class CreateAnalysisResponse
    {
        public string AnalysisGuid { get; set; }
        public string Status { get; set; }
    }

    public class AnalysisStatusResponse
    {
        public string AnalysisGuid { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }

        public long? TotalRecords { get; set; }
        public int? RecordSize { get; set; }
        public int? DigitalWords { get; set; }

        public System.DateTime CrtTime { get; set; }
        public System.DateTime? StartTime { get; set; }
        public System.DateTime? FinishTime { get; set; }
    }

    public class SavedFileResult
    {
        public string FullPath { get; set; }
        public long FileSize { get; set; }
    }

    /// <summary>
    /// 分页响应模型
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// 数据列表
        /// </summary>
        public List<T> Data { get; set; }
        
        /// <summary>
        /// 总记录数
        /// </summary>
        public int Total { get; set; }
        
        /// <summary>
        /// 当前页码
        /// </summary>
        public int Page { get; set; }
        
        /// <summary>
        /// 每页大小
        /// </summary>
        public int PageSize { get; set; }
        
        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }
    }

    public sealed class AnalysisListItemDto
    {
        public string AnalysisGuid { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }

        public string OriginalName { get; set; }
        public long FileSize { get; set; }

        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
    }

    public sealed class AnalysisDetailDto
    {
        public string AnalysisGuid { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }

        public long? TotalRecords { get; set; }
        public int? RecordSize { get; set; }
        public int? DigitalWords { get; set; }

        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }

        public FileDto File { get; set; }
        public CfgSummaryDto CfgSummary { get; set; }
    }

    public sealed class FileDto
    {
        public int Id { get; set; }
        public string OriginalName { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
        public string StorageType { get; set; }
        public string StoragePath { get; set; }
        public string ExtractPath { get; set; }
    }

    public sealed class CfgSummaryDto
    {
        public string StationName { get; set; }
        public string DeviceId { get; set; }
        public int AnalogCount { get; set; }
        public int DigitalCount { get; set; }
        public decimal? FrequencyHz { get; set; }
        public decimal? TimeMul { get; set; }
        public string StartTimeRaw { get; set; }
        public string TriggerTimeRaw { get; set; }
    }

    public sealed class CfgDto
    {
        public string StationName { get; set; }
        public string DeviceId { get; set; }
        public int AnalogCount { get; set; }
        public int DigitalCount { get; set; }
        public decimal? FrequencyHz { get; set; }
        public decimal? TimeMul { get; set; }
        public string StartTimeRaw { get; set; }
        public string TriggerTimeRaw { get; set; }

        public string Revision { get; set; }
        public string FormatType { get; set; }
        public string DataType { get; set; }
        public string SampleRateJson { get; set; }  // 这里用 string，避免 JSON 类型序列化差异
        public string FullCfgText { get; set; }     // includeText=true 才返回
    }

    public sealed class ChannelDto
    {
        public int ChannelIndex { get; set; }
        public string ChannelType { get; set; }
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public string Unit { get; set; }
        public decimal? RatioA { get; set; }
        public decimal? OffsetB { get; set; }
        public decimal? Skew { get; set; }
        public int IsEnable { get; set; }
    }

    public sealed class HdrDto
    {
        public string FaultStartTime { get; set; }
        public string FaultKeepingTime { get; set; }
        public List<NameValue> DeviceInfoJson { get; set; }
        public List<TripInfo> TripInfoJSON { get; set; }
        public List<FaultInfo> FaultInfoJson { get; set; }
        public List<NameValue> DigitalStatusJson { get; set; }
        public List<DigitalEvent> DigitalEventJson { get; set; }
        public List<SettingValue> SettingValueJson { get; set; }
        public List<RelayEnaValue> RelayEnaValueJSON { get; set; }
    }

    public sealed class WaveDataPageDto
    {
        public int FromSample { get; set; }
        public int ToSample { get; set; }
        public int DownSample { get; set; }

        public int[] Channels { get; set; } = Array.Empty<int>();
        public int[] Digitals { get; set; } = Array.Empty<int>();

        public WaveDataRowDto[] Rows { get; set; } = Array.Empty<WaveDataRowDto>();
    }

    public sealed class WaveDataRowDto
    {
        public int SampleNo { get; set; }
        public int TimeRaw { get; set; }
        public double[] Analog { get; set; } = Array.Empty<double>();
        public short[] Digital { get; set; } = Array.Empty<short>();
    }
    
    public class WaveRowRaw
    {
        public int SampleNo { get; set; }
        public int TimeRaw { get; set; }

        // Channel1..Channel70
        public double? Channel1 { get; set; }
        public double? Channel2 { get; set; }
        public double? Channel3 { get; set; }
        public double? Channel4 { get; set; }
        public double? Channel5 { get; set; }
        public double? Channel6 { get; set; }
        public double? Channel7 { get; set; }
        public double? Channel8 { get; set; }
        public double? Channel9 { get; set; }
        public double? Channel10 { get; set; }
        public double? Channel11 { get; set; }
        public double? Channel12 { get; set; }
        public double? Channel13 { get; set; }
        public double? Channel14 { get; set; }
        public double? Channel15 { get; set; }
        public double? Channel16 { get; set; }
        public double? Channel17 { get; set; }
        public double? Channel18 { get; set; }
        public double? Channel19 { get; set; }
        public double? Channel20 { get; set; }
        public double? Channel21 { get; set; }
        public double? Channel22 { get; set; }
        public double? Channel23 { get; set; }
        public double? Channel24 { get; set; }
        public double? Channel25 { get; set; }
        public double? Channel26 { get; set; }
        public double? Channel27 { get; set; }
        public double? Channel28 { get; set; }
        public double? Channel29 { get; set; }
        public double? Channel30 { get; set; }
        public double? Channel31 { get; set; }
        public double? Channel32 { get; set; }
        public double? Channel33 { get; set; }
        public double? Channel34 { get; set; }
        public double? Channel35 { get; set; }
        public double? Channel36 { get; set; }
        public double? Channel37 { get; set; }
        public double? Channel38 { get; set; }
        public double? Channel39 { get; set; }
        public double? Channel40 { get; set; }
        public double? Channel41 { get; set; }
        public double? Channel42 { get; set; }
        public double? Channel43 { get; set; }
        public double? Channel44 { get; set; }
        public double? Channel45 { get; set; }
        public double? Channel46 { get; set; }
        public double? Channel47 { get; set; }
        public double? Channel48 { get; set; }
        public double? Channel49 { get; set; }
        public double? Channel50 { get; set; }
        public double? Channel51 { get; set; }
        public double? Channel52 { get; set; }
        public double? Channel53 { get; set; }
        public double? Channel54 { get; set; }
        public double? Channel55 { get; set; }
        public double? Channel56 { get; set; }
        public double? Channel57 { get; set; }
        public double? Channel58 { get; set; }
        public double? Channel59 { get; set; }
        public double? Channel60 { get; set; }
        public double? Channel61 { get; set; }
        public double? Channel62 { get; set; }
        public double? Channel63 { get; set; }
        public double? Channel64 { get; set; }
        public double? Channel65 { get; set; }
        public double? Channel66 { get; set; }
        public double? Channel67 { get; set; }
        public double? Channel68 { get; set; }
        public double? Channel69 { get; set; }
        public double? Channel70 { get; set; }

         /// <summary>
        /// 数字量字（bitset），来自数据库 Tb_ZwavData.DigitalWords VARBINARY(100)
        /// 按小端序存储：每个 word 2 字节（低字节在前）
        /// </summary>
        public byte[] DigitalWords { get; set; }

        public double GetAnalog(int idx) => idx switch
        {
            1 => (Channel1 ?? 0),
            2 => (Channel2 ?? 0),
            3 => (Channel3 ?? 0),
            4 => (Channel4 ?? 0),
            5 => (Channel5 ?? 0),
            6 => (Channel6 ?? 0),
            7 => (Channel7 ?? 0),
            8 => (Channel8 ?? 0),
            9 => (Channel9 ?? 0),
            10 => (Channel10 ?? 0),
            11 => (Channel11 ?? 0),
            12 => (Channel12 ?? 0),
            13 => (Channel13 ?? 0),
            14 => (Channel14 ?? 0),
            15 => (Channel15 ?? 0),
            16 => (Channel16 ?? 0),
            17 => (Channel17 ?? 0),
            18 => (Channel18 ?? 0),
            19 => (Channel19 ?? 0),
            20 => (Channel20 ?? 0),
            21 => (Channel21 ?? 0),
            22 => (Channel22 ?? 0),
            23 => (Channel23 ?? 0),
            24 => (Channel24 ?? 0),
            25 => (Channel25 ?? 0),
            26 => (Channel26 ?? 0),
            27 => (Channel27 ?? 0),
            28 => (Channel28 ?? 0),
            29 => (Channel29 ?? 0),
            30 => (Channel30 ?? 0),
            31 => (Channel31 ?? 0),
            32 => (Channel32 ?? 0),
            33 => (Channel33 ?? 0),
            34 => (Channel34 ?? 0),
            35 => (Channel35 ?? 0),
            36 => (Channel36 ?? 0),
            37 => (Channel37 ?? 0),
            38 => (Channel38 ?? 0),
            39 => (Channel39 ?? 0),
            40 => (Channel40 ?? 0),
            41 => (Channel41 ?? 0),
            42 => (Channel42 ?? 0),
            43 => (Channel43 ?? 0),
            44 => (Channel44 ?? 0),
            45 => (Channel45 ?? 0),
            46 => (Channel46 ?? 0),
            47 => (Channel47 ?? 0),
            48 => (Channel48 ?? 0),
            49 => (Channel49 ?? 0),
            50 => (Channel50 ?? 0),
            51 => (Channel51 ?? 0),
            52 => (Channel52 ?? 0),
            53 => (Channel53 ?? 0),
            54 => (Channel54 ?? 0),
            55 => (Channel55 ?? 0),
            56 => (Channel56 ?? 0),
            57 => (Channel57 ?? 0),
            58 => (Channel58 ?? 0),
            59 => (Channel59 ?? 0),
            60 => (Channel60 ?? 0),
            61 => (Channel61 ?? 0),
            62 => (Channel62 ?? 0),
            63 => (Channel63 ?? 0),
            64 => (Channel64 ?? 0),
            65 => (Channel65 ?? 0),
            66 => (Channel66 ?? 0),
            67 => (Channel67 ?? 0),
            68 => (Channel68 ?? 0),
            69 => (Channel69 ?? 0),
            70 => (Channel70 ?? 0),
            _ => 0
        };

         /// <summary>
        /// 获取第 idx 路数字量（1-based），返回 0/1。
        /// idx=1 表示第1路数字量，映射到 DigitalWords 的 bit0；
        /// idx=16 映射到 word0 的 bit15；
        /// idx=17 映射到 word1 的 bit0。
        /// </summary>
        public short GetDigital(int idx)
        {
            if (idx <= 0) return 0;
            if (DigitalWords == null || DigitalWords.Length < 2) return 0;

            int bit = idx - 1;
            int wordIndex = bit / 16;
            int bitInWord = bit % 16;

            int byteIndex = wordIndex * 2;
            if (byteIndex + 1 >= DigitalWords.Length) return 0;

            // 小端：低字节在前
            ushort word = (ushort)(DigitalWords[byteIndex] | (DigitalWords[byteIndex + 1] << 8));
            return (short)(((word >> bitInWord) & 0x1) == 1 ? 1 : 0);
        }
    }

}