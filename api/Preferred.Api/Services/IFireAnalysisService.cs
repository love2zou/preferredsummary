using System.Collections.Generic;
using System.Threading.Tasks;

namespace Preferred.Api.Services
{
    public interface IFireAnalysisService
    {
        Task<FireUploadResult> SaveUploadAsync(string fileName, long size, System.IO.Stream content);
        Task<FireAnalysisResult> AnalyzeAsync(string fileId, FireAnalysisParams @params);
    }

    public class FireUploadResult
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
    }

    public class FireAnalysisResult
    {
        public List<string> Logs { get; set; }
        public FireKpi Kpi { get; set; }
        public List<FireHit> Gallery { get; set; }
    }

    public class FireKpi
    {
        public string Fps { get; set; }
        public string CandPct { get; set; }
        public string Elapsed { get; set; }
        public string MaxArea { get; set; }
    }

    public class FireHit
    {
        public string Url { get; set; }
        public string Type { get; set; }
        public double Time { get; set; }
        public double Score { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    public class FireAnalysisParams
    {
        public int Width { get; set; } = 640;
        public int Step { get; set; } = 1;
        public int Skip { get; set; } = 0;
        public double WindowSec { get; set; } = 1.0;
        public int TopN { get; set; } = 1;
        public double MinScore { get; set; } = 0.80;
        public int FireY { get; set; } = 90;
        public int Rg { get; set; } = 40;
        public int Gb { get; set; } = 10;
        public double Spike { get; set; } = 0.28;
        public int White { get; set; } = 210;
        public int Area { get; set; } = 80;
        public double Alpha { get; set; } = 0.08;
        public bool DetectFire { get; set; } = true;
        public bool DetectFlash { get; set; } = true;
        public bool ShowHeat { get; set; } = false;
        public double LocalSpike { get; set; } = 0.55;
        public int FlashMinY { get; set; } = 140;
        public double WhiteCap { get; set; } = 0.25;
        public int Persist { get; set; } = 2;
        public double AreaMaxRatio { get; set; } = 0.10;
    }
}
