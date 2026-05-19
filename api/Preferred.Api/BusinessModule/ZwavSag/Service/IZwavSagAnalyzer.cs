using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 电压暂降分析器。
    /// 只负责纯计算，不承担录波文件读取、数据库访问或任务编排职责。
    /// </summary>
    public interface IZwavSagAnalyzer
    {
        /// <summary>
        /// 基于已准备好的分析上下文执行暂降识别，并返回事件与 RMS 明细结果。
        /// 调用方需要提前完成采样点、通道信息、参考参数等上下文准备。
        /// </summary>
        Task<ZwavSagAnalyzeResult> AnalyzeAsync(ZwavSagAnalyzeContext context);
    }
}
