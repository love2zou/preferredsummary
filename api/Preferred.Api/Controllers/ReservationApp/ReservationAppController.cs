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
    public class ReservationAppController : ControllerBase
    {
        private readonly IReservationAppService _reservationAppService;

        public ReservationAppController(IReservationAppService reservationAppService)
        {
            _reservationAppService = reservationAppService;
        }

        [HttpGet("clubs")]
        [ProducesResponseType(typeof(ApiResponse<List<ReservationClubDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClubs([FromQuery] string? city = null)
        {
            var data = await _reservationAppService.GetClubsAsync(city);
            return Ok(new ApiResponse<List<ReservationClubDto>>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpGet("home")]
        [ProducesResponseType(typeof(ApiResponse<ReservationHomeDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHome([FromQuery] int memberId)
        {
            var data = await _reservationAppService.GetHomeAsync(memberId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationHomeDto>
                {
                    Success = false,
                    Message = "会员不存在"
                });
            }

            return Ok(new ApiResponse<ReservationHomeDto>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpGet("trainers")]
        [ProducesResponseType(typeof(ApiResponse<List<ReservationTrainerCardDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrainers([FromQuery] ReservationTrainerQuery query)
        {
            var data = await _reservationAppService.GetTrainersAsync(query);
            return Ok(new ApiResponse<List<ReservationTrainerCardDto>>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpGet("trainers/{trainerId}")]
        [ProducesResponseType(typeof(ApiResponse<ReservationTrainerDetailDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrainerDetail(int trainerId)
        {
            var data = await _reservationAppService.GetTrainerDetailAsync(trainerId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationTrainerDetailDto>
                {
                    Success = false,
                    Message = "教练不存在"
                });
            }

            return Ok(new ApiResponse<ReservationTrainerDetailDto>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpGet("trainers/{trainerId}/booking")]
        [ProducesResponseType(typeof(ApiResponse<ReservationBookingPageDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBookingPage(int trainerId, [FromQuery] int memberId)
        {
            var data = await _reservationAppService.GetBookingPageAsync(trainerId, memberId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationBookingPageDto>
                {
                    Success = false,
                    Message = "预约页数据不存在"
                });
            }

            return Ok(new ApiResponse<ReservationBookingPageDto>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpPost("reservations")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCreateResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateReservation([FromBody] ReservationCreateRequest request)
        {
            var data = await _reservationAppService.CreateReservationAsync(request);
            if (data == null)
            {
                return BadRequest(new ApiResponse<ReservationCreateResultDto>
                {
                    Success = false,
                    Message = "预约创建失败，请检查课程余量和时间段"
                });
            }

            return Ok(new ApiResponse<ReservationCreateResultDto>
            {
                Success = true,
                Message = "预约成功",
                Data = data
            });
        }

        [HttpGet("reservations")]
        [ProducesResponseType(typeof(ApiResponse<List<ReservationOrderItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReservations([FromQuery] int memberId, [FromQuery] string? status = null)
        {
            var data = await _reservationAppService.GetMemberReservationsAsync(memberId, status);
            return Ok(new ApiResponse<List<ReservationOrderItemDto>>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }

        [HttpPost("reservations/cancel")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CancelReservation([FromBody] ReservationCancelRequest request)
        {
            var success = await _reservationAppService.CancelReservationAsync(request);
            return Ok(new ApiResponse
            {
                Success = success,
                Message = success ? "取消成功" : "取消失败"
            });
        }

        [HttpGet("profile")]
        [ProducesResponseType(typeof(ApiResponse<ReservationProfileDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfile([FromQuery] int memberId)
        {
            var data = await _reservationAppService.GetProfileAsync(memberId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationProfileDto>
                {
                    Success = false,
                    Message = "会员不存在"
                });
            }

            return Ok(new ApiResponse<ReservationProfileDto>
            {
                Success = true,
                Message = "获取成功",
                Data = data
            });
        }
    }
}
