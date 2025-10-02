using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 通知服务实现
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationListDto>> GetNotificationList(int page = 1, int pageSize = 10, NotificationSearchParams searchParams = null)
        {
            var query = _context.Notifications.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.Name))
                    query = query.Where(x => x.Name.Contains(searchParams.Name));
                    
                if (!string.IsNullOrEmpty(searchParams.Content))
                    query = query.Where(x => x.Content.Contains(searchParams.Content));
                    
                if (!string.IsNullOrEmpty(searchParams.NotifyType))
                    query = query.Where(x => x.NotifyType == searchParams.NotifyType);
                    
                if (searchParams.NotifyStatus.HasValue)
                    query = query.Where(x => x.NotifyStatus == searchParams.NotifyStatus.Value);
                    
                if (!string.IsNullOrEmpty(searchParams.SendUser))
                    query = query.Where(x => x.SendUser.Contains(searchParams.SendUser));
                    
                if (!string.IsNullOrEmpty(searchParams.Receiver))
                    query = query.Where(x => x.Receiver.Contains(searchParams.Receiver));
                    
                if (searchParams.IsRead.HasValue)
                    query = query.Where(x => x.IsRead == searchParams.IsRead.Value);
            }

            return await query
                .OrderByDescending(x => x.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new NotificationListDto
                {
                    Id = x.Id,
                    IsRead = x.IsRead,
                    Name = x.Name,
                    Content = x.Content,
                    NotifyType = x.NotifyType,
                    NotifyStatus = x.NotifyStatus,
                    SendTime = x.SendTime,
                    SendUser = x.SendUser,
                    Receiver = x.Receiver,
                    Remark = x.Remark,
                    SeqNo = x.SeqNo,
                    CrtTime = x.CrtTime,
                    UpdTime = x.UpdTime
                })
                .ToListAsync();
        }

        public async Task<int> GetNotificationCount(NotificationSearchParams searchParams = null)
        {
            var query = _context.Notifications.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.Name))
                    query = query.Where(x => x.Name.Contains(searchParams.Name));
                    
                if (!string.IsNullOrEmpty(searchParams.Content))
                    query = query.Where(x => x.Content.Contains(searchParams.Content));
                    
                if (!string.IsNullOrEmpty(searchParams.NotifyType))
                    query = query.Where(x => x.NotifyType == searchParams.NotifyType);
                    
                if (searchParams.NotifyStatus.HasValue)
                    query = query.Where(x => x.NotifyStatus == searchParams.NotifyStatus.Value);
                    
                if (!string.IsNullOrEmpty(searchParams.SendUser))
                    query = query.Where(x => x.SendUser.Contains(searchParams.SendUser));
                    
                if (!string.IsNullOrEmpty(searchParams.Receiver))
                    query = query.Where(x => x.Receiver.Contains(searchParams.Receiver));
                    
                if (searchParams.IsRead.HasValue)
                    query = query.Where(x => x.IsRead == searchParams.IsRead.Value);
            }

            return await query.CountAsync();
        }

        public async Task<NotificationListDto> GetNotificationById(int id)
        {
            return await _context.Notifications
                .Where(x => x.Id == id)
                .Select(x => new NotificationListDto
                {
                    Id = x.Id,
                    IsRead = x.IsRead,
                    Name = x.Name,
                    Content = x.Content,
                    NotifyType = x.NotifyType,
                    NotifyStatus = x.NotifyStatus,
                    SendTime = x.SendTime,
                    SendUser = x.SendUser,
                    Receiver = x.Receiver,
                    Remark = x.Remark,
                    SeqNo = x.SeqNo,
                    CrtTime = x.CrtTime,
                    UpdTime = x.UpdTime
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ApiResponse<NotificationListDto>> CreateNotification(NotificationCreateDto notificationDto)
        {
            try
            {
                // 输入验证
                if (notificationDto == null)
                    return new ApiResponse<NotificationListDto> { Success = false, Message = "通知信息不能为空" };
                    
                if (string.IsNullOrWhiteSpace(notificationDto.Name))
                    return new ApiResponse<NotificationListDto> { Success = false, Message = "通知名称不能为空" };
                    
                if (string.IsNullOrWhiteSpace(notificationDto.Content))
                    return new ApiResponse<NotificationListDto> { Success = false, Message = "通知内容不能为空" };

                var notification = new Notification
                {
                    Name = notificationDto.Name,
                    Content = notificationDto.Content,
                    NotifyType = notificationDto.NotifyType,
                    NotifyStatus = notificationDto.NotifyStatus,
                    SendTime = notificationDto.SendTime,
                    SendUser = notificationDto.SendUser,
                    Receiver = notificationDto.Receiver,
                    Remark = notificationDto.Remark,
                    SeqNo = notificationDto.SeqNo,
                    IsRead = 0,
                    CrtTime = DateTime.UtcNow,
                    UpdTime = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                
                var result = await GetNotificationById(notification.Id);
                return new ApiResponse<NotificationListDto> { Success = true, Message = "创建成功", Data = result };
            }
            catch (Exception ex)
            {
                return new ApiResponse<NotificationListDto> { Success = false, Message = $"创建通知失败：{ex.Message}" };
            }
        }

        public async Task<ApiResponse<NotificationListDto>> UpdateNotification(NotificationUpdateDto notificationDto)
        {
            try
            {
                // 输入验证
                if (notificationDto == null)
                    return new ApiResponse<NotificationListDto> { Success = false, Message = "通知信息不能为空" };

                var notification = await _context.Notifications.FindAsync(notificationDto.Id);
                if (notification == null)
                {
                    return new ApiResponse<NotificationListDto> { Success = false, Message = "通知不存在" };
                }
                
                // 更新字段
                if (notificationDto.IsRead.HasValue)
                    notification.IsRead = notificationDto.IsRead.Value;
                    
                if (!string.IsNullOrEmpty(notificationDto.Name))
                    notification.Name = notificationDto.Name;
                    
                if (!string.IsNullOrEmpty(notificationDto.Content))
                    notification.Content = notificationDto.Content;
                    
                if (!string.IsNullOrEmpty(notificationDto.NotifyType))
                    notification.NotifyType = notificationDto.NotifyType;
                    
                if (notificationDto.NotifyStatus.HasValue)
                    notification.NotifyStatus = notificationDto.NotifyStatus.Value;
                    
                if (notificationDto.SendTime.HasValue)
                    notification.SendTime = notificationDto.SendTime.Value;
                    
                if (!string.IsNullOrEmpty(notificationDto.SendUser))
                    notification.SendUser = notificationDto.SendUser;
                    
                if (!string.IsNullOrEmpty(notificationDto.Receiver))
                    notification.Receiver = notificationDto.Receiver;
                    
                if (!string.IsNullOrEmpty(notificationDto.Remark))
                    notification.Remark = notificationDto.Remark;
                    
                if (notificationDto.SeqNo.HasValue)
                    notification.SeqNo = notificationDto.SeqNo.Value;

                notification.UpdTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                var result = await GetNotificationById(notification.Id);
                return new ApiResponse<NotificationListDto> { Success = true, Message = "更新成功", Data = result };
            }
            catch (Exception ex)
            {
                return new ApiResponse<NotificationListDto> { Success = false, Message = $"更新通知失败：{ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteNotification(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "通知不存在" };
                }

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Message = "删除成功", Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = $"删除通知失败：{ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> BatchDeleteNotifications(List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "通知ID列表不能为空" };
                }

                var notifications = await _context.Notifications
                    .Where(x => ids.Contains(x.Id))
                    .ToListAsync();

                if (notifications.Count == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "没有找到要删除的通知" };
                }

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Message = "批量删除成功", Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = $"批量删除通知失败：{ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> MarkAsRead(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "通知不存在" };
                }

                notification.IsRead = 1;
                notification.UpdTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Message = "标记已读成功", Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = $"标记已读失败：{ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> BatchMarkAsRead(List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return new ApiResponse<bool> { Success = false, Message = "通知ID列表不能为空" };
                }

                var notifications = await _context.Notifications
                    .Where(x => ids.Contains(x.Id) && x.IsRead == 0)
                    .ToListAsync();

                if (notifications.Count == 0)
                {
                    return new ApiResponse<bool> { Success = true, Message = "没有需要标记的通知", Data = true }; // 没有需要标记的通知，也算成功
                }

                foreach (var notification in notifications)
                {
                    notification.IsRead = 1;
                    notification.UpdTime = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Message = "批量标记已读成功", Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = $"批量标记已读失败：{ex.Message}" };
            }
        }

        public async Task<int> GetUnreadCount(string receiver)
        {
            if (string.IsNullOrWhiteSpace(receiver))
                return 0;
                
            return await _context.Notifications
                .Where(x => x.Receiver == receiver && x.IsRead == 0)
                .CountAsync();
        }
    }
}