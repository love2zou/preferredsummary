using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Preferred.Api.Services;
using System.Threading.Tasks;

namespace Preferred.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ISystemMonitorService _systemMonitorService;
        private readonly ILogger<TestController> _logger;

        public TestController(ISystemMonitorService systemMonitorService, ILogger<TestController> logger)
        {
            _systemMonitorService = systemMonitorService;
            _logger = logger;
        }

        /// <summary>
        /// 测试系统监控服务
        /// </summary>
        [HttpGet("system-monitor")]
        public async Task<IActionResult> TestSystemMonitor()
        {
            _logger.LogInformation("开始测试系统监控服务...");
            
            try
            {
                var result = await _systemMonitorService.GetCurrentSystemResourceAsync();
                _logger.LogInformation($"系统监控测试完成 - CPU: {result.CpuUsage}%, 内存: {result.MemoryUsage}%, 磁盘: {result.DiskUsage}%");
                
                return Ok(new
                {
                    success = true,
                    message = "系统监控测试完成",
                    data = result
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "系统监控测试失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}