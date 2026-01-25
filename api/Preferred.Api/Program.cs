using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Text;

namespace Preferred.Api
{
    public class Program
    {
        private sealed class FilteringTextWriter : TextWriter
        {
            private readonly TextWriter _inner;
            private readonly Func<string, bool> _shouldFilter;
            private readonly StringBuilder _line = new StringBuilder();

            public FilteringTextWriter(TextWriter inner, Func<string, bool> shouldFilter)
            {
                _inner = inner ?? TextWriter.Null;
                _shouldFilter = shouldFilter ?? (_ => false);
            }

            public override Encoding Encoding => _inner.Encoding;

            public override void Write(char value)
            {
                if (value == '\r') return;
                if (value == '\n')
                {
                    FlushLine();
                    return;
                }
                _line.Append(value);
            }

            public override void Write(string value)
            {
                if (value == null) return;
                foreach (var ch in value) Write(ch);
            }

            public override void WriteLine(string value)
            {
                Write(value);
                Write('\n');
            }

            public override void Flush()
            {
                FlushLine();
                _inner.Flush();
            }

            private void FlushLine()
            {
                if (_line.Length == 0) return;
                var s = _line.ToString();
                _line.Clear();
                if (!_shouldFilter(s))
                {
                    _inner.WriteLine(s);
                }
            }
        }

        public static void Main(string[] args)
        {
            // 确保控制台输出编码为 UTF-8
            Console.OutputEncoding = Encoding.UTF8;
            
            // 注册 CodePagesEncodingProvider 以支持更多编码（如 GB18030）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Environment.SetEnvironmentVariable("OPENCV_LOG_LEVEL", "OFF");
            Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_DEBUG", "0");
            Environment.SetEnvironmentVariable("OPENCV_FFMPEG_DEBUG", "0");
            Environment.SetEnvironmentVariable("OPENCV_FFMPEG_LOGLEVEL", "-8");

            Func<string, bool> shouldFilterNative = s =>
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                var t = s.TrimStart();
                return t.StartsWith("[h264 @", StringComparison.OrdinalIgnoreCase)
                       || t.Contains("error while decoding MB", StringComparison.OrdinalIgnoreCase)
                       || t.Contains("cabac decode", StringComparison.OrdinalIgnoreCase)
                       || t.Contains("decode_slice_header error", StringComparison.OrdinalIgnoreCase)
                       || t.Contains("no frame!", StringComparison.OrdinalIgnoreCase);
            };
            Console.SetError(new FilteringTextWriter(Console.Error, shouldFilterNative));

            // 配置 Serilog
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30, // 保留30天的日志文件
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1) // 每秒刷新到磁盘
                )
                .CreateLogger();

            try
            {
                Log.Information("应用程序启动中...");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用程序启动失败");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog() // 使用 Serilog
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
