using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Preferred.Api.Services
{
    public class FireAnalysisService : IFireAnalysisService
    {
        private static string UploadRoot => Path.Combine(AppContext.BaseDirectory, "Uploads");

        public async Task<FireUploadResult> SaveUploadAsync(string fileName, long size, Stream content)
        {
            Directory.CreateDirectory(UploadRoot);
            var fileId = Guid.NewGuid().ToString("N");
            var saveName = fileId + Path.GetExtension(fileName);
            var path = Path.Combine(UploadRoot, saveName);
            using (var fs = File.Create(path))
            {
                await content.CopyToAsync(fs);
            }

            return new FireUploadResult { FileId = fileId, FileName = fileName, Size = size };
        }

        public async Task<FireAnalysisResult> AnalyzeAsync(string fileId, FireAnalysisParams @params)
        {
            var input = Directory.EnumerateFiles(UploadRoot, fileId + "*").FirstOrDefault();
            if (input == null)
            {
                return new FireAnalysisResult
                {
                    Logs = new List<string> { "未找到上传文件" },
                    Kpi = new FireKpi { Fps = "0", CandPct = "0%", Elapsed = "0", MaxArea = "0" },
                    Gallery = new List<FireHit>()
                };
            }

            var tempDir = Path.Combine(UploadRoot, "tmp_" + fileId);
            Directory.CreateDirectory(tempDir);
            var ff = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            var ffbin = string.IsNullOrWhiteSpace(ff) ? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg") : ff;
            var fps = Math.Max(1, Math.Min(6, 4));
            var args = $"-y -i \"{input}\" -vf scale={@params.Width}:-2,fps={fps} \"{Path.Combine(tempDir, "frame_%06d.png")}\"";
            var sw = Stopwatch.StartNew();
            var ok = RunProcess(ffbin, args, out var procLog);

            var logs = new List<string>();
            logs.Add("开始分析: " + Path.GetFileName(input));
            logs.AddRange(procLog.Select(l => l.Length > 120 ? l.Substring(0, 120) : l));

            var frames = Directory.EnumerateFiles(tempDir, "frame_*.png").ToList();
            var hitBuffer = new List<(double score, FireHit hit)>();
            int maxArea = 0;
            double candSum = 0; int candCnt = 0;
            foreach (var fp in frames)
            {
                using var bmp = new Bitmap(fp);
                var W = bmp.Width; var H = bmp.Height;
                int whiteThr = @params.White;
                int fireY = @params.FireY;
                int rg = @params.Rg; int gb = @params.Gb;
                int step = Math.Max(1, @params.Step);
                var mask = new byte[W * H];
                long sumY = 0; int cnt = 0; int onCount = 0;
                for (int y = 0; y < H; y += step)
                {
                    for (int x = 0; x < W; x += step)
                    {
                        var c = bmp.GetPixel(x, y);
                        var r = c.R; var g = c.G; var b = c.B;
                        var Y = (r * 77 + g * 150 + b * 29) >> 8;
                        sumY += Y; cnt++;
                        bool m = false;
                        if (@params.DetectFire)
                        {
                            if (r > g && g > b && (r - g) >= rg && (g - b) >= gb && Y >= fireY)
                            {
                                var s = r + g + b + 1e-6;
                                var rn = r / s;
                                if (rn > 0.40) m = true;
                            }
                        }
                        if (!m && @params.DetectFlash)
                        {
                            if (r >= whiteThr && g >= whiteThr && b >= whiteThr)
                            {
                                var maxc = Math.Max(r, Math.Max(g, b));
                                var minc = Math.Min(r, Math.Min(g, b));
                                if ((maxc - minc) <= 18) m = true;
                            }
                        }
                        if (m)
                        {
                            for (int dy = 0; dy < step && (y + dy) < H; dy++)
                            {
                                var row = (y + dy) * W;
                                for (int dx = 0; dx < step && (x + dx) < W; dx++)
                                {
                                    mask[row + (x + dx)] = 1; onCount++;
                                }
                            }
                        }
                    }
                }
                int areaMin = Math.Max(1, @params.Area);
                var bbox = LargestBBox(mask, W, H, areaMin);
                if (bbox != null)
                {
                    maxArea = Math.Max(maxArea, bbox.area);
                    var score = Math.Log(1 + bbox.area) * 0.6 + 1.0;
                    var type = bbox.area > (@params.Area * 2) ? "fire" : "flash";
                    var url = MakeSvgDataUrl(W, H, type == "flash" ? "#22c55e" : "#ef4444");
                    var idx = ParseFrameIndex(fp);
                    var timeSec = idx / (double)fps;
                    hitBuffer.Add((score, new FireHit { Url = url, Type = type, Time = timeSec, Score = score, W = W, H = H }));
                }
                candSum += (onCount / (double)(W * H)); candCnt++;
            }

            hitBuffer = hitBuffer.OrderByDescending(x => x.score).ToList();
            var topN = Math.Max(1, @params.TopN);
            var gallery = hitBuffer.Take(topN * 10).Select(x => x.hit).ToList();
            sw.Stop();

            var result = new FireAnalysisResult
            {
                Logs = logs,
                Kpi = new FireKpi
                {
                    Fps = frames.Count > 0 ? (frames.Count / Math.Max(0.001, sw.Elapsed.TotalSeconds)).ToString("0.0") : "0.0",
                    CandPct = (candCnt > 0 ? (candSum / candCnt) * 100 : 0).ToString("0.0") + "%",
                    Elapsed = sw.Elapsed.TotalSeconds.ToString("0.00"),
                    MaxArea = maxArea.ToString()
                },
                Gallery = gallery
            };
            try { Directory.Delete(tempDir, true); } catch { }
            return result;
        }

        private static string MakeSvgDataUrl(int w, int h, string stroke)
        {
            var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}'><rect x='20' y='20' width='{w-40}' height='{h-40}' fill='none' stroke='{stroke}' stroke-width='4'/><rect x='0' y='0' width='{w}' height='{h}' fill='url(#g)'/><defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'><stop offset='0' stop-color='#0b1220'/><stop offset='1' stop-color='#0e1527'/></linearGradient></defs></svg>";
            var data = Uri.EscapeDataString(svg);
            return "data:image/svg+xml;charset=UTF-8," + data;
        }

        private static dynamic LargestBBox(byte[] mask, int W, int H, int minArea)
        {
            var n = W * H;
            var vis = new byte[n];
            int[] qx = new int[n]; int[] qy = new int[n];
            int bestArea = 0; int bx1=0, by1=0, bx2=0, by2=0;
            int idx(int x, int y) => y * W + x;
            for (int y = 0; y < H; y++)
            {
                int baseRow = y * W;
                for (int x = 0; x < W; x++)
                {
                    int i = baseRow + x;
                    if (mask[i] == 0 || vis[i] == 1) continue;
                    int head = 0, tail = 0; qx[tail] = x; qy[tail] = y; tail++; vis[i] = 1;
                    int x1 = x, x2 = x, y1 = y, y2 = y; int area = 0;
                    while (head < tail)
                    {
                        int cx = qx[head], cy = qy[head]; head++; area++;
                        if (cx < x1) x1 = cx; if (cx > x2) x2 = cx;
                        if (cy < y1) y1 = cy; if (cy > y2) y2 = cy;
                        if (cx + 1 < W) { int j = idx(cx + 1, cy); if (mask[j] == 1 && vis[j] == 0) { vis[j] = 1; qx[tail] = cx + 1; qy[tail] = cy; tail++; } }
                        if (cx - 1 >= 0) { int j = idx(cx - 1, cy); if (mask[j] == 1 && vis[j] == 0) { vis[j] = 1; qx[tail] = cx - 1; qy[tail] = cy; tail++; } }
                        if (cy + 1 < H) { int j = idx(cx, cy + 1); if (mask[j] == 1 && vis[j] == 0) { vis[j] = 1; qx[tail] = cx; qy[tail] = cy + 1; tail++; } }
                        if (cy - 1 >= 0) { int j = idx(cx, cy - 1); if (mask[j] == 1 && vis[j] == 0) { vis[j] = 1; qx[tail] = cx; qy[tail] = cy - 1; tail++; } }
                    }
                    if (area > bestArea && area >= minArea) { bestArea = area; bx1 = x1; by1 = y1; bx2 = x2; by2 = y2; }
                }
            }
            if (bestArea <= 0) return null;
            return new { x1 = bx1, y1 = by1, x2 = bx2, y2 = by2, area = bestArea };
        }

        private static int ParseFrameIndex(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var m = System.Text.RegularExpressions.Regex.Match(name ?? string.Empty, "(\\d+)$");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var idx)) return idx;
            return 0;
        }

        private static bool RunProcess(string fileName, string args, out List<string> logs)
        {
            logs = new List<string>();
            try
            {
                var psi = new ProcessStartInfo(fileName, args)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string line;
                while ((line = p.StandardError.ReadLine()) != null) logs.Add(line);
                while ((line = p.StandardOutput.ReadLine()) != null) logs.Add(line);
                p.WaitForExit();
                return p.ExitCode == 0 || logs.Count > 0;
            }
            catch (Exception ex)
            {
                logs.Add("调用处理失败: " + ex.Message);
                return false;
            }
        }
    }
}
