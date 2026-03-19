/// <summary>参考电压类型</summary>
public enum ZwavReferenceType
{
    /// <summary>公称参考电压</summary>
    Declared = 1,
    /// <summary>滑动参考电压（稳态估算）</summary>
    Sliding = 2
}

/// <summary>RMS 计算模式（历史枚举：当前后端固定为 1周波窗口 + 半周波更新）</summary>
public enum ZwavRmsMode
{
    /// <summary>1周波</summary>
    OneCycle = 1,
    /// <summary>半周波</summary>
    HalfCycle = 2
}

/// <summary>暂降事件类型</summary>
public enum ZwavSagEventType
{
    /// <summary>电压暂降</summary>
    Sag = 1,
    /// <summary>短时中断</summary>
    Interruption = 2
}
