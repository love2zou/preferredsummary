export const DEFAULT_ALGO_PARAMS = {
  SampleFps: 18,
  DiffThreshold: 36,
  DiffThresholdMin: 12,
  AdaptiveDiffK: 2.3,
  MinContourArea: 60,
  MeanDeltaRise: 6.0,
  MeanDeltaFall: 4.0,
  BrightStdK: 2.0,
  BrightThrMin: 165,
  BrightThrMax: 240,
  BrightRatioDelta: 0.0035,
  FlashAreaRatio: 0.2,
  GlobalBrightnessDelta: 5.0,
  MaxPulseSec: 1.2,
  SustainRejectSec: 1.5,
  ResizeMaxWidth: 960,
  BlurKernel: 5,
  RequireConsecutiveHits: 2,
  CooldownSec: 2,
  MergeGapSec: 1.0,
  MaxMotionRatioPerSec: 0.25
}

export const DEFAULT_ALGO_PARAMS_JSON = JSON.stringify(DEFAULT_ALGO_PARAMS, null, 2)

const algoKeys = Object.keys(DEFAULT_ALGO_PARAMS)

export const ALGO_FIELD_GROUPS = [
  '抽帧',
  '差分候选',
  '亮度判别',
  '闪光/辅助',
  '时序',
  '预处理',
  '抑制策略'
]

export const ALGO_FIELDS = [
  { key: 'SampleFps', label: 'SampleFps', labelZh: '抽帧帧率', desc: '用于算法分析的视频采样帧率。', group: '抽帧', min: 1, max: 60, step: 1 },
  { key: 'DiffThreshold', label: 'DiffThreshold', labelZh: '差分阈值', desc: '帧间亮度差二值化阈值，值越大越严格。', group: '差分候选', min: 1, max: 255, step: 1 },
  { key: 'DiffThresholdMin', label: 'DiffThresholdMin', labelZh: '差分阈值下限', desc: '自适应差分阈值的最低值。', group: '差分候选', min: 0, max: 255, step: 1 },
  { key: 'AdaptiveDiffK', label: 'AdaptiveDiffK', labelZh: '自适应系数 K', desc: '自适应差分强度，越大越不容易触发。', group: '差分候选', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'MinContourArea', label: 'MinContourArea', labelZh: '最小轮廓面积', desc: '过滤微小噪点轮廓，单位为像素面积。', group: '差分候选', min: 1, max: 2000, step: 1 },

  { key: 'MeanDeltaRise', label: 'MeanDeltaRise', labelZh: '亮度上升阈值', desc: '全局亮度上升达到该幅度时，更倾向识别为脉冲开始。', group: '亮度判别', min: 0, max: 50, step: 0.1, precision: 2 },
  { key: 'MeanDeltaFall', label: 'MeanDeltaFall', labelZh: '亮度回落阈值', desc: '全局亮度回落达到该幅度时，更倾向识别为脉冲结束。', group: '亮度判别', min: 0, max: 50, step: 0.1, precision: 2 },
  { key: 'BrightStdK', label: 'BrightStdK', labelZh: '动态高亮 K', desc: '高亮阈值中的标准差系数 K。', group: '亮度判别', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'BrightThrMin', label: 'BrightThrMin', labelZh: '高亮阈值最小值', desc: '动态高亮阈值的下限。', group: '亮度判别', min: 0, max: 255, step: 1 },
  { key: 'BrightThrMax', label: 'BrightThrMax', labelZh: '高亮阈值最大值', desc: '动态高亮阈值的上限。', group: '亮度判别', min: 0, max: 255, step: 1 },
  { key: 'BrightRatioDelta', label: 'BrightRatioDelta', labelZh: '高亮比例变化阈值', desc: '高亮像素比例变化超过该值时，更容易触发事件。', group: '亮度判别', min: 0, max: 0.01, step: 0.0001, precision: 4 },

  { key: 'FlashAreaRatio', label: 'FlashAreaRatio', labelZh: '闪光面积比阈值', desc: '变化面积占比达到该阈值时，更倾向判定为闪光。', group: '闪光/辅助', min: 0, max: 1, step: 0.01, precision: 3 },
  { key: 'GlobalBrightnessDelta', label: 'GlobalBrightnessDelta', labelZh: '全局亮度差阈值', desc: '全局亮度变化超过该值时，可辅助判别闪光。', group: '闪光/辅助', min: 0, max: 100, step: 0.5, precision: 2 },

  { key: 'MaxPulseSec', label: 'MaxPulseSec', labelZh: '最大脉冲时长(秒)', desc: '单次脉冲允许持续的最长时间。', group: '时序', min: 0, max: 10, step: 0.1, precision: 2 },
  { key: 'SustainRejectSec', label: 'SustainRejectSec', labelZh: '持续光源抑制(秒)', desc: '持续亮光超过该时长时，更可能被抑制为非异常。', group: '时序', min: 0, max: 30, step: 0.1, precision: 2 },

  { key: 'ResizeMaxWidth', label: 'ResizeMaxWidth', labelZh: '最大宽度', desc: '预处理缩放的宽度上限，0 表示不缩放。', group: '预处理', min: 0, max: 3840, step: 10 },
  { key: 'BlurKernel', label: 'BlurKernel', labelZh: '模糊核大小', desc: '预处理去噪使用的模糊核大小。', group: '预处理', min: 0, max: 31, step: 1 },

  { key: 'RequireConsecutiveHits', label: 'RequireConsecutiveHits', labelZh: '连续命中帧数', desc: '需要连续命中多少次才确认事件。', group: '抑制策略', min: 1, max: 10, step: 1 },
  { key: 'CooldownSec', label: 'CooldownSec', labelZh: '冷却时间(秒)', desc: '事件结束后冷却 N 秒内不再触发新事件。', group: '抑制策略', min: 0, max: 30, step: 1 },
  { key: 'MergeGapSec', label: 'MergeGapSec', labelZh: '合并间隔(秒)', desc: '相邻事件时间间隔小于该值时自动合并。', group: '抑制策略', min: 0, max: 30, step: 1 },
  { key: 'MaxMotionRatioPerSec', label: 'MaxMotionRatioPerSec', labelZh: '最大运动比例/秒', desc: '大范围运动超过该比例时，更倾向抑制误报。', group: '抑制策略', min: 0, max: 1, step: 0.01, precision: 3 }
]

