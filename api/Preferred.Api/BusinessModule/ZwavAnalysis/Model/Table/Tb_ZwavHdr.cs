using System;

namespace Preferred.Api.Models
{
    public class ZwavHdr
    {
        public int Id { get; set; }
        public int AnalysisId { get; set; }
        public string FaultStartTime { get; set; }
        public string FaultKeepingTime { get; set; }

        public string DeviceInfoJson { get; set; }
        public string TripInfoJSON { get; set; }
        public string FaultInfoJson { get; set; }
        public string DigitalStatusJson { get; set; }
        public string DigitalEventJson { get; set; }
        public string SettingValueJson { get; set; }
        public string RelayEnaValueJSON { get; set; }

        public int SeqNo { get; set; }
        public DateTime CrtTime { get; set; }
        public DateTime UpdTime { get; set; }
    }
}
