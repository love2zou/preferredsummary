using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 访问地址服务接口
    /// </summary>
    public interface INetworkUrlService
    {
        /// <summary>
        /// 获取访问地址列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="size">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>访问地址列表</returns>
        Task<List<NetworkUrlListDto>> GetNetworkUrlList(int page, int size, NetworkUrlSearchParams searchParams);

        /// <summary>
        /// 获取访问地址总数
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>总数</returns>
        Task<int> GetNetworkUrlCount(NetworkUrlSearchParams searchParams);

        /// <summary>
        /// 根据标签类型获取访问地址列表
        /// </summary>
        /// <param name="tagCodeType">标签类型</param>
        /// <returns>访问地址列表</returns>
        Task<List<NetworkUrlListDto>> GetNetworkUrlsByTagType(string tagCodeType);

        /// <summary>
        /// 根据ID获取访问地址详情
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <returns>访问地址详情</returns>
        Task<NetworkUrl> GetNetworkUrlById(int id);

        /// <summary>
        /// 创建访问地址
        /// </summary>
        /// <param name="networkUrlDto">访问地址信息</param>
        /// <returns>是否成功</returns>
        Task<bool> CreateNetworkUrl(NetworkUrlDto networkUrlDto);

        /// <summary>
        /// 更新访问地址
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <param name="networkUrlDto">访问地址信息</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateNetworkUrl(int id, NetworkUrlDto networkUrlDto);

        /// <summary>
        /// 删除访问地址
        /// </summary>
        /// <param name="id">访问地址ID</param>
        /// <returns>是否成功</returns>
        Task<bool> DeleteNetworkUrl(int id);

        /// <summary>
        /// 检查URL是否存在
        /// </summary>
        /// <param name="url">URL地址</param>
        /// <param name="excludeId">排除的ID</param>
        /// <returns>是否存在</returns>
        Task<bool> IsUrlExists(string url, int? excludeId = null);
    }
}