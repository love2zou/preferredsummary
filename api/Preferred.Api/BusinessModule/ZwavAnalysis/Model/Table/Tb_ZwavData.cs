using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 录波数据
    /// </summary>
    public class ZwavData
    {
        public int Id { get; set; }  // 主键ID
        public int AnalysisId { get; set; }
        public int SampleNo { get; set; }  // 样本号
        public int TimeRaw { get; set; }  // 原始时间戳

        // 模拟量通道
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
        /// 数字量字（bitset），对应数据库 VARBINARY(100)
        /// </summary>
        public byte[] DigitalWords { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
