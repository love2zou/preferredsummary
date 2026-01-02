using System;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Zwav.Application.Parsing
{
    public class HdrParser
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        /// <summary>
        /// 解析 HDR XML 文本。遇到不规范 XML（前导控制字符/多根/片段）会自动容错。
        /// </summary>
        public HdrParseResult Parse(string xmlText, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(xmlText)) return null;

            // 1) 清洗：去掉常见前导不可见字符（导致 XDocument.Parse 报错的高频原因）
            xmlText = SanitizeXmlText(xmlText);

            XContainer root;
            try
            {
                // 2) 优先按“标准 Document”解析（最快、兼容正常 XML）
                var doc = XDocument.Parse(xmlText, LoadOptions.None);
                root = doc;
            }
            catch (Exception exDoc)
            {
                // 3) fallback：按“片段/非严格 XML”解析
                // HDR 有时并非严格单根，或者前后有额外内容
                try
                {
                    root = ParseAsFragment(xmlText);
                }
                catch (Exception exFrag)
                {
                    error = $"XDocument.Parse failed: {exDoc.Message}; Fragment parse failed: {exFrag.Message}";
                    return null;
                }
            }

            // 4) 一次性缓存所有元素并按 LocalName 分组，避免多次 Descendants 全树扫描
            var all = root.Descendants().ToList();
            var byName = all
                .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            string GetFirst(string localName)
            {
                return byName.TryGetValue(localName, out var list) && list.Count > 0
                    ? (list[0].Value ?? string.Empty).Trim()
                    : string.Empty;
            }

            IEnumerable<XElement> GetAll(string localName)
            {
                return byName.TryGetValue(localName, out var list) ? list : Enumerable.Empty<XElement>();
            }

            string Val(XElement el, string key)
            {
                if (el == null) return string.Empty;

                // 属性：忽略大小写
                foreach (var a in el.Attributes())
                {
                    if (string.Equals(a.Name.LocalName, key, StringComparison.OrdinalIgnoreCase))
                        return (a.Value ?? string.Empty).Trim();
                }

                // 子节点：先 key，再 Key
                foreach (var c in el.Elements())
                {
                    var ln = c.Name.LocalName;
                    if (string.Equals(ln, key, StringComparison.Ordinal) ||
                        (ln.Length == key.Length &&
                        char.ToUpperInvariant(key[0]) == ln[0] &&
                        string.Equals(ln, char.ToUpperInvariant(key[0]) + key.Substring(1), StringComparison.Ordinal)))
                    {
                        return (c.Value ?? string.Empty).Trim();
                    }
                }

                return string.Empty;
            }

            // 业务字段
            var deviceParams = GetAll("DeviceInfo")
                .Select(el => new NameValue { Name = Val(el, "name"), Value = Val(el, "value") })
                .ToList();

            var faultStartTime = GetFirst("FaultStartTime");
            var faultKeepingTime = GetFirst("FaultKeepingTime");

            var tripInfos = GetAll("TripInfo")
                .Select(el => new TripInfo
                {
                    Time = Val(el, "time"),
                    Name = Val(el, "name"),
                    Phase = Val(el, "phase"),
                    Value = Val(el, "value")
                })
                .ToList();

            var faultInfos = GetAll("FaultInfo")
                .Select(el => new FaultInfo
                {
                    Name = Val(el, "name"),
                    Value = Val(el, "value"),
                    Unit = Val(el, "unit")
                })
                .ToList();

            var digitalStatus = GetAll("DigitalStatus")
                .Select(el => new NameValue { Name = Val(el, "name"), Value = Val(el, "value") })
                .ToList();

            var digitalEvents = GetAll("DigitalEvent")
                .Select(el => new DigitalEvent
                {
                    Time = Val(el, "time"),
                    Name = Val(el, "name"),
                    Value = Val(el, "value")
                })
                .ToList();

            var settings = GetAll("SettingValue")
                .Select(el => new SettingValue
                {
                    Name = Val(el, "name"),
                    Value = Val(el, "value"),
                    Unit = Val(el, "unit")
                })
                .ToList();

            var relayEnaValues = GetAll("RelayEnaValue")
                .Select(el => new RelayEnaValue { Name = Val(el, "name"), Value = Val(el, "value") })
                .ToList();

            return new HdrParseResult
            {
                FaultStartTime = faultStartTime,
                FaultKeepingTime = faultKeepingTime,

                DeviceInfoJson = JsonSerializer.Serialize(deviceParams, JsonOpts),
                TripInfoJSON = JsonSerializer.Serialize(tripInfos, JsonOpts),
                FaultInfoJson = JsonSerializer.Serialize(faultInfos, JsonOpts),
                DigitalStatusJson = JsonSerializer.Serialize(digitalStatus, JsonOpts),
                DigitalEventJson = JsonSerializer.Serialize(digitalEvents, JsonOpts),
                SettingValueJson = JsonSerializer.Serialize(settings, JsonOpts),
                RelayEnaValueJSON = JsonSerializer.Serialize(relayEnaValues, JsonOpts)
            };
        }

        public HdrParseResult Parse(string xmlText)
            => Parse(xmlText, out _);

        private static string SanitizeXmlText(string s)
        {
            // 去掉 UTF-8 BOM 和 \0 等前导垃圾字符
            // 这些经常导致 “Data at the root level is invalid.” / “Unexpected token” 等错误
            return s.TrimStart('\uFEFF', '\0', ' ', '\t', '\r', '\n');
        }

        /// <summary>
        /// 将不严格 XML 当片段解析（容错多根、前后杂质等）
        /// </summary>
        private static XContainer ParseAsFragment(string xmlText)
        {
            // 用 XmlReader 的 Fragment 解析，把片段包进一个虚拟根节点
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                CheckCharacters = true
            };

            using var sr = new StringReader(xmlText);
            using var xr = XmlReader.Create(sr, settings);

            var fakeRoot = new XElement("Root");
            while (!xr.EOF)
            {
                if (xr.NodeType == XmlNodeType.Element)
                {
                    var node = XElement.ReadFrom(xr) as XElement;
                    if (node != null) fakeRoot.Add(node);
                }
                else
                {
                    xr.Read();
                }
            }

            return new XDocument(fakeRoot);
        }
    }
}