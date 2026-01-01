using System;

namespace Preferred.Api.Models
{
    public class ZwavFile
    {
        public int Id { get; set; }
        public string OriginalName { get; set; }
        public long FileSize { get; set; }        // 你表里 INT；建议后面改 BIGINT
        public string StoragePath { get; set; }
         public string ExtractPath { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}