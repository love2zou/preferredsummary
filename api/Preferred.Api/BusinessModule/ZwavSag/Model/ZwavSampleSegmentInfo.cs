using System;
using System.Collections.Generic;
namespace Zwav.Application.Sag
{
    /// <summary>
    /// 可分析采样段信息
    /// </summary>
    public class ZwavSampleSegmentInfo
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public double SampleIntervalMs { get; set; }
    }
}