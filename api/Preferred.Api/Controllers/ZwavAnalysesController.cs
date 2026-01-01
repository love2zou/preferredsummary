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

        /// <summary>上传录波文件并写入 Tb_ZwavFile</summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1024L * 1024 * 1024)] // 1GB
        [ProducesResponseType(typeof(UploadZwavFileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadAsync(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ApiErrorResponse { Message = "请选择要上传的文件" });

            try
            {
                var r = await _app.UploadAsync(file, ct);

                return Ok(new ApiResponse<UploadZwavFileResponse>
                {
                    Success = true,
                    Message = "上传成功",
                    Data = new UploadZwavFileResponse
                    {
                        FileId = r.FileId,
                        OriginalName = r.OriginalName,
                        StoragePath = r.StoragePath,
                        FileSizeBytes = r.FileSizeBytes,
                        Ext = Path.GetExtension(r.OriginalName),
                        CrtTimeUtc = r.CrtTimeUtc
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new ApiErrorResponse { Message = "客户端已取消请求" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "上传失败", Details = ex.Message });
            }
        }
        /// <summary>基于已上传文件(FileId)创建解析任务</summary>
        [HttpPost("create")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(CreateAnalysisResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreateAnalysisResponse>> CreateAsync([FromBody] CreateAnalysisByFileIdRequest req, CancellationToken ct)
        {
            if (req == null || req.FileId <= 0)
                return BadRequest(new ApiErrorResponse { Message = "FileId 不能为空" });

            try
            {
                // 关键：从 Tb_ZwavFile 取文件信息并创建解析任务（由 AppService 做全套校验与入队）
                var result = await _app.CreateAnalysisByFileIdAsync(req.FileId, req.ForceRecreate, ct);

                var progressUrl = Url.Action(nameof(GetStatusAsync), new { analysisGuid = result.AnalysisGuid });

                // 语义上建议返回 202 Accepted（任务已接收进入队列），你也可以保持 200
                return Accepted(new ApiResponse<CreateAnalysisResponse>
                {
                    Success = true,
                    Message = "解析任务已创建并进入队列",
                    Data = new CreateAnalysisResponse
                    {
                        AnalysisGuid = result.AnalysisGuid,
                        Status = result.Status,
                        ProgressUrl = progressUrl
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // 客户端主动取消（前端 abort / 断连）
                return StatusCode(499, new ApiErrorResponse
                {
                    Message = "客户端已取消请求"
                });
            }
            catch (Exception ex)
            {
                // 兜底异常
                return StatusCode(500, new ApiErrorResponse
                {
                    Message = "创建解析任务失败",
                    Details = ex.Message
                });
            }
        }


        /// <summary>查询任务状态</summary>
        [HttpGet("{analysisGuid}")]
        public async Task<ActionResult<AnalysisStatusResponse>> GetStatusAsync([FromRoute] string analysisGuid, CancellationToken ct)
        {
            var status = await _app.GetStatusAsync(analysisGuid, ct);
            if (status == null) return NotFound();

            return Ok(new ApiResponse<AnalysisStatusResponse>
            {
                Success = true,
                Message = "查询任务状态成功",
                Data = status
            });
        }

        // 任务列表
        [HttpGet]
        public async Task<IActionResult> GetListAsync(
            [FromQuery] string status,
            [FromQuery] string keyword,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string orderBy = "CrtTimeDesc",
            CancellationToken ct = default)
        {
            try
            {
                var data = await _app.QueryAsync(status, keyword, fromUtc, toUtc, page, pageSize, orderBy, ct);
                return Ok(new ApiResponse<PagedResult<AnalysisListItemDto>>
                {
                    Success = true,
                    Message = "查询成功",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询任务列表失败", Details = ex.Message });
            }
        }

        // 任务详情
        [HttpGet("{analysisGuid}/detail")]
        public async Task<IActionResult> GetDetailAsync([FromRoute] string analysisGuid, CancellationToken ct)
        {
            try
            {
                var data = await _app.GetDetailAsync(analysisGuid, ct);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<AnalysisDetailDto> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询任务详情失败", Details = ex.Message });
            }
        }

        // CFG
        [HttpGet("{analysisGuid}/cfg")]
        public async Task<IActionResult> GetCfgAsync([FromRoute] string analysisGuid, [FromQuery] bool includeText = false, CancellationToken ct = default)
        {
            try
            {
                var data = await _app.GetCfgAsync(analysisGuid, includeText, ct);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "CFG 不存在或任务不存在" });

                return Ok(new ApiResponse<CfgDto> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询 CFG 失败", Details = ex.Message });
            }
        }

        // Channels
        [HttpGet("{analysisGuid}/channels")]
        public async Task<IActionResult> GetChannelsAsync(
            [FromRoute] string analysisGuid,
            [FromQuery] string type = "All",
            [FromQuery] bool enabledOnly = true,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _app.GetChannelsAsync(analysisGuid, type, enabledOnly, ct);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<ChannelDto[]> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询通道失败", Details = ex.Message });
            }
        }

        // HDR
        [HttpGet("{analysisGuid}/hdr")]
        public async Task<IActionResult> GetHdrAsync([FromRoute] string analysisGuid, CancellationToken ct)
        {
            try
            {
                var data = await _app.GetHdrAsync(analysisGuid, ct);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "HDR 不存在或任务不存在" });

                return Ok(new ApiResponse<HdrDto> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询 HDR 失败", Details = ex.Message });
            }
        }

        // 波形数据
        [HttpGet("{analysisGuid}/get-wavedata")]
        public async Task<IActionResult> GetWaveDataAsync(
            [FromRoute] string analysisGuid,
            [FromQuery] int? fromSample,
            [FromQuery] int? toSample,
            [FromQuery] int? offset,
            [FromQuery] int? limit = 2000,
            [FromQuery] string channels = null,
            [FromQuery] string digitals = null,
            [FromQuery] int downSample = 1,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _app.GetWaveDataAsync(analysisGuid, fromSample, toSample, offset, limit, channels, digitals, downSample, ct);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<WaveDataPageDto> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询波形数据失败", Details = ex.Message });
            }
        }

        // 删除任务（可选，但你接口声明了就必须实现）
        [HttpDelete("{analysisGuid}")]
        public async Task<IActionResult> DeleteAsync([FromRoute] string analysisGuid, [FromQuery] bool deleteFile = false, CancellationToken ct = default)
        {
            try
            {
                var ok = await _app.DeleteAsync(analysisGuid, deleteFile, ct);
                if (!ok) return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<object> { Success = true, Message = "删除成功", Data = null });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "删除任务失败", Details = ex.Message });
            }
        }

    }
}
