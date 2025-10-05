using System;
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
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet("bound-coaches")]
        [ProducesResponseType(typeof(ApiResponse<BoundCoachDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBoundCoaches([FromQuery] int memberId)
        {
            var data = await _bookingService.GetBoundCoachesAsync(memberId);
            return Ok(new ApiResponse<BoundCoachDto[]>{ Success = true, Message = "获取成功", Data = data.ToArray() });
        }

        [HttpGet("bound-members")]
        [ProducesResponseType(typeof(ApiResponse<BoundMemberDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBoundMembers([FromQuery] int coachId)
        {
            var data = await _bookingService.GetBoundMembersAsync(coachId);
            return Ok(new ApiResponse<BoundMemberDto[]>{ Success = true, Message = "获取成功", Data = data.ToArray() });
        }

        [HttpPost("bind")]
        public async Task<IActionResult> BindCoach([FromBody] CoachMemberRelation dto)
        {
            var ok = await _bookingService.BindCoachAsync(dto.MemberId, dto.CoachId);
            return Ok(new ApiResponse { Success = ok, Message = ok ? "绑定成功" : "绑定失败" });
        }

        [HttpPost("unbind")]
        public async Task<IActionResult> UnbindCoach([FromBody] CoachMemberRelation dto)
        {
            var ok = await _bookingService.UnbindCoachAsync(dto.MemberId, dto.CoachId);
            return Ok(new ApiResponse { Success = ok, Message = ok ? "解绑成功" : "解绑失败" });
        }

        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(ApiResponse<AvailableSlotDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] int coachId, [FromQuery] string bookDate)
        {
            var date = DateTime.Parse(bookDate);
            var slots = await _bookingService.GetAvailableSlotsAsync(coachId, date);
            return Ok(new ApiResponse<AvailableSlotDto[]>{ Success = true, Message = "获取成功", Data = slots.ToArray() });
        }

        [HttpPost("batch-create")]
        public async Task<IActionResult> BatchCreate([FromBody] CreateBatchRequest request)
        {
            var ok = await _bookingService.BatchCreateAsync(request);
            return Ok(new ApiResponse { Success = ok, Message = ok ? "创建成功" : "创建失败" });
        }

        [HttpGet("list")]
        [ProducesResponseType(typeof(ApiResponse<BookingItemDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] int memberId)
        {
            var data = await _bookingService.ListAsync(memberId);
            return Ok(new ApiResponse<BookingItemDto[]>{ Success = true, Message = "获取成功", Data = data.ToArray() });
        }

        [HttpGet("listbycoach")]
        [ProducesResponseType(typeof(ApiResponse<BookingItemDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListByCoach([FromQuery] int coachId)
        {
            var data = await _bookingService.ListByCoachAsync(coachId);
            return Ok(new ApiResponse<BookingItemDto[]>{ Success = true, Message = "获取成功", Data = data.ToArray() });
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelRequest request)
        {
            var ok = await _bookingService.CancelAsync(request.Id);
            return Ok(new ApiResponse { Success = ok, Message = ok ? "取消成功" : "取消失败" });
        }

        [HttpGet("reserved-by-date")]
        [ProducesResponseType(typeof(ApiResponse<ReservedByDateDto[]>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReservedByDate([FromQuery] int coachId, [FromQuery] string bookDate)
        {
            var date = DateTime.Parse(bookDate);
            var data = await _bookingService.GetReservedByDateAsync(coachId, date);
            return Ok(new ApiResponse<ReservedByDateDto[]>
            {
                Success = true,
                Message = "获取成功",
                Data = data.ToArray()
            });
        }
    }
}