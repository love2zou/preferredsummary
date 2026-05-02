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
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class ReservationAdminController : ControllerBase
    {
        private readonly IReservationAdminService _reservationAdminService;

        public ReservationAdminController(IReservationAdminService reservationAdminService)
        {
            _reservationAdminService = reservationAdminService;
        }

        [HttpGet("lookups/clubs")]
        [ProducesResponseType(typeof(List<ReservationLookupOptionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClubOptions()
        {
            return Ok(await _reservationAdminService.GetClubOptionsAsync());
        }

        [HttpGet("lookups/trainers")]
        [ProducesResponseType(typeof(List<ReservationLookupOptionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrainerOptions()
        {
            return Ok(await _reservationAdminService.GetTrainerOptionsAsync());
        }

        [HttpGet("lookups/users")]
        [ProducesResponseType(typeof(List<ReservationLookupOptionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserOptions([FromQuery] string? keyword = null)
        {
            return Ok(await _reservationAdminService.GetUserOptionsAsync(keyword));
        }

        [HttpGet("clubs/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationClubAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClubList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
        {
            var searchParams = new ReservationClubAdminSearchParams { Keyword = keyword };
            return Ok(new PagedResponse<ReservationClubAdminDto>
            {
                Data = await _reservationAdminService.GetClubListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetClubCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpGet("clubs/{id}")]
        public async Task<IActionResult> GetClubDetail(int id)
        {
            var data = await _reservationAdminService.GetClubByIdAsync(id);
            return data == null ? NotFound(new ApiErrorResponse { Message = "门店不存在" }) : Ok(data);
        }

        [HttpPost("clubs")]
        public async Task<IActionResult> CreateClub([FromBody] ReservationClubAdminEditDto dto)
        {
            var success = await _reservationAdminService.CreateClubAsync(dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "创建成功" : "创建失败" });
        }

        [HttpPut("clubs/{id}")]
        public async Task<IActionResult> UpdateClub(int id, [FromBody] ReservationClubAdminEditDto dto)
        {
            var success = await _reservationAdminService.UpdateClubAsync(id, dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "更新成功" : "更新失败" });
        }

        [HttpDelete("clubs/{id}")]
        public async Task<IActionResult> DeleteClub(int id)
        {
            var success = await _reservationAdminService.DeleteClubAsync(id);
            return Ok(new ApiResponse { Success = success, Message = success ? "删除成功" : "删除失败，可能已被业务数据占用" });
        }

        [HttpGet("trainers/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationTrainerAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrainerList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] int? clubId = null, [FromQuery] bool? isActive = null)
        {
            var searchParams = new ReservationTrainerAdminSearchParams
            {
                Keyword = keyword,
                ClubId = clubId,
                IsActive = isActive
            };

            return Ok(new PagedResponse<ReservationTrainerAdminDto>
            {
                Data = await _reservationAdminService.GetTrainerListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetTrainerCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpGet("trainers/{id}")]
        public async Task<IActionResult> GetTrainerDetail(int id)
        {
            var data = await _reservationAdminService.GetTrainerByIdAsync(id);
            return data == null ? NotFound(new ApiErrorResponse { Message = "教练不存在" }) : Ok(data);
        }

        [HttpPost("trainers")]
        public async Task<IActionResult> CreateTrainer([FromBody] ReservationTrainerAdminEditDto dto)
        {
            var success = await _reservationAdminService.CreateTrainerAsync(dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "创建成功" : "创建失败" });
        }

        [HttpPut("trainers/{id}")]
        public async Task<IActionResult> UpdateTrainer(int id, [FromBody] ReservationTrainerAdminEditDto dto)
        {
            var success = await _reservationAdminService.UpdateTrainerAsync(id, dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "更新成功" : "更新失败" });
        }

        [HttpDelete("trainers/{id}")]
        public async Task<IActionResult> DeleteTrainer(int id)
        {
            var success = await _reservationAdminService.DeleteTrainerAsync(id);
            return Ok(new ApiResponse { Success = success, Message = success ? "删除成功" : "删除失败，可能已被预约数据占用" });
        }

        [HttpGet("sessions/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationSessionAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessionList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? trainerProfileId = null, [FromQuery] string? keyword = null)
        {
            var searchParams = new ReservationSessionAdminSearchParams
            {
                TrainerProfileId = trainerProfileId,
                Keyword = keyword
            };

            return Ok(new PagedResponse<ReservationSessionAdminDto>
            {
                Data = await _reservationAdminService.GetSessionListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetSessionCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpGet("sessions/{id}")]
        public async Task<IActionResult> GetSessionDetail(int id)
        {
            var data = await _reservationAdminService.GetSessionByIdAsync(id);
            return data == null ? NotFound(new ApiErrorResponse { Message = "课程类型不存在" }) : Ok(data);
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] ReservationSessionAdminEditDto dto)
        {
            var success = await _reservationAdminService.CreateSessionAsync(dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "创建成功" : "创建失败" });
        }

        [HttpPut("sessions/{id}")]
        public async Task<IActionResult> UpdateSession(int id, [FromBody] ReservationSessionAdminEditDto dto)
        {
            var success = await _reservationAdminService.UpdateSessionAsync(id, dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "更新成功" : "更新失败" });
        }

        [HttpDelete("sessions/{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var success = await _reservationAdminService.DeleteSessionAsync(id);
            return Ok(new ApiResponse { Success = success, Message = success ? "删除成功" : "删除失败，可能已被预约数据占用" });
        }

        [HttpGet("schedules/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationScheduleAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetScheduleList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? trainerProfileId = null, [FromQuery] int? clubId = null, [FromQuery] string? scheduleDate = null, [FromQuery] bool? isAvailable = null)
        {
            var searchParams = new ReservationScheduleAdminSearchParams
            {
                TrainerProfileId = trainerProfileId,
                ClubId = clubId,
                ScheduleDate = scheduleDate,
                IsAvailable = isAvailable
            };

            return Ok(new PagedResponse<ReservationScheduleAdminDto>
            {
                Data = await _reservationAdminService.GetScheduleListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetScheduleCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpGet("schedules/{id}")]
        public async Task<IActionResult> GetScheduleDetail(int id)
        {
            var data = await _reservationAdminService.GetScheduleByIdAsync(id);
            return data == null ? NotFound(new ApiErrorResponse { Message = "排班不存在" }) : Ok(data);
        }

        [HttpPost("schedules")]
        public async Task<IActionResult> CreateSchedule([FromBody] ReservationScheduleAdminEditDto dto)
        {
            var success = await _reservationAdminService.CreateScheduleAsync(dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "创建成功" : "创建失败" });
        }

        [HttpPut("schedules/{id}")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] ReservationScheduleAdminEditDto dto)
        {
            var success = await _reservationAdminService.UpdateScheduleAsync(id, dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "更新成功" : "更新失败" });
        }

        [HttpDelete("schedules/{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var success = await _reservationAdminService.DeleteScheduleAsync(id);
            return Ok(new ApiResponse { Success = success, Message = success ? "删除成功" : "删除失败，可能已被预约数据占用" });
        }

        [HttpGet("packages/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationPackageAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPackageList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? memberId = null, [FromQuery] int? clubId = null, [FromQuery] string? statusCode = null)
        {
            var searchParams = new ReservationPackageAdminSearchParams
            {
                MemberId = memberId,
                ClubId = clubId,
                StatusCode = statusCode
            };

            return Ok(new PagedResponse<ReservationPackageAdminDto>
            {
                Data = await _reservationAdminService.GetPackageListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetPackageCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpGet("packages/{id}")]
        public async Task<IActionResult> GetPackageDetail(int id)
        {
            var data = await _reservationAdminService.GetPackageByIdAsync(id);
            return data == null ? NotFound(new ApiErrorResponse { Message = "课包不存在" }) : Ok(data);
        }

        [HttpPost("packages")]
        public async Task<IActionResult> CreatePackage([FromBody] ReservationPackageAdminEditDto dto)
        {
            var success = await _reservationAdminService.CreatePackageAsync(dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "创建成功" : "创建失败" });
        }

        [HttpPut("packages/{id}")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] ReservationPackageAdminEditDto dto)
        {
            var success = await _reservationAdminService.UpdatePackageAsync(id, dto);
            return Ok(new ApiResponse { Success = success, Message = success ? "更新成功" : "更新失败" });
        }

        [HttpDelete("packages/{id}")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var success = await _reservationAdminService.DeletePackageAsync(id);
            return Ok(new ApiResponse { Success = success, Message = success ? "删除成功" : "删除失败，可能已有预约记录" });
        }

        [HttpGet("orders/list")]
        [ProducesResponseType(typeof(PagedResponse<ReservationOrderAdminDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrderList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? memberId = null, [FromQuery] int? trainerProfileId = null, [FromQuery] string? statusCode = null)
        {
            var searchParams = new ReservationOrderAdminSearchParams
            {
                MemberId = memberId,
                TrainerProfileId = trainerProfileId,
                StatusCode = statusCode
            };

            return Ok(new PagedResponse<ReservationOrderAdminDto>
            {
                Data = await _reservationAdminService.GetOrderListAsync(page, pageSize, searchParams),
                Total = await _reservationAdminService.GetOrderCountAsync(searchParams),
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] ReservationOrderStatusUpdateDto dto)
        {
            var success = await _reservationAdminService.UpdateOrderStatusAsync(id, dto.StatusCode);
            return Ok(new ApiResponse { Success = success, Message = success ? "状态更新成功" : "状态更新失败" });
        }
    }
}
