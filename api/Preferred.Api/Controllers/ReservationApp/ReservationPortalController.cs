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
    public class ReservationPortalController : ControllerBase
    {
        private readonly IReservationPortalService _reservationPortalService;

        public ReservationPortalController(IReservationPortalService reservationPortalService)
        {
            _reservationPortalService = reservationPortalService;
        }

        [HttpGet("member/flow")]
        [ProducesResponseType(typeof(ApiResponse<ReservationFlowResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMemberFlow([FromQuery] int memberId)
        {
            var data = await _reservationPortalService.GetMemberFlowAsync(memberId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationFlowResponseDto> { Success = false, Message = "会员不存在" });
            }

            return Ok(new ApiResponse<ReservationFlowResponseDto> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("member/commerce")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCommerceResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCommerce([FromQuery] int memberId)
        {
            var data = await _reservationPortalService.GetCommerceAsync(memberId);
            return Ok(new ApiResponse<ReservationCommerceResponseDto> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("member/center")]
        [ProducesResponseType(typeof(ApiResponse<ReservationMemberCenterResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMemberCenter([FromQuery] int memberId)
        {
            var data = await _reservationPortalService.GetMemberCenterAsync(memberId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationMemberCenterResponseDto> { Success = false, Message = "会员不存在" });
            }

            return Ok(new ApiResponse<ReservationMemberCenterResponseDto> { Success = true, Message = "获取成功", Data = data });
        }

        [HttpGet("coach/workbench")]
        [ProducesResponseType(typeof(ApiResponse<ReservationCoachWorkbenchResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCoachWorkbench([FromQuery] int coachUserId)
        {
            var data = await _reservationPortalService.GetCoachWorkbenchAsync(coachUserId);
            if (data == null)
            {
                return NotFound(new ApiResponse<ReservationCoachWorkbenchResponseDto> { Success = false, Message = "教练不存在" });
            }

            return Ok(new ApiResponse<ReservationCoachWorkbenchResponseDto> { Success = true, Message = "获取成功", Data = data });
        }
    }
}
