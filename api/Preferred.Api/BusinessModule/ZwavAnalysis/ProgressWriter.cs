using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;

internal sealed class ProgressWriter
{
    private readonly ApplicationDbContext _ctx;
    private readonly int _analysisId;
    private readonly CancellationToken _ct;

    private int _lastProgress = -1;
    private string _lastStatus = null;
    private DateTime _lastWriteUtc = DateTime.MinValue;

    public ProgressWriter(ApplicationDbContext ctx, int analysisId, CancellationToken ct)
    {
        _ctx = ctx;
        _analysisId = analysisId;
        _ct = ct;
    }

    // “尝试更新”：不 await，减少调用处阻塞；内部做限频
    public void TryUpdate(string status, int progress)
    {
        // fire-and-forget 在 ASP.NET Core 不推荐乱用线程；这里仅做轻量限频判断，不做真正异步任务。
        // 真正写库仍通过 UpdateAsync。
        _lastStatus = status;
        _lastProgress = progress;
    }

    public async Task UpdateAsync(string status, int progress, string error = null)
    {
        progress = Math.Clamp(progress, 0, 100);
        var now = DateTime.UtcNow;

        // 限频策略：进度不变不写；或距离上次写 < 300ms 且变化 < 1% 不写
        var tooFrequent = (now - _lastWriteUtc).TotalMilliseconds < 300 && progress == _lastProgress && status == _lastStatus;
        if (tooFrequent) return;

        var a = await _ctx.ZwavAnalyses
            .Where(x => x.Id == _analysisId)
            .FirstOrDefaultAsync(_ct);
        if (a == null) throw new InvalidOperationException($"未找到 Id={_analysisId} 的 ZwavAnalysis 记录。");
        a.Status = status;
        a.Progress = progress;
        a.ErrorMessage = error;
        a.UpdTime = now;
        await _ctx.SaveChangesAsync(_ct);

        _lastWriteUtc = now;
        _lastProgress = progress;
        _lastStatus = status;
    }
}