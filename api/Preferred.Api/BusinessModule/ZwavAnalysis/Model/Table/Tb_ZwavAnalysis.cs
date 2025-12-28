using System;

namespace Preferred.Api.Models
{
    public class ZwavAnalysis
    {
        public int Id { get; set; }
        public string AnalysisGuid { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }

        public int FileId { get; set; }
        public int? TotalRecords { get; set; }   // 你表里是 INT；建议后面改 BIGINT
        public int? RecordSize { get; set; }
        public int? DigitalWords { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }

        // 可选导航
        public ZwavFile File { get; set; }
    }
}