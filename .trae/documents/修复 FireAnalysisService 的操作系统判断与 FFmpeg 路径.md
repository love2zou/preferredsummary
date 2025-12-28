## 问题原因
- `api/Preferred.Api/Services/FireAnalysisService.cs:47` 使用 `OperatingSystem.IsWindows()`，该 API 在 `netcoreapp3.1` 不存在，导致编译错误。
- 项目目标框架为 `netcoreapp3.1`（Preferred.Api.csproj:4），需改用兼容方式判断平台。

## 修改项
- 在 `FireAnalysisService.cs` 文件顶部添加 `using System.Runtime.InteropServices;`。
- 将第 47 行替换为：
  - `var ffbin = string.IsNullOrWhiteSpace(ff) ? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg") : ff;`

## 兼容性加强（可选）
- 若 `FFMPEG_PATH` 设置为目录而非二进制路径，自动拼接可执行文件名：
  - 当为目录时：`Path.Combine(ff, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg")`；否则按原值使用。
- 当未找到 ffmpeg，保留现有错误捕获并在 `RunProcess` 日志中追加提示，辅助定位环境问题。

## 验证
- 重新编译 API 项目，确认不再出现 `OperatingSystem.IsWindows` 的编译错误。
- 在 Windows 与非 Windows 环境各运行一次分析端点，确认 ffmpeg 可正常调用（PATH 或 FFMPEG_PATH 均可）。
- 观察返回日志与 KPI，验证分析流程未受影响。