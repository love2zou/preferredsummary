export type AlgoParams = {
  SampleFps: number
  DiffThreshold: number
  DiffThresholdMin: number
  AdaptiveDiffK: number
  MinContourArea: number
  MeanDeltaRise: number
  MeanDeltaFall: number
  BrightStdK: number
  BrightThrMin: number
  BrightThrMax: number
  BrightRatioDelta: number
  FlashAreaRatio: number
  GlobalBrightnessDelta: number
  MaxPulseSec: number
  SustainRejectSec: number
  ResizeMaxWidth: number
  BlurKernel: number
  RequireConsecutiveHits: number
  CooldownSec: number
  MergeGapSec: number
  MaxMotionRatioPerSec: number
}
export const DEFAULT_ALGO_PARAMS: AlgoParams = {
  // ===== 抽帧 =====
  SampleFps: 18,                 // ↓ 从 12 降到 10，CPU/IO 更稳，1 秒内仍 ≥10 帧

  // ===== diff / 轮廓 =====
  DiffThreshold: 36,              // ↓ 降低一点，避免夜间或偏暗视频漏检
  DiffThresholdMin: 12,           // ↓ 保底阈值，防止 AdaptiveDiff 过高
  AdaptiveDiffK: 2.3,             // ↓ 从 2.6 回落，减少“抬阈值导致全漏”
  MinContourArea: 60,             // ↓ 小型火花更容易进候选

  // ===== 灰度均值突变（核心主信号）=====
  MeanDeltaRise: 6.0,             // ↓ 非饱和闪光更友好
  MeanDeltaFall: 4.0,             // ↑ 要求更明显回落，抑制慢亮

  // ===== 亮度分布 =====
  BrightStdK: 2.0,                // ↓ 适配普通工业视频噪声
  BrightThrMin: 165,              // ↓ 容忍偏暗、压缩视频
  BrightThrMax: 240,              // ↓ 避免高曝光整屏误判
  BrightRatioDelta: 0.0035,       // ↓ 更容易捕捉“瞬时亮点”

  // ===== 面积 / 全局 =====
  FlashAreaRatio: 0.20,           // ↓ 小范围闪光（火花）更友好
  GlobalBrightnessDelta: 5.0,     // ↓ 对整体亮度变化不那么敏感

  // ===== 脉冲时序 =====
  MaxPulseSec: 1.2,               // ↑ 给一点余量，兼容 0.8~1.1s 闪光
  SustainRejectSec: 1.5,          // ↑ 排除持续亮灯/焊接

  // ===== 图像预处理 =====
  ResizeMaxWidth: 960,            // 保持不变（性能/细节平衡）
  BlurKernel: 5,                  // 保持

  // ===== 稳定性控制 =====
  RequireConsecutiveHits: 2,      // 保持：2 帧确认
  CooldownSec: 2,                 // 保持：防止连发
  MergeGapSec: 1.0,               // 保持：短间隔合并
  MaxMotionRatioPerSec: 0.25      // ↓ 对大范围运动更严格
}

export const DEFAULT_ALGO_PARAMS_JSON = JSON.stringify(DEFAULT_ALGO_PARAMS, null, 2)

const algoKeys: Array<keyof AlgoParams> = [
  'SampleFps',
  'DiffThreshold',
  'DiffThresholdMin',
  'AdaptiveDiffK',
  'MinContourArea',
  'MeanDeltaRise',
  'MeanDeltaFall',
  'BrightStdK',
  'BrightThrMin',
  'BrightThrMax',
  'BrightRatioDelta',
  'FlashAreaRatio',
  'GlobalBrightnessDelta',
  'MaxPulseSec',
  'SustainRejectSec',
  'ResizeMaxWidth',
  'BlurKernel',
  'RequireConsecutiveHits',
  'CooldownSec',
  'MergeGapSec',
  'MaxMotionRatioPerSec'
]

export type AlgoFieldGroup =
  | '抽帧'
  | '差分候选'
  | '亮度判别'
  | '闪光/辅助'
  | '时序'
  | '预处理'
  | '抑制策略'

export type AlgoFieldMeta = {
  key: keyof AlgoParams
  label: string
  labelZh: string
  desc: string
  group: AlgoFieldGroup
  min: number
  max: number
  step: number
  precision?: number
}

