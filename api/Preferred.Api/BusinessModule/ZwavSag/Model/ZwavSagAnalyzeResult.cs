using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zwav.Application.Sag
{
    public class ZwavSagAnalyzeResult
    {
        public List<ZwavSagEventResult> Events { get; set; } = new List<ZwavSagEventResult>();
        public List<ZwavSagRmsPointResult> RmsPoints { get; set; } = new List<ZwavSagRmsPointResult>();
    }
}