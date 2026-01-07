using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Preferred.Api.Models;
using Zwav.Application.Parsing;
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

                // 语义上建议返回 202 Accepted（任务已接收进入队列），你也可以保持 200
                return Accepted(new ApiResponse<CreateAnalysisResponse>
                {
                    Success = true,
                    Message = "解析任务已创建并进入队列",
                    Data = new CreateAnalysisResponse
                    {
                        AnalysisGuid = result.AnalysisGuid,
                        Status = result.Status
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

        /// <summary>分页查询录波解析任务列表</summary>
        [HttpGet]
        public async Task<IActionResult> GetListAsync(
            [FromQuery] string status,
            [FromQuery] string keyword,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var data = await _app.QueryAsync(status, keyword, fromUtc, toUtc, page, pageSize);
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

        /// <summary>获取录波解析任务详情（包含文件信息和配置摘要）</summary>
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

        /// <summary>获取 CFG 配置信息</summary>
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

        /// <summary>获取通道列表</summary>
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

        /// <summary>获取 HDR 头文件信息</summary>
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

        /// <summary>获取录波波形数据（支持区间查询、分页与下采样）</summary>
        /// <param name="analysisGuid">录波分析唯一标识，用于定位对应的分析记录</param>
        /// <param name="fromSample">起始采样点编号（SampleNo）与 toSample 同时传入时生效，表示按采样点区间查询。</param>
        /// <param name="toSample">结束采样点编号（SampleNo）与 fromSample 同时传入时生效，表示按采样点区间查询。</param>
        /// <param name="limit">本次最多返回的采样点数量</param>
        /// <param name="channels">模拟量通道索引集合（1~70），支持逗号分隔或区间格式（如 "1,2,5-8"）。</param>
        /// <param name="digitals">开关量通道索引集合（1~50），支持逗号分隔或区间格式。</param>
        /// <param name="downSample">下采样倍率（抽点参数）。N 表示仅返回 SampleNo 能被 N 整除的采样点；小于等于 0 时按 1 处理（不下采样）。</param>
        [HttpGet("{analysisGuid}/get-wavedata")]
        public async Task<IActionResult> GetWaveDataAsync(
            [FromRoute] string analysisGuid,
            [FromQuery] int? fromSample,
            [FromQuery] int? toSample,
            [FromQuery] int? limit = 20000,
            [FromQuery] string channels = null,
            [FromQuery] string digitals = null,
            [FromQuery] int downSample = 1)
        {
            try
            {
                var data = await _app.GetWaveDataAsync(analysisGuid, fromSample, toSample, limit, channels, digitals, downSample);
                if (data == null) return NotFound(new ApiErrorResponse { Message = "任务不存在" });

                return Ok(new ApiResponse<WaveDataPageDto> { Success = true, Message = "查询成功", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "查询波形数据失败", Details = ex.Message });
            }
        }

        /// <summary>下载原始录波文件</summary>
        [HttpGet("{analysisGuid}/download")]
        public async Task<IActionResult> DownloadAsync([FromRoute] string analysisGuid, CancellationToken ct)
        {
            try
            {
                var (path, name) = await _app.GetFileDownloadInfoAsync(analysisGuid, ct);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                {
                    return NotFound(new ApiErrorResponse { Message = "文件不存在" });
                }

                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                // 使用 application/octet-stream 强制下载
                return File(stream, "application/octet-stream", name);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "下载文件失败", Details = ex.Message });
            }
        }

        /// <summary>删除录波解析任务</summary>
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

        /// <summary>导出波形数据（CSV）</summary>
        [HttpGet("{analysisGuid}/export")]
        public async Task<IActionResult> ExportAsync(
            [FromRoute] string analysisGuid,
            [FromQuery] bool enabledOnly = true)
        {
            try
            {
                // 这里用 MemoryStream 简单好用；如果你担心文件很大，可改“写到临时文件再 FileStreamResult”
                var ms = new MemoryStream();
                var fileName = await _app.ExportWaveDataAsync(
                    analysisGuid: analysisGuid,
                    output: ms);

                ms.Position = 0;
                return File(ms, "text/csv; charset=utf-8", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "导出失败", Details = ex.Message });
            }
        }
    }
}
