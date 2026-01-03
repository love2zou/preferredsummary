using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
     public class NameValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class TripInfo
    {
        public string Time { get; set; }
        public string Name { get; set; }
        public string Phase { get; set; }
        public string Value { get; set; }
    }

    public class FaultInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
    }

    public class DigitalEvent
    {
        public string Time { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class SettingValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
    }
    
        public class RelayEnaValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
    
    public class CfgParseResult
    {
        public string FullText { get; set; }
        public string StationName { get; set; }
        public string DeviceId { get; set; }
        public string Revision { get; set; }

        public int AnalogCount { get; set; }
        public int DigitalCount { get; set; }

        public decimal? FrequencyHz { get; set; }
        public decimal? TimeMul { get; set; }

        public string StartTimeRaw { get; set; }
        public string TriggerTimeRaw { get; set; }

        public string FormatType { get; set; } // BINARY/ASCII
        public string DataType { get; set; }   // INT16/INT32 等（本实现主要按INT16）

        public string SampleRateJson { get; set; } // JSON string

        public List<ChannelDef> Channels { get; set; } = new List<ChannelDef>();
        public int DigitalWords { get; set; } // 由DigitalCount推导
    }

    public class ChannelDef
    {
        public int ChannelIndex { get; set; } // 1-based
        public string ChannelType { get; set; } // Analog/Digital/Virtual
        public string Code { get; set; }
        public string Name { get; set; }
        public string Phase { get; set; }
        public string Unit { get; set; }
        public decimal? A { get; set; }
        public decimal? B { get; set; }
        public decimal? Skew { get; set; }
    }

    public class HdrParseResult
    {
        public string FaultStartTime { get; set; }
        public string FaultKeepingTime { get; set; }
        public string DeviceInfoJson { get; set; }
        public string TripInfoJSON { get; set; }
        public string FaultInfoJson { get; set; }
        public string DigitalStatusJson { get; set; }
        public string DigitalEventJson { get; set; }
        public string SettingValueJson { get; set; }
        public string RelayEnaValueJSON { get; set; }
    }
  
    public sealed class DatRowAll
    {
        public int Index { get; set; }           // idx: i+1
        public int SampleNo { get; set; }        // sampleNo（JS里读了但没展示，这里保留）
        public int TimeRaw { get; set; }         // timeRaw
        public double[] Channels { get; set; }    // 全部模拟量物理值，长度 = ACount
        public byte[] DigitalWords { get; set; } // 压缩后的开关量
    }

    public class WaveDataParseResult
    {
        public List<DatRowAll> Rows { get; set; }
        public int DigitalWords { get; set; }    // dvWords
        public int RecordSize { get; set; }      // recordSize
        public int TotalRecords { get; set; }    // totalRecords
    }
}