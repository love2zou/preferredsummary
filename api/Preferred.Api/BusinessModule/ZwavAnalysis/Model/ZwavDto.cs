using System;
using System.ComponentModel.DataAnnotations;

namespace Preferred.Api.Models
{
    public class CreateAnalysisResponse
    {
        public string AnalysisGuid { get; set; }
        public string Status { get; set; } // Queued/Parsing/Ready/Failed
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

    public class AnalysisMetaResponse
    {
        public FileMetaDto File { get; set; }
        public CfgMetaDto Cfg { get; set; }
        public HdrMetaDto Hdr { get; set; }
        public DatMetaDto Dat { get; set; }
        public ChannelDto[] Channels { get; set; }
    }

    public class FileMetaDto
    {
        public string OriginalName { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
        public string StorageType { get; set; }
        public string StoragePath { get; set; }
        public string ExtractPath { get; set; }
    }

    public class CfgMetaDto
    {
        public string StationName { get; set; }
        public string DeviceId { get; set; }
        public string Revision { get; set; }
        public int AnalogCount { get; set; }
        public int DigitalCount { get; set; }
        public decimal? FrequencyHz { get; set; }
        public decimal? TimeMul { get; set; }
        public string StartTimeRaw { get; set; }
        public string TriggerTimeRaw { get; set; }
        public string FormatType { get; set; }
        public string DataType { get; set; }
        public string SampleRateJson { get; set; } // 直接返回 JSON 字符串
    }

    public class HdrMetaDto
    {
        public string HdrVersion { get; set; }
        public string DeviceInfoJson { get; set; }
        public string FaultInfoJson { get; set; }
        public string DigitalStatusJson { get; set; }
        public string DigitalEventJson { get; set; }
        public string SettingValueJson { get; set; }
    }

    public class DatMetaDto
    {
        public long? TotalRecords { get; set; }
        public int? RecordSize { get; set; }
        public int? DigitalWords { get; set; }
    }

    public class ChannelDto
    {
        public int ChannelIndex { get; set; }
        public string ChannelType { get; set; } // Analog/Digital/Virtual
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public string Unit { get; set; }
        public decimal? RatioA { get; set; }
        public decimal? OffsetB { get; set; }
        public decimal? Skew { get; set; }
        public int IsEnable { get; set; }
    }

    public class SamplesResponse
    {
        public long Start { get; set; }
        public int Count { get; set; }
        public string[] Channels { get; set; }
        public SampleRowDto[] Rows { get; set; }
    }

    public class SampleRowDto
    {
        public long RecordIndex { get; set; }
        public long? TimeRaw { get; set; } // 可选：若你DAT有原始时间
        public double? TimeMs { get; set; } // 可选：根据CFG计算后的毫秒
        public System.Collections.Generic.Dictionary<string, double> Values { get; set; } // key=通道code/index
    }

    public class WaveResponse
    {
        public long Start { get; set; }
        public long End { get; set; }
        public int MaxPoints { get; set; }
        public string Mode { get; set; } // Lttb/Envelope
        public WaveSeriesDto[] Series { get; set; }
    }

    public class WaveSeriesDto
    {
        public string Channel { get; set; } // e.g. "1" / "Ia" / "virt:3I0"
        public WavePointDto[] Points { get; set; }
    }

    public class WavePointDto
    {
        public double X { get; set; } // recordIndex or timeMs
        public double Y { get; set; }
        public double? YMin { get; set; } // Envelope 模式可用
        public double? YMax { get; set; }
    }
}