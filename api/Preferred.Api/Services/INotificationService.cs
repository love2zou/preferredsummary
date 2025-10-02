using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 通知服务接口
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 获取通知列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>通知列表</returns>
        Task<List<NotificationListDto>> GetNotificationList(int page = 1, int pageSize = 10, NotificationSearchParams searchParams = null);
        
        /// <summary>
        /// 获取通知总数
        /// </summary>
        /// <param name="searchParams">搜索参数</param>
        /// <returns>总数</returns>
        Task<int> GetNotificationCount(NotificationSearchParams searchParams = null);
        
        /// <summary>
        /// 根据ID获取通知
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>通知信息</returns>
        Task<NotificationListDto> GetNotificationById(int id);
        
        /// <summary>
        /// 创建通知
        /// </summary>
        /// <param name="notificationDto">通知信息</param>
        /// <returns>创建结果</returns>
        Task<ApiResponse<NotificationListDto>> CreateNotification(NotificationCreateDto notificationDto);
        
        /// <summary>
        /// 更新通知
        /// </summary>
        /// <param name="notificationDto">通知信息</param>
        /// <returns>更新结果</returns>
        Task<ApiResponse<NotificationListDto>> UpdateNotification(NotificationUpdateDto notificationDto);
        
        /// <summary>
        /// 删除通知
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>删除结果</returns>
        Task<ApiResponse<bool>> DeleteNotification(int id);
        
        /// <summary>
        /// 批量删除通知
        /// </summary>
        /// <param name="ids">通知ID列表</param>
        /// <returns>删除结果</returns>
        Task<ApiResponse<bool>> BatchDeleteNotifications(List<int> ids);
        
        /// <summary>
        /// 标记通知为已读
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>操作结果</returns>
        Task<ApiResponse<bool>> MarkAsRead(int id);
        
        /// <summary>
        /// 批量标记通知为已读
        /// </summary>
        /// <param name="ids">通知ID列表</param>
        /// <returns>操作结果</returns>
        Task<ApiResponse<bool>> BatchMarkAsRead(List<int> ids);
        
        /// <summary>
        /// 获取用户未读通知数量
        /// </summary>
        /// <param name="receiver">接收人</param>
        /// <returns>未读数量</returns>
        Task<int> GetUnreadCount(string receiver);
    }
}