export const ALGO_FIELDS: AlgoFieldMeta[] = [
  // ★ min 改为 1，避免 SampleFps=0/1 这种漏检坑
  { key: 'SampleFps', label: 'SampleFps', labelZh: '抽帧帧率', desc: '用于分析的采样帧率（只认 SampleFps）。', group: '抽帧', min: 1, max: 60, step: 1 },

  { key: 'DiffThreshold', label: 'DiffThreshold', labelZh: '差分阈值', desc: '帧间灰度差二值化阈值（越大越严格）。', group: '差分候选', min: 1, max: 255, step: 1 },
  { key: 'DiffThresholdMin', label: 'DiffThresholdMin', labelZh: '差分阈值下限', desc: '自适应差分阈值的最小值，防止过低导致噪声。', group: '差分候选', min: 0, max: 255, step: 1 },
  { key: 'AdaptiveDiffK', label: 'AdaptiveDiffK', labelZh: '自适应系数K', desc: '自适应阈值强度，越大越不易触发。', group: '差分候选', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'MinContourArea', label: 'MinContourArea', labelZh: '最小轮廓面积', desc: '过滤小噪点轮廓，单位：像素面积。', group: '差分候选', min: 1, max: 2000, step: 1 },

  { key: 'MeanDeltaRise', label: 'MeanDeltaRise', labelZh: '亮度上升阈值', desc: '全局灰度均值上升到该幅度时判为脉冲开始。', group: '亮度判别', min: 0, max: 50, step: 0.1, precision: 2 },
  { key: 'MeanDeltaFall', label: 'MeanDeltaFall', labelZh: '亮度回落阈值', desc: '全局灰度均值回落到该幅度附近时判为脉冲结束。', group: '亮度判别', min: 0, max: 50, step: 0.1, precision: 2 },

  { key: 'BrightStdK', label: 'BrightStdK', labelZh: '动态高亮K', desc: '高亮阈值=mean+K*std，K 越大高亮越苛刻。', group: '亮度判别', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'BrightThrMin', label: 'BrightThrMin', labelZh: '高亮阈值最小', desc: '动态阈值的下限，防止阈值过低。', group: '亮度判别', min: 0, max: 255, step: 1 },
  { key: 'BrightThrMax', label: 'BrightThrMax', labelZh: '高亮阈值最大', desc: '动态阈值的上限，防止阈值过高。', group: '亮度判别', min: 0, max: 255, step: 1 },

  // ★ 你当前 max=0.01 够用；ProfileB=0.0045、ProfileA=0.0030 都在范围内
  { key: 'BrightRatioDelta', label: 'BrightRatioDelta', labelZh: '高亮比例变化阈值', desc: '高亮像素比例变化超过该值时更倾向触发。', group: '亮度判别', min: 0, max: 0.01, step: 0.0001, precision: 4 },

  { key: 'FlashAreaRatio', label: 'FlashAreaRatio', labelZh: '闪光面积比阈值', desc: '大面积变化时更倾向判为闪光（0~1）。', group: '闪光/辅助', min: 0, max: 1, step: 0.01, precision: 3 },
  { key: 'GlobalBrightnessDelta', label: 'GlobalBrightnessDelta', labelZh: '全局亮度差阈值', desc: '全局亮度变化超过该值可触发候选/辅助闪光判别。', group: '闪光/辅助', min: 0, max: 100, step: 0.5, precision: 2 },

  { key: 'MaxPulseSec', label: 'MaxPulseSec', labelZh: '最大脉冲时长(秒)', desc: '单次脉冲允许的最大持续时间，超出可能被抑制/拆分。', group: '时序', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'SustainRejectSec', label: 'SustainRejectSec', labelZh: '持续光源抑制(秒)', desc: '持续亮光超过该时长认为非异常脉冲而抑制。', group: '时序', min: 0, max: 30, step: 0.1, precision: 2 },

  { key: 'ResizeMaxWidth', label: 'ResizeMaxWidth', labelZh: '最大宽度', desc: '预处理缩放上限（0 表示不缩放）。', group: '预处理', min: 0, max: 3840, step: 10 },
  { key: 'BlurKernel', label: 'BlurKernel', labelZh: '模糊核大小', desc: '去噪模糊核尺寸（建议奇数；偶数会自动+1）。', group: '预处理', min: 0, max: 31, step: 1 },

  { key: 'RequireConsecutiveHits', label: 'RequireConsecutiveHits', labelZh: '连续命中帧数', desc: '要求连续命中多少次才确认事件，越大越严格。', group: '抑制策略', min: 1, max: 10, step: 1 },
  { key: 'CooldownSec', label: 'CooldownSec', labelZh: '冷却时间(秒)', desc: '事件结束后 N 秒内不再触发新事件，防止重复。', group: '抑制策略', min: 0, max: 30, step: 1 },
  { key: 'MergeGapSec', label: 'MergeGapSec', labelZh: '合并间隔(秒)', desc: '相邻事件间隔小于该值则合并为同一事件。', group: '抑制策略', min: 0, max: 30, step: 1 },
  { key: 'MaxMotionRatioPerSec', label: 'MaxMotionRatioPerSec', labelZh: '最大运动比例/秒', desc: '画面大范围运动超过该比例时更倾向判为误触发并抑制。', group: '抑制策略', min: 0, max: 1, step: 0.01, precision: 3 }
]

export const ALGO_FIELD_GROUPS: AlgoFieldGroup[] = ['抽帧', '差分候选', '亮度判别', '闪光/辅助', '时序', '预处理', '抑制策略']

export const normalizeAlgoParams = (p: Partial<AlgoParams>): AlgoParams => {
  const merged: any = { ...DEFAULT_ALGO_PARAMS }
  const src: any = p || {}
  algoKeys.forEach((k) => {
    const raw = src[k]
    const v = raw === undefined || raw === null ? (DEFAULT_ALGO_PARAMS as any)[k] : Number(raw)
    merged[k] = Number.isFinite(v) ? v : (DEFAULT_ALGO_PARAMS as any)[k]
  })

  if (merged.BrightThrMax < merged.BrightThrMin) merged.BrightThrMax = merged.BrightThrMin
  if (merged.BlurKernel > 1 && merged.BlurKernel % 2 === 0) merged.BlurKernel += 1
  if (merged.RequireConsecutiveHits <= 0) merged.RequireConsecutiveHits = 1
  if (merged.SampleFps <= 0) merged.SampleFps = DEFAULT_ALGO_PARAMS.SampleFps

  return merged as AlgoParams
}

export const parseAlgoParamsJson = (json: string | null | undefined): AlgoParams => {
  if (!json || !String(json).trim()) return { ...DEFAULT_ALGO_PARAMS }
  try {
    const obj = JSON.parse(String(json))
    return normalizeAlgoParams(obj || {})
  } catch {
    return { ...DEFAULT_ALGO_PARAMS }
  }
}

export const toAlgoParamsJson = (p: AlgoParams) => JSON.stringify(normalizeAlgoParams(p), null, 2)
