using System;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 暂降通道相别识别规则。
    /// `Enabled=false` 时，该规则命中的电压通道会被排除，不参与暂降分析。
    /// </summary>
    public class ZwavSagChannelRule
    {
        public int Id { get; set; }

        public string RuleName { get; set; }

        public string PhaseName { get; set; }

        public int SeqNo { get; set; }

        public bool Enabled { get; set; } = true;

        public DateTime CrtTime { get; set; }

        public DateTime UpdTime { get; set; }
    }
}
