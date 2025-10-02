using System.Threading.Tasks;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 系统监控服务接口
    /// </summary>
    public interface ISystemMonitorService
    {
        /// <summary>
        /// 获取当前系统资源使用情况
        /// </summary>
        /// <returns>系统资源数据</returns>
        Task<Models.SystemResourceCreateDto> GetCurrentSystemResourceAsync();
        
        /// <summary>
        /// 执行系统资源监控并保存到数据库
        /// </summary>
        /// <returns></returns>
        Task MonitorAndSaveAsync();
    }
}