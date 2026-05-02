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
    public class ReservationDemoController : ControllerBase
    {
        private readonly IReservationDemoService _reservationDemoService;

        public ReservationDemoController(IReservationDemoService reservationDemoService)
        {
            _reservationDemoService = reservationDemoService;
        }

        [AllowAnonymous]
        [HttpPost("seed")]
        [ProducesResponseType(typeof(ApiResponse<ReservationDemoSeedResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Seed()
        {
            var data = await _reservationDemoService.SeedDemoDataAsync();
            return Ok(new ApiResponse<ReservationDemoSeedResultDto>
            {
                Success = true,
                Message = data.Message,
                Data = data
            });
        }
    }
}
