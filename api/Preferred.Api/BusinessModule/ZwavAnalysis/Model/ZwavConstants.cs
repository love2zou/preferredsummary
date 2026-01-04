using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
    public static class ZwavConstants
    {
        /// <summary>最大模拟量通道数</summary>
        public const int MaxAnalog = 70;
        
        /// <summary>最大开关量通道数</summary>
        public const int MaxDigital = 700;

        /// <summary>进度状态：排队中</summary>
        public const string Queued = "Queued";
        /// <summary>进度状态：已取消</summary>
        public const string Canceled = "Canceled";
        /// <summary>进度状态：已完成</summary>
        public const string Completed = "Completed";
        /// <summary>进度状态：失败</summary>
        public const string Failed = "Failed";
        /// <summary>进度状态：解析中</summary>
        public const string Parsing = "Parsing";
        /// <summary>进度状态：解析读取录波文件</summary>
        public const string ParsingRead = "ParsingRead";
        /// <summary>进度状态：解析提取文件/summary>
        public const string ParsingExtract = "ParsingExtract";
        /// <summary>进度状态：解析CFG文件</summary>
        public const string ParsingCfg = "ParsingCfg";
        /// <summary>进度状态：解析HDR文件</summary>
        public const string ParsingHdr = "ParsingHdr";
        /// <summary>进度状态：解析通道</summary>
        public const string ParsingChannel = "ParsingChannel";
        /// <summary>进度状态：解析Dat数据</summary>
        public const string ParsingDat = "ParsingDat";
    }
}
