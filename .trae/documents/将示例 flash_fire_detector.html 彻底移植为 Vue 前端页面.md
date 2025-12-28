## 目标

* 以 Vue 3 `<script setup>` 纯前端技术将 `/src/example/flash_fire_detector.html` 的功能、布局与样式一模一样移植到 `src/views/FireAnalysis.vue`

* 不调用后端；所有检测逻辑、预览、命中缩略图、导入导出、帮助与 Lightbox 全在前端实现

## 页面结构与样式

* 模板：按示例搭建 `header`、左侧 `panel`（操作与参数）、右侧 `stage`（检测预览、命中缩略图、日志）网格布局

* 深色主题 CSS 变量与类名完全匹配示例：`.wrap/.grid/.panel/.row/.btn/.file/.slider/.switch/.kpi/.stage/.canvas-wrap/.overlay/.thumbs/.thumb/.meta/.chip/.tools/.log/.sep/.footer/.small/.ok`

* 复制 Lightbox 与 Help Modal 的样式与结构（`.lightbox/.lb-inner/.lb-toolbar/.lb-btn/.lb-img-wrap/.lb-img/.lb-caption`、`.modal/.modal-card`）

## 状态与参数（Vue 响应式）

* `params` 与 `paramsDefault`：与示例一致的所有参数键（width/step/skip/windowSec/topN/minScore/fireY/rg/gb/spike/white/area/alpha/detectFire/detectFlash/showHeat/localSpike/flashMinY/whiteCap/persist/areaMaxRatio）

* 绑定滑条与开关：`v-model` 双向绑定，`@input/@change` 时更新 `params` 并持久化到 `localStorage('ff_params_v7_localflash')`

* `kpi`：fps/candPct/maxArea/elapsed 字段与示例一致

## 文件选择与画布

* `video` 隐藏元素用于播放本地文件；`file input` 设置 `video.src = URL.createObjectURL(file)` 并在 `loadedmetadata` 时 `resizeCanvas()`

* `canvas` 与 `overlay`：`canvasEl/overlayEl` ref；`ctx/octx` 获取 2D 上下文；`resizeCanvas()` 按 `params.width` 与视频宽高比计算 H

## 检测算法（前端实现，完全按示例）

* 新增局部 EMA 背景与网格：`emaY/bgYGrid/gridW/gridH`，`ensureGridBuffers(W,H,step)`，`updateEMA`、`YfromRGB`

* 构建掩码：`buildFlashLocalMask(img,W,H,step)`（局部 spike+白画面统计）与 `buildFireMask(img,W,H,step)`（暖色判定）

* 网格连通域与 IoU：`ccOnGrid(mask,gw,gh)` 与 `iou(a,b)`；将网格 bbox 转像素 bbox：`gridBBoxToPixelBox`

* 主循环：`start()/stop()/loop(ts)`；支持 `requestVideoFrameCallback` 优先；跳帧 `skip`；更新 KPI、状态线、进度条；可视化 `drawVizCombined(bbox,maskGrid)`

## 时间窗口 Top‑N 命中与打分

* 打分函数 `scoreHit(r)` 与窗口缓冲 `buffer/currentWindowIdx`

* `considerHit(r)`：进入缓冲、超出保留数时丢弃尾部、生成缩略图 `renderThumb(type,bbox)`

* `flushWindow()`：窗口切换或停止时输出前 N 项到画廊

## 命中缩略图与 Lightbox

* 画廊 `gallery` 数组，prepend 新命中，并限制最大项如示例（60），溢出释放 URL 对象

* Lightbox 打开/关闭/上一张/下一张/下载当前、键盘事件与遮罩点击，caption 显示与示例一致

## 日志与 KPI

* `log(text, level)` API：记录到日志面板，滚动到末尾；开始/停止检测时记录耗时与参数快照

* KPI 文本更新：fps/candPct/maxArea/elapsed（与示例同名 id + Vue 绑定）

## 导入/导出参数与命中导出

* 参数导出与导入：与示例一致，添加按钮与文件选择器（JSON 读写）

* 命中导出：实现示例中的 ZIP（STORE，无压缩）与 CSV 清单（保持 EOCD/中央目录等结构），文件名命名与清单格式一致

## 交互行为与帮助

* 顶部帮助按钮打开/关闭 Modal，点击遮罩关闭；键盘 Escape 关闭

* 控制区按钮：`▶️ 开始检测`、`⏹ 停止`、`X 清空结果`；另有默认/导出/导入参数按钮，与示例完全一致

## 移除后端依赖

* 移除 `AutoLoginService`、`fireAnalysisService` 与任何上传/分析 API 调用

* 保留完全本地处理；`URL.createObjectURL` 与 `canvas.toBlob` 用于缩略图数据源

## 验证

* 在浏览器中选择 mp4，本地播放与分析循环运行；观察：

  * 进度条百分比变化，状态行 spike 与背景显示

  * 命中缩略图按照时间窗口 Top‑N 生成，Lightbox/下载正常

  * ZIP/CSV 导出可打开且清单正确

  * 参数导入导出与 localStorage 持久化生效

## 交付

* 提交重写后的 `src/views/FireAnalysis.vue` 单文件组件，保证功能与示例完全一致、纯前端实现

