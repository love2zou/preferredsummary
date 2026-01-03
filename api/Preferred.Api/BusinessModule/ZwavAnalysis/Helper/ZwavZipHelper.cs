using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using Preferred.Api.Models;

namespace Zwav.Application.Parsing
{
    public static class ZwavZipHelper
    {
        public static string EnsureExtract(string archivePath, string extractDir)
        {
            Directory.CreateDirectory(extractDir);

            // 简单策略：若目录为空则解压
            if (!Directory.EnumerateFileSystemEntries(extractDir).Any())
            {
                ZipFile.ExtractToDirectory(archivePath, extractDir, true);
            }
            return extractDir;
        }

        public static (string cfg, string hdr, string dat) FindCoreFiles(string dir)
        {
            string Find(string ext) =>
                Directory.GetFiles(dir, $"*{ext}", SearchOption.AllDirectories)
                    .OrderByDescending(x => x.Length)
                    .FirstOrDefault();

            return (Find(".cfg"), Find(".hdr"), Find(".dat"));
        }

       // 读取文件并尝试使用不同编码进行解码
        public static string ReadTextWithFallbacks(string path, string want = "cfg")
        {
            var bytes = File.ReadAllBytes(path);  // 读取所有字节
            var bestText = string.Empty;
            var bestScore = double.NegativeInfinity;
             if (string.Equals(want, "hdr", StringComparison.OrdinalIgnoreCase))
            {
                var encFromXml = DetectXmlEncoding(bytes);
                if (!string.IsNullOrEmpty(encFromXml))
                {
                    try
                    {
                        var encoding = Encoding.GetEncoding(encFromXml);
                        var t = encoding.GetString(bytes).Replace("\r\n", "\n").Replace("\r", "\n");

                        if (TryValidXml(t, out var err))
                            return t;

                        Console.WriteLine($"HDR XML validate failed. enc={encFromXml}, err={err}");
                        // 临时策略：就算校验失败也先返回，避免 HDR 直接“无法解析”
                        // 让后续解析阶段再决定如何容错
                        return t;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"HDR decode failed. enc={encFromXml}, ex={ex.Message}");
                    }
                }
            }
            // 其次，尝试使用常见编码
            string[] encodings = { "utf-8", "gb18030", "gbk" };
            foreach (string encodingName in encodings)
            {
                try
                {
                    Encoding encoding = Encoding.GetEncoding(encodingName);
                    string text = encoding.GetString(bytes).Replace("\r\n", "\n").Replace("\r", "\n");
                     if (!IsLikelyText(text, want)) continue;

                    int badCount = (text.Split(new[] { '\uFFFD' }, StringSplitOptions.None).Length - 1);
                    int cjkCount = Regex.Matches(text, @"[\u4e00-\u9fa5]").Count;
                    int xmlBonus = (string.Equals(want, "hdr", StringComparison.OrdinalIgnoreCase) && TryValidXml(text, out _)) ? 80 : 0;

                    int score = -badCount * 15 + cjkCount + xmlBonus;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestText = text;
                    }
                }
                catch (Exception ex)
                {
                    // 捕捉异常并忽略，继续尝试下一个编码
                    Console.WriteLine($"Error with encoding {encodingName}: {ex.Message}");
                }
            }

            // 返回最佳解码文本，或最终使用默认解码
            if (bestScore > double.NegativeInfinity)
            {
                return bestText;
            }

