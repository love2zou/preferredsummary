using System;

namespace Preferred.Api.Models
{
    public class ZwavCfg
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }
        public string FullCfgText { get; set; }
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

        public string SampleRateJson { get; set; } // MySQL JSON 列，EF 用 string 承载

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
