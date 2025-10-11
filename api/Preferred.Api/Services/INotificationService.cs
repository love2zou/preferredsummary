using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface INotificationService
    {
        Task<List<NotificationListDto>> GetNotificationList(int page = 1, int pageSize = 10, NotificationSearchParams searchParams = null);
        Task<int> GetNotificationCount(NotificationSearchParams searchParams = null);
        Task<NotificationListDto> GetNotificationById(int id);
        Task<ApiResponse<NotificationListDto>> CreateNotification(NotificationCreateDto notificationDto);
        Task<ApiResponse<NotificationListDto>> UpdateNotification(NotificationUpdateDto notificationDto);
        Task<ApiResponse<bool>> DeleteNotification(int id);
        Task<ApiResponse<bool>> BatchDeleteNotifications(List<int> ids);
        Task<ApiResponse<bool>> MarkAsRead(int id);
        Task<ApiResponse<bool>> BatchMarkAsRead(List<int> ids);
        Task<int> GetUnreadCount(string receiver);
        // 统一封装：注册成功后的欢迎与绑定提醒消息
        Task<ApiResponse<bool>> SendRegisterMessagesAsync(string receiver, string sender = "管理员");
        // 会员预约成功后，推送提醒给教练
        Task<ApiResponse<bool>> SendBookingCreatedToCoachAsync(int coachId, int memberId, DateTime bookDate, List<TimeSlotItem> timeSlots);
        // 新增：通知发送（单条/批量）
        Task<ApiResponse<bool>> SendNotification(int id, string sendUser);
        Task<ApiResponse<bool>> BatchSendNotifications(List<int> ids, string sendUser);
    }
}