            // 最后兜底：使用默认编码
            try
            {
                return Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Replace("\r", "\n");
            }
            catch
            {
                // 如果解码失败，返回原始字节转换的文本
                return BitConverter.ToString(bytes);
            }
        }

        // 检查文本是否为可能的文本（针对 cfg 文件）
        private static bool IsLikelyText(string text, string want)
        {
            var lines = text.Split('\n');
            return want == "cfg" ? (text.Contains(",") && lines.Length >= 6) : lines.Length >= 1;
        }

        /// <summary>
        /// 根据 BOM / XML 声明智能检测编码（等价 JS detectXmlEncoding）
        /// </summary>
        private static string DetectXmlEncoding(byte[] u8)
        {
            // BOM
            if (u8.Length >= 3 && u8[0] == 0xEF && u8[1] == 0xBB && u8[2] == 0xBF) return "utf-8";
            if (u8.Length >= 2 && u8[0] == 0xFE && u8[1] == 0xFF) return "utf-16be";
            if (u8.Length >= 2 && u8[0] == 0xFF && u8[1] == 0xFE) return "utf-16le";

            // 取前 256 字节按 ASCII 拼出来，找 encoding="xxx"
            int take = Math.Min(256, u8.Length);
            var ascii = Encoding.ASCII.GetString(u8, 0, take);

            var m = Regex.Match(ascii, "encoding=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var enc = m.Groups[1].Value.Trim().ToLowerInvariant();

                // 映射规则与前端一致
                if (enc == "gb2312") enc = "gb18030";
                if (enc == "utf8") enc = "utf-8";
                if (enc == "unicode") enc = "utf-16le";
                if (enc == "utf-16") enc = "utf-16le";

                // gbk 维持 gbk；gb18030 维持 gb18030；其他直接返回
                return enc;
            }

            return null;
        }

        public static bool TryValidXml(string text, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "empty"; return false; }

            // 关键：去掉常见“看不见”的前导字符
            text = text.TrimStart('\uFEFF', '\0', ' ', '\t', '\r', '\n');

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,

                    // 关键：HDR 有时是片段/不严格 Document，用 Auto 更稳
                    ConformanceLevel = ConformanceLevel.Auto,

                    // 如果你怀疑有非法字符，可先设 false 观察（更宽松）
                    CheckCharacters = true
                };

                using var sr = new StringReader(text);
                using var reader = XmlReader.Create(sr, settings);
                while (reader.Read()) { }
                return true;
            }
            catch (Exception ex) // 不要只抓 XmlException，其他异常也要看
            {
                error = ex.Message;
                return false;
            }
        }
        
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains("\"")) s = s.Replace("\"", "\"\"");
            if (s.Contains(",") || s.Contains("\n") || s.Contains("\r"))
                return $"\"{s}\"";
            return s;
        }

        public static short Get(byte[] digitalWords, int idx)
        {
            if (idx <= 0) return 0;
            if (digitalWords == null || digitalWords.Length < 2) return 0;

            int bit = idx - 1;
            int wordIndex = bit / 16;
            int bitInWord = bit % 16;

            int byteIndex = wordIndex * 2;
            if (byteIndex + 1 >= digitalWords.Length) return 0;

            ushort word = (ushort)(digitalWords[byteIndex] | (digitalWords[byteIndex + 1] << 8));
            return (short)(((word >> bitInWord) & 1) == 1 ? 1 : 0);
        }

        public static readonly Func<ZwavData, double?>[] Getters =
        {
            x => x.Channel1,  x => x.Channel2,  x => x.Channel3,  x => x.Channel4,  x => x.Channel5,
            x => x.Channel6,  x => x.Channel7,  x => x.Channel8,  x => x.Channel9,  x => x.Channel10,
            x => x.Channel11, x => x.Channel12, x => x.Channel13, x => x.Channel14, x => x.Channel15,
            x => x.Channel16, x => x.Channel17, x => x.Channel18, x => x.Channel19, x => x.Channel20,
            x => x.Channel21, x => x.Channel22, x => x.Channel23, x => x.Channel24, x => x.Channel25,
            x => x.Channel26, x => x.Channel27, x => x.Channel28, x => x.Channel29, x => x.Channel30,
            x => x.Channel31, x => x.Channel32, x => x.Channel33, x => x.Channel34, x => x.Channel35,
            x => x.Channel36, x => x.Channel37, x => x.Channel38, x => x.Channel39, x => x.Channel40,
            x => x.Channel41, x => x.Channel42, x => x.Channel43, x => x.Channel44, x => x.Channel45,
            x => x.Channel46, x => x.Channel47, x => x.Channel48, x => x.Channel49, x => x.Channel50,
            x => x.Channel51, x => x.Channel52, x => x.Channel53, x => x.Channel54, x => x.Channel55,
            x => x.Channel56, x => x.Channel57, x => x.Channel58, x => x.Channel59, x => x.Channel60,
            x => x.Channel61, x => x.Channel62, x => x.Channel63, x => x.Channel64, x => x.Channel65,
            x => x.Channel66, x => x.Channel67, x => x.Channel68, x => x.Channel69, x => x.Channel70
        };
    }
}