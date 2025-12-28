using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Preferred.Api.Models;
using Preferred.Api.Services;
using SixLabors.ImageSharp;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace Preferred.Api.Controllers
{
    /// <summary>
    /// 录波文件解析控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ZwavAnalysesController : ControllerBase
    {
        private readonly IZwavAnalysisAppService _app;

        public ZwavAnalysesController(IZwavAnalysisAppService app)
        {
            _app = app;
        }

        /// <summary>上传ZWAV/ZIP并创建解析任务</summary>
        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1024L * 1024 * 1024)] // 1GB
        [ProducesResponseType(typeof(CreateAnalysisResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreateAnalysisResponse>> Create(
            IFormFile file,
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                return BadRequest("请选择要上传的文件");

            // 扩展名校验：支持 .ZWAV / .ZIP（大小写不敏感）
            var fileName = Path.GetFileName(file.FileName ?? string.Empty);
            var ext = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(ext) ||
                (!ext.Equals(".zwav", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest("只支持 ZWAV(.zwav) 或 ZIP(.zip) 格式的文件");
            }

            // 这里不强制要求 ContentType（ZWAV 常见会是 application/octet-stream）
            // 你可以按需加白名单，但不建议过严

            try
            {
                // 传 ct 更规范（如果你 service 还没接 ct，先不传也可以）
                var result = await _app.CreateAnalysisAsync(file /*, ct*/);

                return Accepted(new CreateAnalysisResponse
                {
                    AnalysisGuid = result.AnalysisGuid,
                    Status = result.Status
                });
            }
            catch (OperationCanceledException)
            {
                // .NET Core 3.1 没有 Status499 常量，直接写 499
                return StatusCode(499, "client canceled request");
            }
        }

        /// <summary>查询任务状态</summary>
        [HttpGet("{analysisGuid}")]
        public async Task<ActionResult<AnalysisStatusResponse>> GetStatus([FromRoute] string analysisGuid)
        {
            var status = await _app.GetStatusAsync(analysisGuid);
            if (status == null) return NotFound();

            return Ok(status);
        }

        /// <summary>获取meta（文件+CFG+HDR+DAT概览+通道列表）</summary>
        [HttpGet("{analysisGuid}/meta")]
        public async Task<ActionResult<AnalysisMetaResponse>> GetMeta([FromRoute] string analysisGuid)
        {
            var meta = await _app.GetMetaAsync(analysisGuid);
            if (meta == null) return NotFound();
            return Ok(meta);
        }

        /// <summary>分页获取样本表格数据</summary>
        [HttpGet("{analysisGuid}/samples")]
        public async Task<ActionResult<SamplesResponse>> GetSamples(
            [FromRoute] string analysisGuid,
            [FromQuery] long start = 0,
            [FromQuery] int count = 200,
            [FromQuery] string channels = "1,2,3")
        {
            if (count <= 0 || count > 5000) return BadRequest("count must be 1..5000");

            var ch = (channels ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            var resp = await _app.GetSamplesAsync(analysisGuid, start, count, ch);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        /// <summary>获取波形下采样数据</summary>
        [HttpGet("{analysisGuid}/wave")]
        public async Task<ActionResult<WaveResponse>> GetWave(
            [FromRoute] string analysisGuid,
            [FromQuery] long start = 0,
            [FromQuery] long end = 20000,
            [FromQuery] string channels = "1,2,3",
            [FromQuery] int maxPoints = 2000,
            [FromQuery] string mode = "Envelope") // Envelope/Lttb
        {
            if (end < start) return BadRequest("end must be >= start");
            if (maxPoints < 100 || maxPoints > 200000) return BadRequest("maxPoints must be 100..200000");

            var ch = (channels ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            var resp = await _app.GetWaveAsync(analysisGuid, start, end, ch, maxPoints, mode);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        /// <summary>导出CSV（占位：你后续可做真正的流式导出）</summary>
        [HttpGet("{analysisGuid}/export/csv")]
        public async Task<IActionResult> ExportCsv(
            [FromRoute] string analysisGuid,
            [FromQuery] long start = 0,
            [FromQuery] long end = 20000,
            [FromQuery] string channels = "1,2,3")
        {
            var ch = (channels ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            var file = await _app.ExportCsvAsync(analysisGuid, start, end, ch);
            if (file == null) return NotFound();

            return File(file.Value.Content, "text/csv; charset=utf-8", file.Value.FileName);
        }

        /// <summary>删除任务（占位：会同时清理缓存/文件）</summary>
        [HttpDelete("{analysisGuid}")]
        public async Task<IActionResult> Delete([FromRoute] string analysisGuid)
        {
            var ok = await _app.DeleteAsync(analysisGuid);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
