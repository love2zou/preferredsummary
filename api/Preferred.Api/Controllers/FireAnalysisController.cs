using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Preferred.Api.Services;
using Preferred.Api.Models;

namespace Preferred.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class FireAnalysisController : ControllerBase
    {
        private readonly IFireAnalysisService _service;

        public FireAnalysisController(IFireAnalysisService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(524288000)]
        [ProducesResponseType(typeof(ApiResponse<FireUploadResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Upload()
        {
            try
            {
                if (Request.Form?.Files == null || Request.Form.Files.Count == 0)
                {
                    return BadRequest(new ApiErrorResponse { Message = "未找到上传文件" });
                }
                var file = Request.Form.Files[0];
                var result = await _service.SaveUploadAsync(file.FileName, file.Length, file.OpenReadStream());
                return Ok(new ApiResponse<FireUploadResult> { Success = true, Message = "上传成功", Data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "上传失败", Details = ex.Message });
            }
        }

        [HttpPost("analyze")]
        [ProducesResponseType(typeof(ApiResponse<FireAnalysisResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FileId))
                {
                    return BadRequest(new ApiErrorResponse { Message = "参数无效" });
                }
                var result = await _service.AnalyzeAsync(request.FileId, request.Params ?? new FireAnalysisParams());
                return Ok(new ApiResponse<FireAnalysisResult> { Success = true, Message = "分析完成", Data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse { Message = "分析失败", Details = ex.Message });
            }
        }

        public class AnalyzeRequest
        {
            public string FileId { get; set; }
            public FireAnalysisParams Params { get; set; }
        }
    }
}