export function normalizeAlgoParams(partial) {
  const merged = { ...DEFAULT_ALGO_PARAMS }
  const source = partial || {}

  algoKeys.forEach((key) => {
    const raw = source[key]
    const value = raw === undefined || raw === null ? DEFAULT_ALGO_PARAMS[key] : Number(raw)
    merged[key] = Number.isFinite(value) ? value : DEFAULT_ALGO_PARAMS[key]
  })

  if (merged.BrightThrMax < merged.BrightThrMin) merged.BrightThrMax = merged.BrightThrMin
  if (merged.BlurKernel > 1 && merged.BlurKernel % 2 === 0) merged.BlurKernel += 1
  if (merged.RequireConsecutiveHits <= 0) merged.RequireConsecutiveHits = 1
  if (merged.SampleFps <= 0) merged.SampleFps = DEFAULT_ALGO_PARAMS.SampleFps

  return merged
}

export function parseAlgoParamsJson(text) {
  if (!text || !String(text).trim()) return { ...DEFAULT_ALGO_PARAMS }
  try {
    return normalizeAlgoParams(JSON.parse(String(text)))
  } catch {
    return { ...DEFAULT_ALGO_PARAMS }
  }
}

export function toAlgoParamsJson(params) {
  return JSON.stringify(normalizeAlgoParams(params), null, 2)
}
