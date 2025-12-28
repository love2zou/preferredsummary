<template>
  <div class="wrap">
    <header>
      <h1>本地视频闪光 / 火光检测</h1>
      <span class="badge">局部闪光优化 · Top命中筛选 · 分析耗时 · 预览/下载 · ZIP/CSV导出</span>
      <button class="help-btn" @click="helpOpen=true">提示</button>
    </header>

    <div class="grid">
      <div class="panel">
        <h2>操作与参数</h2>
        <div class="row">
          <label class="file btn primary">📤 选择视频 (mp4)
            <input type="file" accept="video/mp4" @change="onFileChange">
          </label>
          <button class="btn" @click="start">▶️ 开始检测</button>
          <button class="btn ghost" @click="stop(false)">⏹ 停止</button>
          <button class="btn ghost" @click="clearThumbs">X 清空结果</button>
        </div>

        <div class="row">
          <button class="btn" @click="restoreDefaults">恢复默认</button>
          <button class="btn" @click="exportParams">导出参数</button>
          <label class="file btn">导入参数
            <input type="file" accept="application/json" @change="importParams">
          </label>
        </div>

        <div class="sep"></div>

        <div class="switch">
          <label title="是否启用基于暖色和亮度的火光检测"><input type="checkbox" v-model="params.detectFire"> 检测火光</label>
          <label title="是否启用基于亮度突增与白光特征的闪光检测（局部优化）"><input type="checkbox" v-model="params.detectFlash"> 检测闪光</label>
          <label title="调试用可视化：显示候选区域的热力/二值图"><input type="checkbox" v-model="params.showHeat"> 显示候选热力/二值图</label>
        </div>

        <div class="slider">
          <label>处理画布宽度（降采样）<span><span>{{ params.width }}</span> px</span></label>
          <input type="range" min="320" max="960" step="40" v-model.number="params.width" @input="onParamsInput">
          <div class="hint">控制算法输入分辨率，越小越快。仅影响分析，不改动原视频。</div>
        </div>

        <div class="slider">
          <label>像素采样步长<span><span>{{ params.step }}</span></span></label>
          <input type="range" min="1" max="4" step="1" v-model.number="params.step" @input="onParamsInput">
          <div class="hint">步长=2 表示每隔 1 像素取样一次，速度更快但精度略降。</div>
        </div>

        <div class="slider">
          <label>帧跳过（每 N 帧分析 1 帧）<span><span>{{ params.skip }}</span></span></label>
          <input type="range" min="0" max="4" step="1" v-model.number="params.skip" @input="onParamsInput">
          <div class="hint">牺牲时序分辨率换取性能，适合长视频快速粗检。</div>
        </div>

        <div class="slider">
          <label>时间窗口长度（秒）<span><span>{{ params.windowSec.toFixed(1) }}</span>s</span></label>
          <input type="range" min="0.3" max="3.0" step="0.1" v-model.number="params.windowSec" @input="onParamsInput">
          <div class="hint">将时间划分为窗口，每窗口只保留 Top-N 张命中图，避免重复。</div>
        </div>

        <div class="slider">
          <label>Top-N（每窗口最多保留几张）</label>
          <select v-model.number="params.topN" @change="persistParams">
            <option :value="1">1</option>
            <option :value="2">2</option>
            <option :value="3">3</option>
          </select>
          <div class="hint">控制每个时间窗口中最多保留的命中缩略图数量。</div>
        </div>

        <div class="slider">
          <label>最小命中分数阈值<span><span>{{ params.minScore.toFixed(2) }}</span></span></label>
          <input type="range" min="0" max="2" step="0.05" v-model.number="params.minScore" @input="onParamsInput">
          <div class="hint">基于 “spike（亮度突增）+ 连通域面积 + 类型加权” 的综合得分，低于该值的命中将被过滤。</div>
        </div>

        <div class="sep"></div>

        <div class="slider">
          <label>火光：最低亮度 Y<span><span>{{ params.fireY }}</span></span></label>
          <input type="range" min="40" max="160" step="1" v-model.number="params.fireY" @input="onParamsInput">
          <div class="hint">提高可减少暗部误报；常见范围 80~120。</div>
        </div>

        <div class="slider">
          <label>火光：红-绿差阈值 (R-G)<span><span>{{ params.rg }}</span></span></label>
          <input type="range" min="10" max="100" step="1" v-model.number="params.rg" @input="onParamsInput">
          <div class="hint">增大可抑制普通暖光或红色物体带来的误报。</div>
        </div>

        <div class="slider">
          <label>火光：绿-蓝差阈值 (G-B)<span><span>{{ params.gb }}</span></span></label>
          <input type="range" min="0" max="60" step="1" v-model.number="params.gb" @input="onParamsInput">
          <div class="hint">区分偏黄橙（火焰）与纯红/粉色光源。</div>
        </div>

        <div class="sep"></div>

        <div class="slider">
          <label>闪光：局部 spike 阈值<span><span>{{ params.localSpike.toFixed(2) }}</span></span></label>
          <input type="range" min="0.20" max="1.20" step="0.05" v-model.number="params.localSpike" @input="persistParams">
          <div class="hint">更关注局部亮度相对背景的突增。</div>
        </div>

        <div class="slider">
          <label>闪光：局部最低亮度 Y<span><span>{{ params.flashMinY }}</span></span></label>
          <input type="range" min="60" max="220" step="5" v-model.number="params.flashMinY" @input="persistParams">
          <div class="hint">过滤暗部噪声的局部阈值；越高越保守。</div>
        </div>

        <div class="slider">
          <label>闪光：白画面上限（全局）<span><span>{{ params.whiteCap.toFixed(2) }}</span></span></label>
          <input type="range" min="0.05" max="0.80" step="0.01" v-model.number="params.whiteCap" @input="persistParams">
          <div class="hint">全帧“近白像素”比例上限，超过则压制闪光。</div>
        </div>

        <div class="slider">
          <label>闪光：时间去抖帧数<span><span>{{ params.persist }}</span></span></label>
          <input type="range" min="1" max="5" step="1" v-model.number="params.persist" @input="persistParams">
          <div class="hint">近几帧 bbox IoU≥0.3 的计数门槛。</div>
        </div>

        <div class="slider">
          <label>闪光：面积上限占比<span><span>{{ params.areaMaxRatio.toFixed(2) }}</span></span></label>
          <input type="range" min="0.02" max="0.50" step="0.01" v-model.number="params.areaMaxRatio" @input="persistParams">
          <div class="hint">限制单个连通域面积占比。</div>
        </div>

        <div class="slider">
          <label>闪光：白光最低通道值<span><span>{{ params.white }}</span></span></label>
          <input type="range" min="160" max="255" step="1" v-model.number="params.white" @input="onParamsInput">
          <div class="hint">用于统计“近白像素”的阈值。</div>
        </div>

        <div class="slider">
          <label>连通域最小像素数（去噪）<span><span>{{ params.area }}</span></span></label>
          <input type="range" min="20" max="600" step="10" v-model.number="params.area" @input="onParamsInput">
          <div class="hint">过滤小面积噪点，强调空间一致性。</div>
        </div>

        <div class="slider">
          <label>EMA 平滑系数 α<span><span>{{ params.alpha.toFixed(2) }}</span></span></label>
          <input type="range" min="0.02" max="0.4" step="0.01" v-model.number="params.alpha" @input="onParamsInput">
          <div class="hint">背景亮度 EMA。</div>
        </div>

        <div class="kpi">
          <div class="card"><div class="v">{{ kpi.fps || '—' }}</div><div class="t">处理 FPS</div></div>
          <div class="card"><div class="v">{{ kpi.candPct || '—' }}</div><div class="t">候选占比</div></div>
          <div class="card"><div class="v">{{ kpi.maxArea || '—' }}</div><div class="t">最大连通域</div></div>
          <div class="card"><div class="v">{{ kpi.elapsed || '—' }}</div><div class="t">分析耗时(秒)</div></div>
        </div>
      </div>

      <div class="stage">
        <div class="panel">
          <h2>检测预览</h2>
          <div class="canvas-wrap">
            <canvas ref="canvasEl"></canvas>
            <canvas ref="overlayEl" class="overlay"></canvas>
          </div>
          <div class="small" v-html="statusLine"></div>
          <div style="margin-top:8px;">
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
              <span class="small">分析进度</span>
              <span class="small">{{ progressText }}</span>
            </div>
            <div style="height:10px;background:#0b1324;border:1px solid #182133;border-radius:999px;overflow:hidden;">
              <div :style="{height:'100%',width:progressPct,background:'linear-gradient(90deg,#22c55e,#60a5fa)'}"></div>
            </div>
          </div>
        </div>

        <div class="panel">
          <div class="tools">
            <h2 style="margin:0">命中缩略图（时间窗口 Top-N）</h2>
            <button class="btn" @click="exportAllAsZip">导出全部命中为 ZIP</button>
            <button class="btn" @click="exportCsvOnly">导出命中清单 CSV</button>
          </div>
          <div id="thumbs">
            <div v-for="(it,idx) in gallery" :key="it.url" class="thumb" @click="openLightbox(idx)">
              <img :src="it.url" :alt="`${it.type} @ ${it.time.toFixed(2)}s`"/>
              <div class="meta">
                <div>{{ it.type==='flash'?'⚡ 闪光':'🔥 火光' }}  @ {{ it.time.toFixed(2) }}s  sc={{ it.score.toFixed(2) }}</div>
                <span class="chip">{{ it.w }}×{{ it.h }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="panel">
          <h2>日志</h2>
          <div class="log">{{ logText }}</div>
          <div class="footer">
            <div class="small">参数与统计将记录于此，可复制保存。</div>
            <button class="btn ghost" @click="clearLog">清空日志</button>
          </div>
        </div>
      </div>
    </div>

    <div :class="['modal', helpOpen?'open':'']" role="dialog" aria-modal="true" aria-label="设计思路与功能说明" @click="onHelpBackdrop">
      <div class="modal-card">
        <div style="display:flex;align-items:center;gap:8px">
          <h3>设计思路 & 主要功能</h3>
          <button class="btn ghost modal-close" @click="helpOpen=false">关闭</button>
        </div>
        <p>本工具用于离线分析本地视频中的 <b>闪光/火光</b> 事件，完全在浏览器内侧执行，不上传文件。</p>
        <ul>
          <li><b>检测策略</b>：
            <ul>
              <li><b>闪光（优化）</b>：局部亮度 EMA 背景 + 局部 spike + 连通域面积 + 时间去抖 + 全局白画面拦截。</li>
              <li><b>火光</b>：R>G>B + (R-G)/(G-B) 阈值 + 最低亮度 + 连通域。</li>
              <li><b>Top-N</b>：按时间窗口保留得分最高的命中。</li>
            </ul>
          </li>
          <li><b>性能可调</b>：处理分辨率、像素步长、帧跳过均可调；KPI 显示处理 FPS、候选占比、最大连通域面积与分析耗时。</li>
          <li><b>可视化</b>：热力/二值图叠加，红框标注最大连通域；命中缩略图点击放大，支持上一张/下一张/下载/关闭。</li>
          <li><b>导出</b>：导出参数、导入参数、导出全部命中 ZIP（含 manifest.csv）与仅导出 CSV 清单。</li>
        </ul>
      </div>
    </div>

    <div :class="['lightbox', lbOpen?'open':'']" role="dialog" aria-modal="true" aria-label="命中图片预览" @click="onLightboxBackdrop">
      <div class="lb-inner">
        <div class="lb-toolbar">
          <button class="lb-btn" @click.stop="prevLightbox" title="上一张（←）">⟨ 上一张</button>
          <button class="lb-btn" @click.stop="nextLightbox" title="下一张（→）">下一张 ⟩</button>
          <button class="lb-btn" @click.stop="downloadCurrent" title="下载本张（Enter）">下载本张 ⤓</button>
          <button class="lb-btn" @click.stop="closeLightbox" title="关闭（Esc）">关闭 ✕</button>
        </div>
        <div class="lb-img-wrap">
          <img v-if="lbOpen && gallery[lbIndex]" class="lb-img" :src="gallery[lbIndex].url" alt="命中图预览"/>
        </div>
        <div class="lb-caption">
          <template v-if="lbOpen && gallery[lbIndex]">
            {{ gallery[lbIndex].type==='flash'?'⚡ 闪光':'🔥 火光' }}  @ {{ gallery[lbIndex].time.toFixed(2) }}s  （{{ lbIndex+1 }}/{{ gallery.length }}）  sc={{ gallery[lbIndex].score.toFixed(2) }}  {{ gallery[lbIndex].w }}×{{ gallery[lbIndex].h }}
          </template>
          <template v-else>—</template>
        </div>
      </div>
    </div>
    <video ref="videoEl" preload="metadata" style="display:none"></video>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';

type Hit = { url:string; type:'flash'|'fire'; time:number; score:number; w:number; h:number }

const videoEl = ref<HTMLVideoElement|null>(null)
const canvasEl = ref<HTMLCanvasElement|null>(null)
const overlayEl = ref<HTMLCanvasElement|null>(null)
const helpOpen = ref(false)
const gallery = ref<Hit[]>([])
const logText = ref('')
const statusLine = ref('未加载视频')
const progressText = ref('0%')
const progressPct = ref('0%')
const lbOpen = ref(false)
const lbIndex = ref(-1)
const statusY = ref('0.0')
const statusBg = ref('0.0')
const statusSpike = ref('0.000')
const spikeOk = ref(false)
const kpi = ref<{ fps:string; candPct:string; maxArea:string; elapsed:string }>({ fps:'—', candPct:'—', maxArea:'—', elapsed:'—' })

const paramsDefault = {
  width:640, step:1, skip:0,
  windowSec:1.0, topN:1, minScore:0.80,
  fireY:90, rg:40, gb:10,
  spike:0.28, white:210, area:80, alpha:0.08,
  detectFire:true, detectFlash:true, showHeat:false,
  localSpike:0.55, flashMinY:140, whiteCap:0.25, persist:2, areaMaxRatio:0.10
}
const params = ref(loadParams() || { ...paramsDefault })

function persistParams(){ try{ localStorage.setItem('ff_params_v7_localflash', JSON.stringify(params.value)) }catch{} }
function loadParams(){ try{ const s = localStorage.getItem('ff_params_v7_localflash'); return s?JSON.parse(s):null }catch{ return null } }

function onParamsInput(){ persistParams(); resizeCanvas() }
function exportParams(){ const blob = new Blob([JSON.stringify(params.value,null,2)], {type:'application/json'}); const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href=url; a.download='flash_fire_params.json'; a.click(); setTimeout(()=>URL.revokeObjectURL(url),3000) }
function importParams(e: Event){ const f = (e.target as HTMLInputElement).files?.[0]; if(!f) return; const reader = new FileReader(); reader.onload = ()=>{ try{ const p = JSON.parse(String(reader.result)); params.value = Object.assign({}, params.value, p); log('已导入参数') }catch{ log('导入失败','danger') } }; reader.readAsText(f) }
function restoreDefaults(){ params.value = { ...paramsDefault }; applyParamsToUI(); log('已恢复默认参数'); persistParams() }

function onHelpBackdrop(e: MouseEvent){ if (e.target === e.currentTarget) helpOpen.value=false }
function openLightbox(index:number){ if (gallery.value.length===0) return; lbIndex.value = Math.max(0, Math.min(index, gallery.value.length-1)); lbOpen.value = true }
function closeLightbox(){ lbOpen.value=false }
function prevLightbox(){ if (gallery.value.length===0) return; lbIndex.value = (lbIndex.value - 1 + gallery.value.length) % gallery.value.length }
function nextLightbox(){ if (gallery.value.length===0) return; lbIndex.value = (lbIndex.value + 1) % gallery.value.length }
function onLightboxBackdrop(e: MouseEvent){ if (e.target === e.currentTarget) closeLightbox() }
function downloadCurrent(){ const it = gallery.value[lbIndex.value]; if (!it) return; const a = document.createElement('a'); const tSafe = it.time.toFixed(2).replace('.','_'); a.href = it.url; a.download = `hit_${String(gallery.value.length-lbIndex.value).padStart(4,'0')}_${it.type}_${tSafe}s_sc${it.score.toFixed(2)}_${it.w}x${it.h}.jpg`; a.click() }

function onFileChange(e: Event){ const f = (e.target as HTMLInputElement).files?.[0]; if(!f) return; if (videoEl.value?.src) URL.revokeObjectURL(videoEl.value.src); if (videoEl.value){ videoEl.value.src = URL.createObjectURL(f); videoEl.value.load(); videoEl.value.onloadedmetadata = ()=>{ resizeCanvas(); statusLine.value = `已加载：${f.name}，时长 ${(videoEl.value!.duration||0).toFixed(2)}s，尺寸 ${videoEl.value!.videoWidth}×${videoEl.value!.videoHeight}`; log('—— 参数快照 ——\n'+JSON.stringify(params.value,null,2)) }; videoEl.value.onended = ()=> stop(true) } }

function resizeCanvas(){ const W = params.value.width|0; if(!videoEl.value?.videoWidth) return; const ratio = (videoEl.value.videoHeight||0) / (videoEl.value.videoWidth||1); const H = Math.round(W * ratio); if (canvasEl.value && overlayEl.value){ canvasEl.value.width=W; canvasEl.value.height=H; overlayEl.value.width=W; overlayEl.value.height=H } }

function log(s:string, level?:'danger'|'warn'){ const ts = new Date().toLocaleTimeString(); const line = level==='danger' ? '❌ ' : level==='warn' ? '⚠️ ' : '📝 '; logText.value += `[${ts}] ${line}${s}\n`; const el = document.querySelector('.log') as HTMLElement; if (el) el.scrollTop = el.scrollHeight }
function clearLog(){ logText.value = '' }

let running = false
let frameHandle:number|null = null
let fpsEMA = 0
let analysisStartMs:number|null = null
let analysisEndMs:number|null = null
let lastFpsTs = performance.now()
let frames = 0
let skipCounter = 0

let emaY:number|null = null
let bgYGrid:Float32Array|null = null
let gridW = 0, gridH = 0
let hitQueue: Array<{bbox:{x:number;y:number;w:number;h:number}, t:number}> = []
let currentWindowIdx = -1
let buffer: Array<{score:number; url:string; type:'flash'|'fire'; time:number}> = []

function updateEMA(v:number, a:number){ if(emaY==null) emaY=v; else emaY = emaY + a*(v-emaY); return emaY }
function ensureGridBuffers(W:number, H:number, step:number){ const gw = Math.floor(W/step), gh = Math.floor(H/step); if (gw!==gridW || gh!==gridH || !bgYGrid){ gridW=gw; gridH=gh; bgYGrid = new Float32Array(gridW*gridH); bgYGrid.fill(0); hitQueue=[] } }
function YfromRGB(r:number,g:number,b:number){ return ( (r*77 + g*150 + b*29) ) >> 8 }

function ccOnGrid(mask:Uint8Array, gw:number, gh:number){ const visited = new Uint8Array(mask.length); const comps: Array<{minx:number;miny:number;maxx:number;maxy:number;area:number}> = []; const qx = new Int16Array(mask.length); const qy = new Int16Array(mask.length); for (let y=0;y<gh;y++){ for(let x=0;x<gw;x++){ const i=y*gw+x; if(!mask[i]||visited[i]) continue; let head=0, tail=0; qx[tail]=x; qy[tail]=y; tail++; visited[i]=1; let minx=x,miny=y,maxx=x,maxy=y, area=0; while(head<tail){ const cx=qx[head], cy=qy[head]; head++; area++; if(cx<minx)minx=cx; if(cx>maxx)maxx=cx; if(cy<miny)miny=cy; if(cy>maxy)maxy=cy; if (cx>0){ const ni=cy*gw+(cx-1); if(mask[ni]&&!visited[ni]){visited[ni]=1; qx[tail]=cx-1; qy[tail]=cy; tail++} } if (cx<gw-1){ const ni=cy*gw+(cx+1); if(mask[ni]&&!visited[ni]){visited[ni]=1; qx[tail]=cx+1; qy[tail]=cy; tail++} } if (cy>0){ const ni=(cy-1)*gw+cx; if(mask[ni]&&!visited[ni]){visited[ni]=1; qx[tail]=cx; qy[tail]=cy-1; tail++} } if (cy<gh-1){ const ni=(cy+1)*gw+cx; if(mask[ni]&&!visited[ni]){visited[ni]=1; qx[tail]=cx; qy[tail]=cy+1; tail++} } } comps.push({minx,miny,maxx,maxy,area}) } } return comps }
function iou(a:{x:number;y:number;w:number;h:number}, b:{x:number;y:number;w:number;h:number}){ const x1=Math.max(a.x,b.x), y1=Math.max(a.y,b.y); const x2=Math.min(a.x+a.w,b.x+b.w), y2=Math.min(a.y+a.h,b.y+b.h); const iw=Math.max(0,x2-x1), ih=Math.max(0,y2-y1); const inter=iw*ih, union=a.w*a.h + b.w*b.h - inter; return union>0 ? inter/union : 0 }
function gridBBoxToPixelBox(c:{minx:number;miny:number;maxx:number;maxy:number;area:number}){ const s=params.value.step; return { x1:c.minx*s, y1:c.miny*s, x2:(c.maxx+1)*s, y2:(c.maxy+1)*s, area:c.area*s*s } }

function buildFlashLocalMask(imageData:ImageData, W:number, H:number, step:number){ const p=params.value; const data=imageData.data; const mask=new Uint8Array(gridW*gridH); let whiteCount=0; for(let gy=0,y=0; gy<gridH; gy++,y+=step){ for(let gx=0,x=0; gx<gridW; gx++,x+=step){ const idx=(y*W+x)*4; const r=data[idx], g=data[idx+1], b=data[idx+2]; const Y=YfromRGB(r,g,b); if (r>=p.white && g>=p.white && b>=p.white) whiteCount++; const gi=gy*gridW+gx; const bg=(bgYGrid![gi]||Y); const denom=bg>20?bg:20; const spikeLocal=(Y-bg)/denom; const on = (Y>=p.flashMinY && spikeLocal>=p.localSpike) ? 1 : 0; mask[gi]=on } } const whiteRatio = whiteCount / (gridW*gridH); return {mask, whiteRatio} }
function buildFireMask(imageData:ImageData, W:number, H:number, step:number){ const p=params.value; const data=imageData.data; const mask=new Uint8Array(gridW*gridH); for(let gy=0,y=0; gy<gridH; gy++,y+=step){ for(let gx=0,x=0; gx<gridW; gx++,x+=step){ const idx=(y*W+x)*4; const r=data[idx], g=data[idx+1], b=data[idx+2]; const Y=YfromRGB(r,g,b); let on=0; if (Y>=p.fireY && r>g && g>b && (r-g)>=p.rg && (g-b)>=p.gb){ const warm = r / Math.max(1, (r+g+b)); if (warm>0.40) on=1 } const gi=gy*gridW+gx; mask[gi]=on } } return {mask} }

function drawVizCombined(bbox:{x1:number;y1:number;x2:number;y2:number}|null, maskGrid:Uint8Array|null){ const octx = overlayEl.value!.getContext('2d')!; octx.clearRect(0,0,overlayEl.value!.width,overlayEl.value!.height); if (params.value.showHeat && maskGrid){ const W=canvasEl.value!.width, H=canvasEl.value!.height, s=params.value.step; octx.save(); octx.globalAlpha=.35; octx.fillStyle='rgba(255,120,0,0.9)'; for(let gy=0; gy<gridH; gy++){ for(let gx=0; gx<gridW; gx++){ const gi=gy*gridW+gx; if (!maskGrid[gi]) continue; octx.fillRect(gx*s, gy*s, s, s) } } octx.restore() } if (bbox){ octx.save(); octx.strokeStyle='#ef4444'; octx.lineWidth=Math.max(2, Math.round(Math.min(overlayEl.value!.width,overlayEl.value!.height)*0.005)); octx.strokeRect(bbox.x1,bbox.y1,bbox.x2-bbox.x1,bbox.y2-bbox.y1); octx.restore() } }

function largestBBox(mask:Uint8Array, W:number, H:number, minArea:number){ const n=W*H; const vis=new Uint8Array(n); let best:any=null, bestArea=0; const qx=new Int32Array(n), qy=new Int32Array(n); const idx=(x:number,y:number)=>y*W+x; for(let y=0;y<H;y++){ const base=y*W; for(let x=0;x<W;x++){ const i=base+x; if(!mask[i]||vis[i]) continue; let head=0, tail=0; qx[tail]=x; qy[tail]=y; tail++; vis[i]=1; let x1=x,x2=x,y1=y,y2=y, area=0; while(head<tail){ const cx=qx[head], cy=qy[head]; head++; area++; if(cx<x1)x1=cx; if(cx>x2)x2=cx; if(cy<y1)y1=cy; if(cy>y2)y2=cy; if(cx+1<W){ const j=idx(cx+1,cy); if(mask[j]&&!vis[j]){vis[j]=1; qx[tail]=cx+1; qy[tail]=cy; tail++} } if(cx-1>=0){ const j=idx(cx-1,cy); if(mask[j]&&!vis[j]){vis[j]=1; qx[tail]=cx-1; qy[tail]=cy; tail++} } if(cy+1<H){ const j=idx(cx,cy+1); if(mask[j]&&!vis[j]){vis[j]=1; qx[tail]=cx; qy[tail]=cy+1; tail++} } if(cy-1>=0){ const j=idx(cx,cy-1); if(mask[j]&&!vis[j]){vis[j]=1; qx[tail]=cx; qy[tail]=cy-1; tail++} } } if(area>bestArea && area>=minArea){ bestArea=area; best={x1,y1,x2,y2,area} } } } return best }

function renderThumb(type:'flash'|'fire', bbox?:{x1:number;y1:number;x2:number;y2:number}){ const ctx = canvasEl.value!.getContext('2d')!; if (bbox){ ctx.save(); ctx.strokeStyle = 'red'; ctx.lineWidth = Math.max(2, Math.round(Math.min(canvasEl.value!.width,canvasEl.value!.height)*0.005)); ctx.strokeRect(bbox.x1, bbox.y1, bbox.x2-bbox.x1, bbox.y2-bbox.y1); ctx.restore() } return new Promise<string>(r=> canvasEl.value!.toBlob(b=> r(URL.createObjectURL(b!)),'image/jpeg',0.92)) }

function appendThumb(url:string, type:'flash'|'fire', timeSec:number, score:number){ const item:Hit = { url, type, time: timeSec, score, w: canvasEl.value!.width, h: canvasEl.value!.height }; gallery.value.unshift(item); const MAX=60; if (gallery.value.length>MAX){ const last = gallery.value.pop(); if (last){ try{ URL.revokeObjectURL(last.url) }catch{} } } }
function clearThumbs(){ const imgs = document.querySelectorAll('.thumb img') as NodeListOf<HTMLImageElement>; imgs.forEach(x=>{ try{ URL.revokeObjectURL(x.src) }catch{} }); gallery.value.splice(0, gallery.value.length) }

function scoreHit(r:{area:number; spike:number; type:'flash'|'fire'}){ const areaScore = Math.log(1 + (r.area||0)); const spikeScore = Math.max(0, Math.min(1.5, r.spike)); const typeBonus = r.type==='flash' ? 0.6 : 0.3; return areaScore*0.6 + spikeScore*1.4 + typeBonus }
async function considerHit(r:{bbox:{x1:number;y1:number;x2:number;y2:number}; area:number; spike:number; type:'flash'|'fire'}){ const sc = scoreHit(r); if (sc < params.value.minScore) return; const timeSec = videoEl.value?.currentTime || 0; const widx = Math.floor(timeSec / Math.max(0.1, params.value.windowSec)); if (currentWindowIdx !== -1 && widx !== currentWindowIdx){ await flushWindow() } currentWindowIdx = widx; const url = await renderThumb(r.type, r.bbox); buffer.push({score:sc, url, type:r.type, time:timeSec}); const keep = Math.max(1, params.value.topN|0) + 2; if (buffer.length > keep){ buffer.sort((a,b)=>b.score-a.score); const dropped = buffer.splice(keep); dropped.forEach(x=>{ try{ URL.revokeObjectURL(x.url) }catch{} }) } }
async function flushWindow(){ if (buffer.length===0) return; buffer.sort((a,b)=>b.score-a.score); const n = Math.min(buffer.length, Math.max(1, params.value.topN|0)); for(let i=0;i<n;i++){ const it = buffer[i]; appendThumb(it.url, it.type, it.time, it.score) } for(let i=n;i<buffer.length;i++){ try{ URL.revokeObjectURL(buffer[i].url) }catch{} } buffer.length = 0 }

function loop(ts:number){
  if(!running) return;
  frames++;
  if (ts - lastFpsTs >= 500){
    const fps = frames * 1000 / (ts - lastFpsTs);
    fpsEMA = fpsEMA? (fpsEMA*0.6 + fps*0.4) : fps;
    kpi.value.fps = fpsEMA.toFixed(1);
    frames = 0; lastFpsTs = ts
  }
  const W=canvasEl.value!.width, H=canvasEl.value!.height;
  const ctx = canvasEl.value!.getContext('2d',{willReadFrequently:true})!;
  if (!W||!H){ return }
  const v = videoEl.value;
  if (!v || (v.readyState||0) < 2){
    const cbIdle=()=>{ if(running) requestAnimationFrame(loop) };
    if (v && 'requestVideoFrameCallback' in v){ (v as any).requestVideoFrameCallback(cbIdle) } else { requestAnimationFrame(loop) }
    return
  }
  if (skipCounter++ % (Math.max(1, (params.value.skip|0)+1)) !== 0){
    ctx.drawImage(v, 0, 0, W, H);
    const cb=()=>{ if(running) requestAnimationFrame(loop) };
    if ('requestVideoFrameCallback' in v){ (v as any).requestVideoFrameCallback(cb) } else { requestAnimationFrame(loop) }
    return
  }
  ctx.drawImage(v, 0, 0, W, H);
  const img = ctx.getImageData(0,0,W,H);
  const step = Math.max(1, params.value.step|0);
  ensureGridBuffers(W,H,step);
  let sumY=0, cnt=0;
  for(let gy=0,y=0; gy<gridH; gy++,y+=step){
    for(let gx=0,x=0; gx<gridW; gx++,x+=step){
      const idx=(y*W+x)<<2;
      const r=img.data[idx], g=img.data[idx+1], b=img.data[idx+2];
      const Y=YfromRGB(r,g,b);
      sumY+=Y; cnt++;
      const gi=gy*gridW+gx;
      const prev=bgYGrid![gi]||Y;
      bgYGrid![gi] = prev + params.value.alpha * (Y - prev)
    }
  }
  const meanY = sumY / Math.max(1,cnt);
  const bgY = updateEMA(meanY, params.value.alpha)!;
  const spikeRatioGlobal = (meanY - bgY) / Math.max(1, bgY);
  let finalHit=false, finalType:'flash'|'fire'|null=null, finalBBox:any=null, finalArea=0, unionMaskGrid:Uint8Array|null=null;
  if (params.value.detectFire){
    const {mask:fireMask} = buildFireMask(img,W,H,step);
    const comps = ccOnGrid(fireMask, gridW, gridH);
    if (comps.length){
      comps.sort((a,b)=>b.area-a.area);
      const c=comps[0];
      if (c.area >= Math.max(1, Math.round(params.value.area/(step*step)))){
        const bboxPix = gridBBoxToPixelBox(c);
        finalHit=true; finalType='fire'; finalBBox=bboxPix; finalArea=c.area; unionMaskGrid = fireMask
      } else { unionMaskGrid = fireMask }
    }
  }
 if (params.value.detectFlash){ const {mask:flashMask, whiteRatio} = buildFlashLocalMask(img,W,H,step); if (whiteRatio <= params.value.whiteCap){ const compsF = ccOnGrid(flashMask, gridW, gridH); if (compsF.length){ compsF.sort((a,b)=>b.area-a.area); const c=compsF[0]; const totalGrid = gridW*gridH; const minAreaGrid = Math.max(1, Math.round(params.value.area/(step*step))); const maxAreaGrid = Math.floor(params.value.areaMaxRatio * totalGrid); if (c.area >= minAreaGrid && c.area <= maxAreaGrid){ const bboxPix = gridBBoxToPixelBox(c); hitQueue.push({ bbox: {x:bboxPix.x1, y:bboxPix.y1, w:bboxPix.x2-bboxPix.x1, h:bboxPix.y2-bboxPix.y1}, t: performance.now() }); if (hitQueue.length>5) hitQueue.shift(); let count=0; for(let i=0;i<hitQueue.length;i++){ const hb=hitQueue[i].bbox; if (iou(hb, {x:bboxPix.x1,y:bboxPix.y1,w:bboxPix.x2-bboxPix.x1,h:bboxPix.y2-bboxPix.y1}) >= 0.3) count++ } const pass = count >= params.value.persist; if (pass){ finalHit=true; finalType='flash'; finalBBox=bboxPix; finalArea=c.area } } } } if (unionMaskGrid){ const u = new Uint8Array(unionMaskGrid.length); for(let i=0;i<u.length;i++) u[i] = unionMaskGrid[i] || flashMask[i] ? 1 : 0; unionMaskGrid=u } else { unionMaskGrid=flashMask } }
drawVizCombined(finalBBox, unionMaskGrid); statusLine.value = `Y=${meanY.toFixed(1)} 背景=${bgY.toFixed(1)} <span class="${spikeRatioGlobal >= params.value.spike ? 'ok' : ''}">spike=${spikeRatioGlobal.toFixed(3)}</span>`; if (isFinite(videoEl.value!.duration) && videoEl.value!.duration>0){ const pct=Math.max(0, Math.min(100, (videoEl.value!.currentTime / videoEl.value!.duration) * 100)); progressText.value = pct.toFixed(0)+'%'; progressPct.value = pct + '%' } if (unionMaskGrid){ const candPct = (unionMaskGrid.reduce((a,c)=>a+c,0) / Math.max(1, gridW*gridH))*100; kpi.value.candPct = candPct.toFixed(1)+'%' } if (finalArea>0){ kpi.value.maxArea = String(finalArea) } if (finalHit && finalType){ considerHit({bbox:finalBBox, area:finalArea, spike:spikeRatioGlobal, type:finalType}).then(()=>{ const sc = scoreHit({area:finalArea, spike:spikeRatioGlobal, type:finalType!}); if (sc >= params.value.minScore){ log(`${finalType==='flash'?'⚡ 闪光':'🔥 火光'} 命中；score=${sc.toFixed(2)} spike=${spikeRatioGlobal.toFixed(3)} area=${finalArea}`) } }) }
 const cb=()=>{ if(running) requestAnimationFrame(loop) }; if (videoEl.value && 'requestVideoFrameCallback' in videoEl.value){ (videoEl.value as any).requestVideoFrameCallback(cb) } else { requestAnimationFrame(loop) }
}

function start(){ if(running) return; if(!videoEl.value?.src){ alert('请先选择本地视频'); return } clearThumbs(); resizeCanvas(); running=true; emaY=null; analysisStartMs=performance.now(); analysisEndMs=null; kpi.value.elapsed='—'; if (progressText.value && progressPct.value){ progressText.value='0%'; progressPct.value='0%' } skipCounter=0; videoEl.value?.play().catch(()=>{}); currentWindowIdx=-1; buffer.length=0; fpsEMA=0; frames=0; lastFpsTs=performance.now(); frameHandle=requestAnimationFrame(loop); log('开始检测：' + ((document.querySelector('input[type=file]') as HTMLInputElement)?.files?.[0]?.name || '（媒体流）')) }
async function stop(autoEnded:boolean){ if (!running){ await flushWindow(); return } running=false; if (frameHandle){ cancelAnimationFrame(frameHandle); frameHandle=null } await flushWindow(); if (!autoEnded) videoEl.value?.pause(); analysisEndMs=performance.now(); const elapsedSec = ((analysisEndMs - (analysisStartMs||analysisEndMs)) / 1000); kpi.value.elapsed = elapsedSec.toFixed(2); if (autoEnded){ progressText.value='100%'; progressPct.value='100%' } log(`已停止检测（耗时 ${elapsedSec.toFixed(2)} s）`, 'warn') }

function crc32(buf: Uint8Array){ let c = ~0>>>0; for (let i=0;i<buf.length;i++){ c = (c ^ buf[i])>>>0; for(let k=0;k<8;k++){ c = (c & 1) ? ((c>>>1) ^ 0xEDB88320)>>>0 : (c>>>1)>>>0 } } return (~c)>>>0 }
function u32le(n:number){ return [n&255,(n>>>8)&255,(n>>>16)&255,(n>>>24)&255] }
function u16le(n:number){ return [n&255,(n>>>8)&255] }
function strToUtf8Bytes(str:string){ return new TextEncoder().encode(str) }
function dosDateTime(d=new Date()){ const year=d.getFullYear(); const dt=((year-1980)<<9)|((d.getMonth()+1)<<5)|d.getDate(); const tm=(d.getHours()<<11)|(d.getMinutes()<<5)|(Math.floor(d.getSeconds()/2)); return {date:dt, time:tm} }
async function makeZip(entries: {name:string, blob:Blob}[]){ let localParts: Uint8Array[] = [], centralParts: Uint8Array[] = []; let offset = 0; const {date,time} = dosDateTime(new Date()); for (let e of entries){ const nameBytes = strToUtf8Bytes(e.name); const buf = new Uint8Array(await e.blob.arrayBuffer()); const crc = crc32(buf), size = buf.length; const localHeader = [ ...u32le(0x04034b50), ...u16le(20), ...u16le(0x0800), ...u16le(0), ...u16le(time), ...u16le(date), ...u32le(crc), ...u32le(size), ...u32le(size), ...u16le(nameBytes.length), ...u16le(0) ]; localParts.push(new Uint8Array(localHeader)); localParts.push(nameBytes); localParts.push(buf); const centralHeader = [ ...u32le(0x02014b50), ...u16le(20), ...u16le(20), ...u16le(0x0800), ...u16le(0), ...u16le(time), ...u16le(date), ...u32le(crc), ...u32le(size), ...u32le(size), ...u16le(nameBytes.length), ...u16le(0), ...u16le(0), ...u16le(0), ...u16le(0), ...u32le(0), ...u32le(offset) ]; centralParts.push(new Uint8Array(centralHeader)); centralParts.push(nameBytes); offset += localHeader.length + nameBytes.length + size } const centralSize = centralParts.reduce((s,a)=>s+a.length,0); const centralOffset = offset; const eocd = [ ...u32le(0x06054b50), ...u16le(0), ...u16le(0), ...u16le(entries.length), ...u16le(entries.length), ...u32le(centralSize), ...u32le(centralOffset), ...u16le(0) ]; const parts = [...localParts, ...centralParts, new Uint8Array(eocd)]; const total = parts.reduce((s,a)=>s+a.length,0); const out = new Uint8Array(total); let p=0; for (let a of parts){ out.set(a,p); p+=a.length } return new Blob([out], {type:'application/zip'}) }

async function exportAllAsZip(){ if (gallery.value.length===0){ alert('没有可导出的命中图片'); return } const entries: {name:string, blob:Blob}[] = []; const lines = ['index,type,time_sec,score,resolution']; for(let i=0;i<gallery.value.length;i++){ const it = gallery.value[i]; try{ const res = await fetch(it.url); const blob = await res.blob(); const idx = String(gallery.value.length - i).padStart(4,'0'); const t = it.time.toFixed(2); const tSafe = t.replace('.','_'); const typeDir = it.type==='flash' ? 'flash' : 'fire'; const name = `${typeDir}/hit_${idx}_${it.type}_${tSafe}s_sc${it.score.toFixed(2)}_${it.w}x${it.h}.jpg`; entries.push({name, blob}); lines.push(`${idx},${it.type},${t},${it.score.toFixed(4)},${it.w}x${it.h}`) }catch{} } const csvBlob = new Blob([lines.join('\n')], {type:'text/csv'}); entries.push({name:'manifest.csv', blob:csvBlob}); const zipBlob = await makeZip(entries); const url = URL.createObjectURL(zipBlob); const a = document.createElement('a'); a.href=url; a.download='hits.zip'; a.click(); setTimeout(()=>URL.revokeObjectURL(url),4000) }
function exportCsvOnly(){ if (gallery.value.length===0){ alert('没有数据可导出'); return } const lines = ['index,type,time_sec,score,resolution']; for(let i=0;i<gallery.value.length;i++){ const it = gallery.value[i]; const idx = String(gallery.value.length - i).padStart(4,'0'); const t = it.time.toFixed(2); lines.push(`${idx},${it.type},${t},${it.score.toFixed(4)},${it.w}x${it.h}`) } const blob = new Blob([lines.join('\n')], {type:'text/csv'}); const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download='hits.csv'; a.click(); setTimeout(()=>URL.revokeObjectURL(url),4000) }

function applyParamsToUI(){ resizeCanvas() }
function onKeydown(e: KeyboardEvent){ if (helpOpen.value){ if (e.key === 'Escape'){ helpOpen.value = false } return } if (!lbOpen.value) return; if (e.key === 'Escape') closeLightbox(); else if (e.key === 'ArrowLeft') prevLightbox(); else if (e.key === 'ArrowRight') nextLightbox(); else if (e.key === 'Enter') downloadCurrent() }
onMounted(()=>{ window.addEventListener('resize', resizeCanvas); window.addEventListener('keydown', onKeydown) })
onUnmounted(()=>{ window.removeEventListener('resize', resizeCanvas); window.removeEventListener('keydown', onKeydown) })
</script>

<style scoped>
:root{ --bg:#0b1220; --panel:#0f172a; --muted:#94a3b8; --text:#e2e8f0; --accent:#60a5fa; --ok:#22c55e; --warn:#f59e0b; --err:#ef4444; --card:#0b1324; --chip:#1f2937 }
*{ box-sizing:border-box }
html,body{ height:100%; margin:0; background:linear-gradient(180deg,#0b1220,#0e1527 30%,#0b1220); color:var(--text); font-family:ui-sans-serif,system-ui,-apple-system,Segoe UI,Roboto,"Helvetica Neue","PingFang SC","Noto Sans CJK SC",sans-serif }
.wrap{ max-width:1200px; margin:0 auto; padding:16px 16px 32px }
header{ display:flex; align-items:center; gap:12px; margin-bottom:12px }
header h1{ font-size:20px; margin:0; font-weight:700; letter-spacing:.3px }
header .badge{ font-size:12px; background:var(--chip); padding:4px 8px; border-radius:999px; color:var(--muted) }
header .help-btn{ margin-left:auto; background:linear-gradient(180deg,#334155,#1f2937); border:1px solid #334155; color:#e5e7eb; padding:6px 10px; border-radius:10px; cursor:pointer }
.grid{ display:grid; grid-template-columns:340px 1fr; gap:16px }
.panel{ background:var(--panel); border:1px solid #172033; border-radius:14px; box-shadow:0 6px 20px rgba(0,0,0,.28); padding:14px }
.panel h2{ margin:0 0 10px; font-size:15px; color:#cbd5e1 }
.row{ display:flex; gap:8px; align-items:center; flex-wrap:wrap; margin-bottom:10px }
.row > *{ flex:0 0 auto }
.btn{ background:linear-gradient(180deg,#17253b,#0e1729); border:1px solid #1e2a3f; color:#dbeafe; padding:8px 12px; border-radius:10px; cursor:pointer; transition:transform .05s ease,opacity .2s }
.btn:hover{ opacity:.95 }
.btn:active{ transform:translateY(1px) }
.btn.primary{ background:linear-gradient(180deg,#2563eb,#1d4ed8); border-color:#1e40af; color:white }
.btn.ghost{ background:transparent; border:1px dashed #334155; color:#93c5fd }
.file{ display:inline-block; position:relative }
.file input{ position:absolute; inset:0; opacity:0; cursor:pointer }
.slider{ display:flex; flex-direction:column; gap:4px; margin-bottom:10px }
.slider label{ display:flex; justify-content:space-between; gap:8px; font-size:13px; color:#cbd5e1 }
.slider input[type="range"], .slider select { width:100% }
.hint{ font-size:12px; color:var(--muted); margin-top:4px; line-height:1.45 }
.switch{ display:flex; align-items:center; gap:8px; font-size:13px; color:#cbd5e1; flex-wrap:wrap }
.kpi{ display:grid; grid-template-columns:repeat(4,1fr); gap:10px; margin-top:6px }
.kpi .card{ background:var(--card); border:1px solid #182133; border-radius:12px; padding:10px }
.kpi .card .v{ font-size:18px; font-weight:700 }
.kpi .card .t{ font-size:12px; color:var(--muted) }
.stage{ display:grid; grid-template-rows:auto 1fr; gap:8px }
.canvas-wrap{ position:relative; background:#020617; border:1px solid #152036; border-radius:10px; overflow:hidden; min-height:260px }
canvas{ display:block; width:100%; height:auto }
.overlay{ position:absolute; inset:0; pointer-events:none }
#thumbs{ display:grid; grid-template-columns:repeat(auto-fill,minmax(160px,1fr)); gap:10px }
.thumb{ background:var(--card); border:1px solid #152036; border-radius:12px; overflow:hidden; cursor:pointer }
.thumb img{ display:block; width:100%; height:auto }
.thumb .meta{ padding:8px; font-size:12px; color:#cbd5e1; display:flex; justify-content:space-between; align-items:center; gap:6px }
.chip{ background:var(--chip); border:1px solid #263246; color:#cbd5e1; padding:2px 6px; border-radius:999px; font-size:11px }
.tools{ display:flex; gap:8px; align-items:center; flex-wrap:wrap; margin-top:8px }
.log{ background:#071120; border:1px solid #152036; border-radius:10px; padding:10px; white-space:pre-wrap; max-height:180px; overflow:auto; font-family:ui-monospace,Consolas,Menlo,monospace; font-size:12px; color:#cbd5e1 }
.sep{ height:1px; background:#1a2438; margin:12px 0 }
.footer{ display:flex; justify-content:space-between; align-items:center; gap:10px }
.small{ font-size:12px; color:#94a3b8 }
.ok{ color:#bbf7d0 }
.lightbox{ position:fixed; inset:0; background:rgba(0,0,0,.75); display:none; align-items:center; justify-content:center; z-index:9999 }
.lightbox.open{ display:flex }
.lb-inner{ position:relative; max-width:90vw; max-height:90vh; display:flex; flex-direction:column; gap:8px; align-items:center }
.lb-toolbar{ display:flex; gap:8px; flex-wrap:wrap; justify-content:center }
.lb-btn{ background:rgba(255,255,255,.08); border:1px solid rgba(255,255,255,.2); color:#e5e7eb; padding:8px 10px; border-radius:10px; cursor:pointer }
.lb-img-wrap{ position:relative; max-width:90vw; max-height:78vh; display:flex; align-items:center; justify-content:center }
.lb-img{ max-width:90vw; max-height:78vh; border-radius:8px; box-shadow:0 10px 30px rgba(0,0,0,.35) }
.lb-caption{ font-size:13px; color:#e5e7eb }
.modal{ position:fixed; inset:0; background:rgba(0,0,0,.6); display:none; align-items:center; justify-content:center; z-index:9999 }
.modal.open{ display:flex }
.modal-card{ width:min(820px,92vw); max-height:90vh; overflow:auto; background:#0f172a; border:1px solid #1f2a44; border-radius:14px; padding:16px 18px; color:#e2e8f0 }
.modal-card h3{ margin:0 0 8px; font-size:18px }
.modal-card p{ margin:6px 0; color:#cbd5e1; line-height:1.6 }
.modal-card ul{ margin:8px 0 0 18px; color:#cbd5e1 }
.modal-close{ margin-left:auto }
</style>
