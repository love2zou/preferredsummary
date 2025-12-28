using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 数据解析服务实现
    /// </summary>
    public class DataParserService : IDataParserService
    {
        private readonly ILogger<DataParserService> _logger;
        private static readonly ConcurrentDictionary<string, DataParseResponse> _parseTasks = new ConcurrentDictionary<string, DataParseResponse>();
        
        public DataParserService(ILogger<DataParserService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 开始解析文件
        /// </summary>
        public async Task<string> StartParseAsync(IFormFile file)
        {
            var taskId = Guid.NewGuid().ToString();
            
            var parseResponse = new DataParseResponse
            {
                TaskId = taskId,
                Status = ParseStatus.Processing,
                Progress = 0,
                Logs = new List<ParseLog>(),
                Result = new ParseResult()
            };
            
            _parseTasks[taskId] = parseResponse;
            
            // 异步执行解析任务
            _ = Task.Run(async () => await ProcessFileAsync(taskId, file));
            
            return taskId;
        }

        /// <summary>
        /// 获取解析状态
        /// </summary>
        public async Task<DataParseResponse> GetParseStatusAsync(string taskId)
        {
            _parseTasks.TryGetValue(taskId, out var response);
            return response ?? new DataParseResponse { TaskId = taskId, Status = ParseStatus.Failed };
        }

        /// <summary>
        /// 导出为 Excel 文件
        /// </summary>
        public async Task<(Stream stream, string fileName, string contentType)> ExportToExcelAsync(string taskId, string fileName = null)
        {
            if (!_parseTasks.TryGetValue(taskId, out var parseResponse) || 
                parseResponse.Status != ParseStatus.Completed ||
                parseResponse.Result?.Data == null)
            {
                throw new InvalidOperationException("解析任务不存在或未完成");
            }

            var result = parseResponse.Result;
            fileName = fileName ?? $"parsed_data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            var stream = new MemoryStream();
            
            using (var package = new ExcelPackage(stream))
            {
                var worksheet = package.Workbook.Worksheets.Add("解析数据");
                
                // 设置表头
                for (int i = 0; i < result.Headers.Count; i++)
                {
                    worksheet.Cells[1, i + 1].Value = result.Headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }
                
                // 填充数据
                for (int row = 0; row < result.Data.Count; row++)
                {
                    var dataRow = result.Data[row];
                    for (int col = 0; col < result.Headers.Count; col++)
                    {
                        var header = result.Headers[col];
                        var value = dataRow.ContainsKey(header) ? dataRow[header] : "";
                        worksheet.Cells[row + 2, col + 1].Value = value?.ToString() ?? "";
                    }
                }
                
                // 自动调整列宽
                worksheet.Cells.AutoFitColumns();
                
                await package.SaveAsync();
            }
            
            stream.Position = 0;
            return (stream, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        /// <summary>
        /// 清理过期的解析任务
        /// </summary>
        public async Task CleanupExpiredTasksAsync()
        {
            var expiredTasks = _parseTasks.Where(kvp => 
                DateTime.Now.Subtract(kvp.Value.Logs.LastOrDefault()?.Timestamp ?? DateTime.Now).TotalHours > 24)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var taskId in expiredTasks)
            {
                _parseTasks.TryRemove(taskId, out _);
            }
            
            _logger.LogInformation($"清理了 {expiredTasks.Count} 个过期的解析任务");
        }

        /// <summary>
        /// 处理文件解析
        /// </summary>
        private async Task ProcessFileAsync(string taskId, IFormFile file)
        {
            var parseResponse = _parseTasks[taskId];
            
            try
            {
                _logger.LogInformation($"开始处理解析任务 {taskId}, 文件: {file.FileName}");
                
                AddLog(parseResponse, "开始解析文件...", LogType.Info);
                AddLog(parseResponse, $"文件名: {file.FileName}", LogType.Info);
                AddLog(parseResponse, $"文件大小: {file.Length / 1024.0:F2} KB", LogType.Info);
                
                parseResponse.Progress = 10;
                
                // 立即读取文件内容到内存，避免流被释放
                byte[] fileBytes;
                using (var stream = file.OpenReadStream())
                {
                    AddLog(parseResponse, "正在读取文件到内存...", LogType.Info);
                    fileBytes = new byte[stream.Length];
                    await stream.ReadAsync(fileBytes, 0, fileBytes.Length);
                    AddLog(parseResponse, "文件读取到内存完成", LogType.Success);
                }
                
                parseResponse.Progress = 20;
                
                // 读取文件内容并检测编码
                AddLog(parseResponse, "开始检测文件编码...", LogType.Info);
                string content = ReadFileWithEncodingDetection(fileBytes, parseResponse);
                parseResponse.Progress = 30;
                
                // 解析数据
                AddLog(parseResponse, "开始解析数据内容...", LogType.Info);
                await ParseDataAsync(content, parseResponse);
                parseResponse.Progress = 90;
                
                AddLog(parseResponse, "解析任务全部完成!", LogType.Success);
                parseResponse.Status = ParseStatus.Completed;
                parseResponse.Progress = 100;
                
                _logger.LogInformation($"解析任务 {taskId} 完成成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"解析任务 {taskId} 失败: {ex.Message}");
                AddLog(parseResponse, $"解析失败: {ex.Message}", LogType.Error);
                if (ex.InnerException != null)
                {
                    AddLog(parseResponse, $"详细错误: {ex.InnerException.Message}", LogType.Error);
                }
                parseResponse.Status = ParseStatus.Failed;
                parseResponse.Progress = 0;
            }
        }

        /// <summary>
        /// 读取文件并检测编码
        /// </summary>
        private string ReadFileWithEncodingDetection(byte[] buffer, DataParseResponse parseResponse)
        {
            AddLog(parseResponse, "正在检测文件编码...", LogType.Info);
            
            // 检测编码
            var encoding = DetectEncoding(buffer);
            parseResponse.Result.Encoding = encoding.EncodingName;
            
            AddLog(parseResponse, $"检测到编码格式: {encoding.EncodingName}", LogType.Info);
            
            var content = encoding.GetString(buffer);
            
            // 移除 BOM
            if (content.Length > 0 && content[0] == '\uFEFF')
            {
                content = content.Substring(1);
                AddLog(parseResponse, "检测到 BOM 标记，已自动移除", LogType.Info);
            }
            
            AddLog(parseResponse, "文件编码检测和内容读取完成", LogType.Success);
            return content;
        }

        /// <summary>
        /// 检测文件编码
        /// </summary>
        private Encoding DetectEncoding(byte[] buffer)
        {
            // 检测 BOM
            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return Encoding.UTF8;
            if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode;
            if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            
            // 尝试 UTF-8
            try
            {
                var utf8 = Encoding.UTF8;
                var content = utf8.GetString(buffer);
                // 检查是否有无效字符
                if (!content.Contains('\uFFFD'))
                {
                    // 进一步验证：检查是否包含有效的UTF-8序列
                    var bytes = utf8.GetBytes(content);
                    if (bytes.Length == buffer.Length)
                        return utf8;
                }
            }
            catch { }
            
            // 尝试 GBK/GB2312 (中文编码)
            try
            {
                var gbk = Encoding.GetEncoding("GBK");
                var content = gbk.GetString(buffer);
                // 检查是否包含中文字符或常见ASCII字符
                if (content.Any(c => c >= 0x4E00 && c <= 0x9FFF) || // 中文字符范围
                    content.All(c => c < 128 || (c >= 0x4E00 && c <= 0x9FFF))) // ASCII + 中文
                {
                    return gbk;
                }
            }
            catch { }
            
            // 尝试 GB18030
            try
            {
                var gb18030 = Encoding.GetEncoding("GB18030");
                var content = gb18030.GetString(buffer);
                if (content.Any(c => c >= 0x4E00 && c <= 0x9FFF))
                {
                    return gb18030;
                }
            }
            catch { }
            
            // 默认使用 UTF-8
            return Encoding.UTF8;
        }

        /// <summary>
        /// 解析数据内容
        /// </summary>
        private async Task ParseDataAsync(string content, DataParseResponse parseResponse)
        {
            AddLog(parseResponse, "开始分析文件结构...", LogType.Info);
            
            // 统一换行符
            content = content.Replace("\r\n", "\n").Replace("\r", "\n");
            
            // 按行分割
            var lines = content.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();
                
            AddLog(parseResponse, $"检测到 {lines.Count} 行有效数据", LogType.Info);
            
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("文件内容为空或无有效数据");
            }
            
            parseResponse.Progress = 40;
            
            // 检测分隔符
            var separator = DetectSeparator(lines[0], parseResponse);
            parseResponse.Result.Separator = separator;
            
            parseResponse.Progress = 50;
            
            // 分析前几行数据，判断是否有表头
            bool hasHeader = AnalyzeHeader(lines, separator, parseResponse);
            
            List<string> headers;
            if (hasHeader)
            {
                // 解析表头
                headers = ParseHeaders(lines[0], separator, parseResponse);
                AddLog(parseResponse, "使用第一行作为表头", LogType.Info);
            }
            else
            {
                // 生成默认表头
                var firstRowColumns = separator == " " 
                    ? Regex.Split(lines[0], @"\s+").Where(v => !string.IsNullOrEmpty(v)).Count()
                    : lines[0].Split(separator).Length;
                    
                headers = Enumerable.Range(1, firstRowColumns).Select(i => $"列{i}").ToList();
                AddLog(parseResponse, $"生成默认表头: {string.Join(", ", headers)}", LogType.Info);
            }
            
            parseResponse.Result.Headers = headers;
            parseResponse.Progress = 60;
            
            // 解析数据行
            var data = await ParseDataRowsAsync(lines, headers, separator, parseResponse, hasHeader);
            parseResponse.Result.Data = data;
            parseResponse.Result.TotalRecords = data.Count;
            
            AddLog(parseResponse, $"数据解析完成! 共解析 {data.Count} 条记录", LogType.Success);
        }

        /// <summary>
        /// 分析是否有表头
        /// </summary>
        private bool AnalyzeHeader(List<string> lines, string separator, DataParseResponse parseResponse)
        {
            if (lines.Count < 2) return false;
            
            var firstRow = separator == " " 
                ? Regex.Split(lines[0], @"\s+").Where(v => !string.IsNullOrEmpty(v)).ToList()
                : lines[0].Split(separator).Select(v => v.Trim()).ToList();
                
            var secondRow = separator == " " 
                ? Regex.Split(lines[1], @"\s+").Where(v => !string.IsNullOrEmpty(v)).ToList()
                : lines[1].Split(separator).Select(v => v.Trim()).ToList();
            
            // 如果第一行包含非数字内容，且第二行主要是数字，则认为有表头
            bool firstRowHasText = firstRow.Any(v => !IsNumeric(v) && !string.IsNullOrEmpty(v));
            bool secondRowMostlyNumeric = secondRow.Count(v => IsNumeric(v)) > secondRow.Count / 2;
            
            bool hasHeader = firstRowHasText && secondRowMostlyNumeric;
            
            AddLog(parseResponse, $"表头分析: 第一行有文本={firstRowHasText}, 第二行主要是数字={secondRowMostlyNumeric}, 判断有表头={hasHeader}", LogType.Info);
            
            return hasHeader;
        }

        /// <summary>
        /// 判断字符串是否为数字
        /// </summary>
        private bool IsNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return double.TryParse(value, out _) || 
                   DateTime.TryParse(value, out _) ||
                   value.All(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+');
        }

        /// <summary>
        /// 检测分隔符
        /// </summary>
        private string DetectSeparator(string firstLine, DataParseResponse parseResponse)
        {
            AddLog(parseResponse, $"分析第一行数据: {firstLine.Substring(0, Math.Min(100, firstLine.Length))}...", LogType.Info);
            
            var separators = new Dictionary<string, string>
            {
                { "\t", "制表符" },
                { "|", "竖线" },
                { ",", "逗号" },
                { ";", "分号" }
            };
            
            // 统计各种分隔符的出现次数
            var separatorCounts = new Dictionary<string, int>();
            foreach (var sep in separators)
            {
                var count = firstLine.Split(sep.Key).Length - 1;
                if (count > 0)
                {
                    separatorCounts[sep.Key] = count;
                    AddLog(parseResponse, $"检测到 {count} 个{sep.Value}", LogType.Info);
                }
            }
            
            // 选择出现次数最多的分隔符
            if (separatorCounts.Any())
            {
                var bestSeparator = separatorCounts.OrderByDescending(x => x.Value).First();
                var separatorName = separators[bestSeparator.Key];
                AddLog(parseResponse, $"选择{separatorName}作为分隔符 (出现 {bestSeparator.Value} 次)", LogType.Success);
                return bestSeparator.Key;
            }
            
            // 检查是否是固定宽度格式（多个连续空格）
            if (Regex.IsMatch(firstLine, @"\s{2,}"))
            {
                AddLog(parseResponse, "检测到固定宽度格式（多个空格分隔）", LogType.Info);
                return " ";
            }
            
            // 默认使用空格
            AddLog(parseResponse, "使用空格作为默认分隔符", LogType.Info);
            return " ";
        }

        /// <summary>
        /// 解析表头
        /// </summary>
        private List<string> ParseHeaders(string headerLine, string separator, DataParseResponse parseResponse)
        {
            var headers = separator == " " 
                ? Regex.Split(headerLine, @"\s+").Where(h => !string.IsNullOrEmpty(h)).ToList()
                : headerLine.Split(separator).Select(h => h.Trim()).ToList();
                
            // 清理表头
            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                // 移除引号
                if ((header.StartsWith("\"") && header.EndsWith("\"")) ||
                    (header.StartsWith("'") && header.EndsWith("'")))
                {
                    header = header.Substring(1, header.Length - 2);
                }
                
                // 如果表头为空，生成默认名称
                if (string.IsNullOrWhiteSpace(header))
                {
                    header = $"列{i + 1}";
                }
                
                headers[i] = header;
            }
            
            AddLog(parseResponse, $"解析到 {headers.Count} 个字段: {string.Join(", ", headers)}", LogType.Info);
            return headers;
        }

        /// <summary>
        /// 解析数据行
        /// </summary>
        private async Task<List<Dictionary<string, object>>> ParseDataRowsAsync(
            List<string> lines, List<string> headers, string separator, DataParseResponse parseResponse, bool hasHeader = true)
        {
            AddLog(parseResponse, "开始解析数据行...", LogType.Info);
            
            var data = new List<Dictionary<string, object>>();
            var startIndex = hasHeader ? 1 : 0; // 如果有表头，从第1行开始；否则从第0行开始
            
            AddLog(parseResponse, $"数据行起始位置: 第{startIndex + 1}行", LogType.Info);
            
            for (int i = startIndex; i < lines.Count; i++)
            {
                if (i % 100 == 0 && i > startIndex)
                {
                    AddLog(parseResponse, $"已解析 {i - startIndex} 行数据...", LogType.Info);
                    parseResponse.Progress = 60 + (int)((i - startIndex) * 30.0 / (lines.Count - startIndex));
                    await Task.Delay(10); // 让其他任务有机会执行
                }
                
                var line = lines[i];
                var values = separator == " " 
                    ? Regex.Split(line, @"\s+").Where(v => !string.IsNullOrEmpty(v)).ToList()
                    : line.Split(separator).Select(v => v.Trim()).ToList();
                
                // 清理值
                for (int j = 0; j < values.Count; j++)
                {
                    var value = values[j];
                    
                    // 移除引号
                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    
                    values[j] = value;
                }
                
                // 构建数据行
                var dataRow = new Dictionary<string, object>();
                for (int j = 0; j < Math.Max(headers.Count, values.Count); j++)
                {
                    var header = j < headers.Count ? headers[j] : $"列{j + 1}";
                    var value = j < values.Count ? values[j] : "";
                    dataRow[header] = value;
                }
                
                data.Add(dataRow);
            }
            
            AddLog(parseResponse, $"数据行解析完成，共 {data.Count} 行", LogType.Success);
            return data;
        }

        
        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(DataParseResponse parseResponse, string message, LogType type)
        {
            parseResponse.Logs.Add(new ParseLog
            {
                Timestamp = DateTime.Now,
                Message = message,
                Type = type
            });
        }
    }
}