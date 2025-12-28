using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 录波数据
    /// </summary>
    [Table("Tb_ZwavData")]
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

        // 数字量通道
        public ushort? Digital1 { get; set; }
        public ushort? Digital2 { get; set; }
        public ushort? Digital3 { get; set; }
        public ushort? Digital4 { get; set; }
        public ushort? Digital5 { get; set; }
        public ushort? Digital6 { get; set; }
        public ushort? Digital7 { get; set; }
        public ushort? Digital8 { get; set; }
        public ushort? Digital9 { get; set; }
        public ushort? Digital10 { get; set; }
        public ushort? Digital11 { get; set; }
        public ushort? Digital12 { get; set; }
        public ushort? Digital13 { get; set; }
        public ushort? Digital14 { get; set; }
        public ushort? Digital15 { get; set; }
        public ushort? Digital16 { get; set; }
        public ushort? Digital17 { get; set; }
        public ushort? Digital18 { get; set; }
        public ushort? Digital19 { get; set; }
        public ushort? Digital20 { get; set; }
        public ushort? Digital21 { get; set; }
        public ushort? Digital22 { get; set; }
        public ushort? Digital23 { get; set; }
        public ushort? Digital24 { get; set; }
        public ushort? Digital25 { get; set; }
        public ushort? Digital26 { get; set; }
        public ushort? Digital27 { get; set; }
        public ushort? Digital28 { get; set; }
        public ushort? Digital29 { get; set; }
        public ushort? Digital30 { get; set; }
        public ushort? Digital31 { get; set; }
        public ushort? Digital32 { get; set; }
        public ushort? Digital33 { get; set; }
        public ushort? Digital34 { get; set; }
        public ushort? Digital35 { get; set; }
        public ushort? Digital36 { get; set; }
        public ushort? Digital37 { get; set; }
        public ushort? Digital38 { get; set; }
        public ushort? Digital39 { get; set; }
        public ushort? Digital40 { get; set; }
        public ushort? Digital41 { get; set; }
        public ushort? Digital42 { get; set; }
        public ushort? Digital43 { get; set; }
        public ushort? Digital44 { get; set; }
        public ushort? Digital45 { get; set; }
        public ushort? Digital46 { get; set; }
        public ushort? Digital47 { get; set; }
        public ushort? Digital48 { get; set; }
        public ushort? Digital49 { get; set; }
        public ushort? Digital50 { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
