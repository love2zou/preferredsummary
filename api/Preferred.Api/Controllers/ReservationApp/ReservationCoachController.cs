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
    public class ReservationCoachController : ControllerBase
    {
        private readonly IReservationCoachService _reservationCoachService;

        public ReservationCoachController(IReservationCoachService reservationCoachService)
        {
            _reservationCoachService = reservationCoachService;
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCoachDashboardDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard([FromQuery] int coachUserId)
        {
            var data = await _reservationCoachService.GetDashboardAsync(coachUserId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationCoachDashboardDto> { Success = false, Message = "教练数据不存在" });
            }

            return Ok(new ApiResponse<ReservationCoachDashboardDto> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("members")]
        [ProducesResponseType(typeof(ApiResponse<List<ReservationCoachMemberDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMembers([FromQuery] int coachUserId)
        {
            var data = await _reservationCoachService.GetMembersAsync(coachUserId);
            return Ok(new ApiResponse<List<ReservationCoachMemberDto>> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("schedule")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCoachScheduleDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSchedule([FromQuery] int coachUserId, [FromQuery] string? scheduleDate = null)
        {
            var data = await _reservationCoachService.GetScheduleAsync(coachUserId, scheduleDate);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationCoachScheduleDto> { Success = false, Message = "教练排班不存在" });
            }

            return Ok(new ApiResponse<ReservationCoachScheduleDto> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("reservations")]
        [ProducesResponseType(typeof(ApiResponse<List<ReservationCoachReservationDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReservations([FromQuery] int coachUserId, [FromQuery] string? statusCode = null)
        {
            var data = await _reservationCoachService.GetReservationsAsync(coachUserId, statusCode);
            return Ok(new ApiResponse<List<ReservationCoachReservationDto>> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("profile")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCoachProfileDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfile([FromQuery] int coachUserId)
        {
            var data = await _reservationCoachService.GetProfileAsync(coachUserId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationCoachProfileDto> { Success = false, Message = "教练信息不存在" });
            }

            return Ok(new ApiResponse<ReservationCoachProfileDto> { Success = true, Message = "获取成功", Data = data });
        }
    }
}
