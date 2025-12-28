using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 文件记录实体模型
    /// </summary>
    [Table("Tb_FileRecord")]
    public class ZwavFileRow
    {
        public int Id { get; set; }
        public string OriginalName { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
        public string StorageType { get; set; }
        public string StoragePath { get; set; }
        public string ExtractPath { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ZwavAnalysisRow
    {
        public int Id { get; set; }
        public string AnalysisGuid { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ErrorMessage { get; set; }
        public int FileId { get; set; }
        public long? TotalRecords { get; set; }
        public int? RecordSize { get; set; }
        public int? DigitalWords { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }

    public class ChannelRow
    {
        public int AnalysisId { get; set; }
        public int ChannelIndex { get; set; }
        public string ChannelType { get; set; }   // Analog/Digital/Virtual
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string Phase { get; set; }
        public string Unit { get; set; }
        public decimal? RatioA { get; set; }
        public decimal? OffsetB { get; set; }
        public decimal? Skew { get; set; }
        public int IsEnable { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}