using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    /// <summary>
    /// 电压暂降分析器
    /// 只负责纯计算，不负责读取波形数据
    /// </summary>
    public interface IZwavSagAnalyzer
    {
        Task<ZwavSagAnalyzeResult> AnalyzeAsync(ZwavSagAnalyzeContext context);
    }
}