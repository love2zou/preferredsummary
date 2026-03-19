using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 暂降分析计算结果（纯内存结果）
    /// 包含事件结果与 RMS 序列点，供上层落库或返回 API。
    /// </summary>
    public class ZwavSagAnalyzeResult
    {
        /// <summary>事件集合（可能为空）</summary>
        public List<ZwavSagEventResult> Events { get; set; } = new List<ZwavSagEventResult>();

        /// <summary>RMS 点集合（可能为空）</summary>
        public List<ZwavSagRmsPointResult> RmsPoints { get; set; } = new List<ZwavSagRmsPointResult>();
    }
}
