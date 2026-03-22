using System;
using System.Collections.Generic;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 暂降通道词库
    /// 用于从录波通道名称/编码中识别电压通道（例如包含 U/V/Voltage 等关键词）。
    /// </summary>
    public class ZwavSagChannelRule
    {
        /// <summary>主键</summary>
        public int Id { get; set; }

        /// <summary>规则名称/关键词</summary>
        public string RuleName { get; set; }

        /// <summary>相别名称（A/B/C/AB/BC/CA）</summary>
        public string PhaseName { get; set; }

        /// <summary>排序号（越小越靠前）</summary>
        public int SeqNo { get; set; }

        /// <summary>创建时间（UTC）</summary>
        public DateTime CrtTime { get; set; }

        /// <summary>更新时间（UTC）</summary>
        public DateTime UpdTime { get; set; }
    }
}
