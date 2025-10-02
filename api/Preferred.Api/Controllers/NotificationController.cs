using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Models;
using Preferred.Api.Services;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 通知管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // 需要JWT认证
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// 获取通知列表
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="size">每页数量，默认为10</param>
        /// <param name="name">标题搜索</param>
        /// <param name="content">内容搜索</param>
        /// <param name="notifyType">通知类型搜索</param>
        /// <param name="notifyStatus">通知状态搜索</param>
        /// <param name="isRead">是否已读搜索</param>
        /// <param name="sendUser">发送人搜索</param>
        /// <param name="receiver">接收人搜索</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>通知列表</returns>
        [HttpGet("list")]
        [ProducesResponseType(typeof(PagedResponse<NotificationListDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNotificationList(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string name = "",
            [FromQuery] string content = "",
            [FromQuery] string notifyType = "",
            [FromQuery] int? notifyStatus = null,
            [FromQuery] int? isRead = null,
            [FromQuery] string sendUser = "",
            [FromQuery] string receiver = "",
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null)
        {
            try
            {
                var searchParams = new NotificationSearchParams
                {
                    Name = name,
                    Content = content,
                    NotifyType = notifyType,
                    NotifyStatus = notifyStatus,
                    IsRead = isRead,
                    SendUser = sendUser,
                    Receiver = receiver,
                    StartTime = startTime,
                    EndTime = endTime
                };

                var notifications = await _notificationService.GetNotificationList(page, size, searchParams);
                var totalCount = await _notificationService.GetNotificationCount(searchParams);

                var response = new PagedResponse<NotificationListDto>
                {
                    Data = notifications,
                    Total = totalCount,
                    Page = page,
                    PageSize = size,
                    TotalPages = (int)Math.Ceiling((double)totalCount / size)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取通知列表失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取通知详情
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>通知详情</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<NotificationListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetNotificationById(int id)
        {
            try
            {
                var notification = await _notificationService.GetNotificationById(id);
                if (notification == null)
                {
                    return NotFound(new ApiErrorResponse { Message = "通知不存在" });
                }

                return Ok(new ApiResponse<NotificationListDto>
                {
                    Success = true,
                    Message = "获取通知详情成功",
                    Data = notification
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取通知详情失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 创建通知
        /// </summary>
        /// <param name="notificationDto">通知信息</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<NotificationListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateNotification([FromBody] NotificationCreateDto notificationDto)
        {
            try
            {
                if (notificationDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _notificationService.CreateNotification(notificationDto);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "创建通知失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 更新通知
        /// </summary>
        /// <param name="notificationDto">通知信息</param>
        /// <returns>更新结果</returns>
        [HttpPut]
        [ProducesResponseType(typeof(ApiResponse<NotificationListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateNotification([FromBody] NotificationUpdateDto notificationDto)
        {
            try
            {
                if (notificationDto == null)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据不能为空" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请求数据验证失败" });
                }

                var result = await _notificationService.UpdateNotification(notificationDto);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "更新通知失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 删除通知
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var result = await _notificationService.DeleteNotification(id);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除通知失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 批量删除通知
        /// </summary>
        /// <param name="ids">通知ID列表</param>
        /// <returns>删除结果</returns>
        [HttpPost("batch-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BatchDeleteNotifications([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请提供要删除的通知ID列表" });
                }

                var result = await _notificationService.BatchDeleteNotifications(ids);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量删除通知失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 标记通知为已读
        /// </summary>
        /// <param name="id">通知ID</param>
        /// <returns>操作结果</returns>
        [HttpPut("{id}/read")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var result = await _notificationService.MarkAsRead(id);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "标记已读失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 批量标记通知为已读
        /// </summary>
        /// <param name="ids">通知ID列表</param>
        /// <returns>操作结果</returns>
        [HttpPost("batch-read")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BatchMarkAsRead([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || ids.Count == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "请提供要标记的通知ID列表" });
                }

                var result = await _notificationService.BatchMarkAsRead(ids);
                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(new ApiErrorResponse { Message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "批量标记已读失败", Details = ex.Message });
            }
        }

        /// <summary>
        /// 获取用户未读通知数量
        /// </summary>
        /// <param name="receiver">接收人</param>
        /// <returns>未读数量</returns>
        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUnreadCount([FromQuery] string receiver)
        {
            try
            {
                if (string.IsNullOrEmpty(receiver))
                {
                    return BadRequest(new ApiErrorResponse { Message = "接收人参数不能为空" });
                }

                var count = await _notificationService.GetUnreadCount(receiver);
                return Ok(new ApiResponse<int>
                {
                    Success = true,
                    Message = "获取未读数量成功",
                    Data = count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "获取未读数量失败", Details = ex.Message });
            }
        }
    }
}