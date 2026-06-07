<template>
  <main class="calendar-page" :class="{ festive: Boolean(todayHighlight) }" :style="themeStyle">
    <div v-if="todayHighlight" class="festival-decoration" aria-hidden="true">
      <span class="festival-line top"></span>
      <span class="festival-line bottom"></span>
      <span class="festival-knot left"></span>
      <span class="festival-knot right"></span>
    </div>

    <header class="calendar-header">
      <div class="calendar-header-inner">
        <RouterLink class="brand" to="/">
          <span class="brand-mark">P</span>
          <span class="brand-text">
            <strong>万年日历</strong>
            <small>Preferred Summary</small>
          </span>
        </RouterLink>

        <nav class="header-nav" aria-label="日历导航">
          <RouterLink to="/">资源目录</RouterLink>
        </nav>
      </div>
    </header>

    <section class="calendar-section" aria-label="万年历">
      <div class="calendar-shell">
        <div class="calendar-main">
          <div class="calendar-primary">
            <div class="calendar-board">
              <div class="calendar-board-toolbar">
                <label class="calendar-date-input">
                  <input
                    :value="selectedDateInput"
                    type="date"
                    aria-label="选择日期"
                    @input="handleDateInput(($event.target as HTMLInputElement).value)"
                  />
                </label>

                <div class="calendar-toolbar-actions">
                  <button class="calendar-ghost-button" type="button" title="上个月" @click="shiftCalendarMonth(-1)">‹</button>
                  <div class="calendar-toolbar-copy">
                    <strong>{{ calendarTitle }}</strong>
                    <span>{{ currentMonthHolidaySummary }}</span>
                  </div>
                  <button class="calendar-ghost-button" type="button" title="下个月" @click="shiftCalendarMonth(1)">›</button>
                  <button class="calendar-today-button" type="button" @click="goToday">今天</button>
                </div>
              </div>

              <div v-if="visibleHolidayPeriods.length > 0" class="holiday-strip">
                <span
                  v-for="period in visibleHolidayPeriods"
                  :key="`${period.name}-${period.start}`"
                >
                  {{ period.name }} {{ formatHolidayRange(period) }}
                </span>
              </div>

              <div class="calendar-weekdays" aria-label="星期标题">
                <span v-for="week in weekLabels" :key="week" class="calendar-weekday">{{ week }}</span>
              </div>

              <div class="calendar-days-grid" aria-label="当前月份日历">
                <button
                  v-for="dayItem in calendarDays"
                  :key="dayItem.key"
                  class="calendar-cell"
                  :class="{
                    muted: !dayItem.isCurrentMonth,
                    today: dayItem.isToday,
                    selected: dayItem.isSelected,
                    weekend: dayItem.isWeekend,
                    holiday: Boolean(dayItem.highlight),
                    rest: dayItem.holidayInfo?.type === 'rest',
                    work: dayItem.holidayInfo?.type === 'work'
                  }"
                  type="button"
                  :title="getCalendarCellTitle(dayItem)"
                  @click="selectCalendarDate(dayItem.date)"
                >
                  <span class="calendar-cell-head">
                    <span
                      v-if="dayItem.holidayInfo || dayItem.isToday"
                      class="rest-mark"
                      :class="{ today: dayItem.isToday && !dayItem.holidayInfo }"
                    >
                      {{ dayItem.holidayInfo ? (dayItem.holidayInfo.type === 'rest' ? '休' : '班') : '今' }}
                    </span>
                    <strong>{{ dayItem.day }}</strong>
                  </span>
                  <small>{{ getCalendarCellCaption(dayItem) }}</small>
                </button>
              </div>
            </div>

            <div class="calendar-info-layout">
              <section class="calendar-summary-panel">
                <div class="calendar-side-head">
                  <strong>{{ selectedPanelTitle }}</strong>
                  <span class="calendar-side-badge">{{ isTodaySelected ? '今天' : selectedDateBadge }}</span>
                </div>

                <div class="calendar-day-switcher">
                  <button class="calendar-arrow-button" type="button" aria-label="前一天" @click="shiftSelectedDate(-1)">‹</button>
                  <strong>{{ todayInfo.day }}</strong>
                  <button class="calendar-arrow-button" type="button" aria-label="后一天" @click="shiftSelectedDate(1)">›</button>
                </div>

                <div class="calendar-lunar-row">
                  <div class="calendar-lunar-summary">{{ selectedLunarSummary }}</div>
                  <div class="calendar-constellation-inline">
                    <span class="constellation-badge">星座</span>
                    <strong>{{ selectedConstellation.name }}</strong>
                    <p>{{ selectedConstellation.dateRange }}</p>
                  </div>
                </div>

                <div class="calendar-solar-terms">
                  <p>
                    <span>上一节气</span>
                    <strong>{{ previousSolarTermText }}</strong>
                  </p>
                  <p>
                    <span>下一节气</span>
                    <strong>{{ nextSolarTermText }}</strong>
                  </p>
                </div>

                <div class="calendar-yi-ji">
                  <div class="yi-ji-card good-card">
                    <span>宜</span>
                    <p>{{ almanacInfo.good.join(' ') }}</p>
                  </div>
                  <div class="yi-ji-card bad-card">
                    <span>忌</span>
                    <p>{{ almanacInfo.bad.join(' ') }}</p>
                  </div>
                </div>
              </section>

              <section class="calendar-almanac-panel">
                <div class="news-card">
                  <div class="news-card-head">
                    <div class="news-card-title">
                      <span class="news-fire" aria-hidden="true">🔥</span>
                      <strong>百度热搜榜</strong>
                    </div>
                    <a
                      class="news-more-link"
                      href="https://top.baidu.com/board?tab=realtime"
                      target="_blank"
                      rel="noreferrer"
                    >
                      更多
                    </a>
                  </div>
                  <div v-if="newsLoading" class="news-state">正在获取百度热搜…</div>
                  <div v-else-if="newsError" class="news-state error">{{ newsError }}</div>
                  <ul v-else class="news-list">
                    <li v-for="(item, index) in newsItems" :key="item.title">
                      <span class="news-rank" :class="newsRankClass(index)">{{ index + 1 }}</span>
                      <a v-if="item.url" :href="item.url" target="_blank" rel="noreferrer" class="news-link">
                        <span class="news-title-text">{{ item.title }}</span>
                        <small
                          v-if="resolveNewsBadge(index)"
                          class="news-tag"
                          :class="resolveNewsBadge(index)?.tone"
                        >
                          {{ resolveNewsBadge(index)?.label }}
                        </small>
                      </a>
                      <span v-else class="news-text">
                        <span class="news-title-text">{{ item.title }}</span>
                        <small
                          v-if="resolveNewsBadge(index)"
                          class="news-tag"
                          :class="resolveNewsBadge(index)?.tone"
                        >
                          {{ resolveNewsBadge(index)?.label }}
                        </small>
                      </span>
                    </li>
                  </ul>
                </div>
              </section>
            </div>
          </div>

          <aside class="outdoor-side-panel">
            <div class="outdoor-panel-head">
              <strong>今日户外建议</strong>
              <span>{{ outdoorLocationLabel }}</span>
            </div>

            <div class="outdoor-weather-overview" :class="`weather-${outdoorWeatherVisual}`">
              <div class="weather-scene" aria-hidden="true">
                <template v-if="outdoorWeatherVisual === 'rain'">
                  <span v-for="drop in 8" :key="`drop-${drop}`" class="weather-drop"></span>
                </template>
                <template v-else-if="outdoorWeatherVisual === 'wind'">
                  <span v-for="gust in 3" :key="`gust-${gust}`" class="weather-gust"></span>
                </template>
                <template v-else-if="outdoorWeatherVisual === 'sun'">
                  <span class="weather-sun"></span>
                  <span v-for="ray in 6" :key="`ray-${ray}`" class="weather-ray"></span>
                </template>
                <template v-else-if="outdoorWeatherVisual === 'cloud'">
                  <span class="weather-cloud cloud-a"></span>
                  <span class="weather-cloud cloud-b"></span>
                </template>
              </div>
              <strong>{{ outdoorWeatherTitle }}</strong>
              <div v-if="outdoorWeatherMetrics.length > 0" class="outdoor-metric-row">
                <span v-for="metric in outdoorWeatherMetrics" :key="metric.label">
                  <small>{{ metric.label }}</small>
                  <strong>{{ metric.value }}</strong>
                </span>
              </div>
            </div>

            <div v-if="outdoorLoading" class="outdoor-state-card">
              正在获取当前位置和今日天气…
            </div>
            <div v-else-if="outdoorError" class="outdoor-state-card error">
              {{ outdoorError }}
            </div>
            <div v-else class="outdoor-card-stack">
              <article
                v-for="card in outdoorRecommendationCards"
                :key="card.key"
                class="outdoor-activity-card"
                :class="card.tone"
              >
                <div class="outdoor-card-top">
                  <span class="outdoor-activity-tag">{{ card.label }}</span>
                  <strong>{{ card.verdict }}</strong>
                </div>
                <div class="outdoor-card-meta">
                  <span class="outdoor-score-pill">{{ card.level }}</span>
                  <span class="outdoor-score-text">建议指数 {{ card.score }}</span>
                </div>
                <p>{{ card.summary }}</p>
                <ul class="outdoor-bullet-list">
                  <li v-for="tip in card.tips" :key="tip">{{ tip }}</li>
                </ul>
              </article>
            </div>
          </aside>
        </div>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

interface LunarInfo {
  monthNumber: number
  dayNumber: number
  monthText: string
  dayText: string
  ganzhiYear: string
}

interface DayHighlight {
  label: string
  note: string
  accent: string
  soft: string
}

interface HolidayPeriod {
  name: string
  start: string
  end: string
}

interface HolidayDateInfo {
  name: string
  type: 'rest' | 'work'
}

interface CalendarDayItem {
  key: string
  date: Date
  day: number
  isCurrentMonth: boolean
  isToday: boolean
  isSelected: boolean
  isWeekend: boolean
  lunarText: string
  highlight: DayHighlight | null
  holidayInfo: HolidayDateInfo | null
}

interface OutdoorLocationInfo {
  latitude: number
  longitude: number
  accuracy: number
}

interface OutdoorWeatherSnapshot {
  temperature: number
  apparentTemperature: number
  humidity: number
  precipitation: number
  windSpeed: number
  weatherCode: number
  maxTemp: number | null
  minTemp: number | null
  precipitationProbability: number | null
  windMax: number | null
  uvMax: number | null
}

interface OutdoorRecommendationCard {
  key: 'fishing' | 'hiking'
  label: string
  verdict: string
  level: string
  score: number
  summary: string
  tips: string[]
  tone: 'great' | 'okay' | 'poor'
}

interface NewsItem {
  title: string
  hot?: string
  url?: string
}

const NEWS_CACHE_KEY = 'preferredsummary:baidu-news-cache'

const fallbackNewsItems: NewsItem[] = [
  { title: '百度热搜暂时不可用，稍后刷新可重试' },
  { title: '如果你已打开过此页面，后续会优先展示最近一次成功获取的缓存' },
  { title: '当前页面其余日历与户外建议功能不受影响' }
]

const weekLabels = ['一', '二', '三', '四', '五', '六', '日']

const lunarFestivals: Record<string, Omit<DayHighlight, 'accent' | 'soft'> & Partial<Pick<DayHighlight, 'accent' | 'soft'>>> = {
  '1-1': { label: '春节', note: '新岁启封，页面增加一点喜庆装饰。' },
  '1-15': { label: '元宵节', note: '灯火团圆，今天的页面多一点温暖气氛。' },
  '5-5': { label: '端午节', note: '端午安康，页面点缀清爽的节日纹理。' },
  '7-7': { label: '七夕', note: '七夕相会，页面保留柔和的节日提醒。' },
  '8-15': { label: '中秋节', note: '月色正好，页面增加中秋主题装饰。' },
  '9-9': { label: '重阳节', note: '重阳登高，页面增加沉稳的节日标识。' },
  '12-8': { label: '腊八节', note: '岁末渐近，页面增加一处传统节日提示。' },
  '12-23': { label: '小年', note: '小年纳福，页面增加年节氛围。' },
  '12-24': { label: '小年', note: '小年纳福，页面增加年节氛围。' }
}

const solarTerms = [
  { name: '小寒', month: 1, c: 5.4055, note: '寒气渐重，页面切换为节气提示。' },
  { name: '大寒', month: 1, c: 20.12, note: '岁末天寒，页面切换为节气提示。' },
  { name: '立春', month: 2, c: 3.87, note: '春意将启，页面增加轻盈的节气装饰。' },
  { name: '雨水', month: 2, c: 18.73, note: '雨水润物，页面增加清新的节气装饰。' },
  { name: '惊蛰', month: 3, c: 5.63, note: '万物萌动，页面增加节气提示。' },
  { name: '春分', month: 3, c: 20.646, note: '昼夜均分，页面增加节气提示。' },
  { name: '清明', month: 4, c: 4.81, note: '清和明朗，页面增加清明节气提示。' },
  { name: '谷雨', month: 4, c: 20.1, note: '春雨生百谷，页面增加节气提示。' },
  { name: '立夏', month: 5, c: 5.52, note: '夏意初起，页面增加明快的节气装饰。' },
  { name: '小满', month: 5, c: 21.04, note: '将满未满，页面增加节气提示。' },
  { name: '芒种', month: 6, c: 5.678, note: '忙有所得，页面增加节气提示。' },
  { name: '夏至', month: 6, c: 21.37, note: '白昼渐长，页面增加夏至提示。' },
  { name: '小暑', month: 7, c: 7.108, note: '暑气渐盛，页面增加节气提示。' },
  { name: '大暑', month: 7, c: 22.83, note: '盛夏时节，页面增加节气提示。' },
  { name: '立秋', month: 8, c: 7.5, note: '暑退凉生，页面增加立秋提示。' },
  { name: '处暑', month: 8, c: 23.13, note: '暑气将止，页面增加节气提示。' },
  { name: '白露', month: 9, c: 7.646, note: '露凝而白，页面增加节气提示。' },
  { name: '秋分', month: 9, c: 23.042, note: '秋色平分，页面增加节气提示。' },
  { name: '寒露', month: 10, c: 8.318, note: '天气转凉，页面增加节气提示。' },
  { name: '霜降', month: 10, c: 23.438, note: '秋深霜降，页面增加节气提示。' },
  { name: '立冬', month: 11, c: 7.438, note: '冬意初成，页面增加节气提示。' },
  { name: '小雪', month: 11, c: 22.36, note: '轻雪将至，页面增加节气提示。' },
  { name: '大雪', month: 12, c: 7.18, note: '雪意渐浓，页面增加节气提示。' },
  { name: '冬至', month: 12, c: 21.94, note: '昼短夜长，页面增加冬至提示。' }
] as const

const holidayPeriods2026: HolidayPeriod[] = [
  { name: '元旦', start: '2026-01-01', end: '2026-01-03' },
  { name: '春节', start: '2026-02-15', end: '2026-02-23' },
  { name: '清明节', start: '2026-04-04', end: '2026-04-06' },
  { name: '劳动节', start: '2026-05-01', end: '2026-05-05' },
  { name: '端午节', start: '2026-06-19', end: '2026-06-21' },
  { name: '中秋节', start: '2026-09-25', end: '2026-09-27' },
  { name: '国庆节', start: '2026-10-01', end: '2026-10-07' }
]

const adjustedWorkdays2026: Record<string, string> = {
  '2026-01-04': '元旦调休',
  '2026-02-14': '春节调休',
  '2026-02-28': '春节调休',
  '2026-05-09': '劳动节调休',
  '2026-09-20': '国庆节调休',
  '2026-10-10': '国庆节调休'
}

const today = ref(new Date())
const selectedDate = ref(new Date(today.value))
const calendarYear = ref(today.value.getFullYear())
const calendarMonth = ref(today.value.getMonth())
const outdoorLoading = ref(false)
const outdoorError = ref('')
const outdoorLocation = ref<OutdoorLocationInfo | null>(null)
const outdoorCity = ref('')
const outdoorWeather = ref<OutdoorWeatherSnapshot | null>(null)
const newsLoading = ref(false)
const newsError = ref('')
const newsItems = ref<NewsItem[]>([])

const selectedLunarInfo = computed(() => getLunarInfo(selectedDate.value))

const todayHighlight = computed<DayHighlight | null>(() => getDateHighlight(selectedDate.value))

const themeStyle = computed<Record<string, string>>(() => ({
  '--theme-accent': todayHighlight.value?.accent || '#245b55',
  '--theme-soft': todayHighlight.value?.soft || 'rgba(36, 91, 85, 0.1)'
}))

const todayInfo = computed(() => {
  const date = selectedDate.value
  const ganzhi = getGanzhiInfo(date)
  const nextTerm = getNextSolarTerm(date)
  return {
    day: date.getDate(),
    weekday: new Intl.DateTimeFormat('zh-CN', { weekday: 'long' }).format(date),
    dayOfYear: getDayOfYear(date),
    weekOfYear: getWeekOfYear(date),
    fullDate: new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    }).format(date),
    lunarText: `${selectedLunarInfo.value.monthText}${selectedLunarInfo.value.dayText}`,
    ganzhiYear: selectedLunarInfo.value.ganzhiYear,
    ganzhiMonth: ganzhi.month,
    ganzhiDay: ganzhi.day,
    daysUntilNextTerm: nextTerm ? `${nextTerm.days}天后` : '节气待定'
  }
})

const selectedDateInput = computed(() => formatDateKey(selectedDate.value))

const isTodaySelected = computed(() => isSameDay(selectedDate.value, today.value))

const selectedDateBadge = computed(() => {
  const holidayInfo = getHolidayDateInfo(selectedDate.value)
  const highlight = getDateHighlight(selectedDate.value)
  return holidayInfo?.name || highlight?.label || `农历 ${todayInfo.value.lunarText}`
})

const selectedPanelTitle = computed(() => {
  return `${todayInfo.value.fullDate} ${todayInfo.value.weekday} (第${todayInfo.value.weekOfYear}周)`
})

const selectedLunarSummary = computed(() => {
  return `(${getChineseZodiac(selectedDate.value)}年) 农历${todayInfo.value.lunarText}`
})

const calendarTitle = computed(() => `${calendarYear.value}年${calendarMonth.value + 1}月`)

const visibleHolidayPeriods = computed(() => {
  return holidayPeriods2026.filter((period) => isDateRangeInMonth(period.start, period.end, calendarYear.value, calendarMonth.value))
})

const currentMonthHolidaySummary = computed(() => {
  if (visibleHolidayPeriods.value.length === 0) return '无放假安排'
  const period = visibleHolidayPeriods.value[0]
  return `${period.name} ${formatHolidayRange(period)}`
})

const almanacInfo = computed(() => {
  const day = selectedDate.value.getDate()
  const goodPool = ['开市', '交易', '立券', '挂匾', '开光', '解除', '拆卸', '动土', '安床']
  const badPool = ['作灶', '出火', '祭祀', '嫁娶', '入宅', '安葬', '掘井', '远行']

  return {
    good: rotateList(goodPool, day).slice(0, 7),
    bad: rotateList(badPool, day + 3).slice(0, 5)
  }
})

const selectedConstellation = computed(() => getConstellationInfo(selectedDate.value))

const previousSolarTermText = computed(() => {
  const previousTerm = getPreviousSolarTerm(selectedDate.value)
  return previousTerm ? `${previousTerm.name} ${formatSolarTermDate(previousTerm.date)}` : '暂无'
})

const nextSolarTermText = computed(() => {
  const nextTerm = getNextSolarTerm(selectedDate.value)
  return nextTerm ? `${nextTerm.name} ${nextTerm.days}天后` : '暂无'
})

const outdoorLocationLabel = computed(() => {
  if (outdoorCity.value) return outdoorCity.value
  if (outdoorLocation.value) return '当前位置'
  if (outdoorLoading.value) return '定位中'
  if (outdoorError.value) return '未获取定位'
  return '待获取位置'
})

const outdoorWeatherTitle = computed(() => {
  if (!outdoorWeather.value) return outdoorLoading.value ? '正在分析今日天气' : '开启定位后可生成建议'
  return `${getWeatherLabel(outdoorWeather.value.weatherCode)} ${Math.round(outdoorWeather.value.temperature)}°C`
})

const outdoorWeatherMetrics = computed(() => {
  if (!outdoorWeather.value) return []
  const weather = outdoorWeather.value
  return [
    { label: '今日温差', value: weather.maxTemp !== null && weather.minTemp !== null ? `${Math.round(weather.minTemp)}-${Math.round(weather.maxTemp)}°C` : '待定' },
    { label: '湿度', value: `${Math.round(weather.humidity)}%` },
    { label: '降水概率', value: weather.precipitationProbability !== null ? `${Math.round(weather.precipitationProbability)}%` : '待定' },
    { label: '体感温度', value: `${Math.round(weather.apparentTemperature)}°C` }
  ]
})

const outdoorWeatherVisual = computed<'default' | 'rain' | 'wind' | 'sun' | 'cloud'>(() => {
  if (!outdoorWeather.value) return 'default'
  const weather = outdoorWeather.value
  if ([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99].includes(weather.weatherCode)) {
    return 'rain'
  }
  if (weather.windSpeed >= 24 || (weather.windMax ?? 0) >= 32) {
    return 'wind'
  }
  if (weather.weatherCode === 0) {
    return 'sun'
  }
  if ([1, 2, 3, 45, 48].includes(weather.weatherCode)) {
    return 'cloud'
  }
  return 'default'
})

const outdoorRecommendationCards = computed<OutdoorRecommendationCard[]>(() => {
  if (!outdoorWeather.value) {
    return [
      {
        key: 'fishing',
        label: '钓鱼',
        verdict: '等待天气数据',
        level: '待判断',
        score: 0,
        summary: '定位成功后会结合风、雨、体感温度给出建议。',
        tips: ['建议先允许浏览器定位', '定位成功后自动刷新判断'],
        tone: 'okay'
      },
      {
        key: 'hiking',
        label: '爬山',
        verdict: '等待天气数据',
        level: '待判断',
        score: 0,
        summary: '定位成功后会结合温度、紫外线和风力评估舒适度。',
        tips: ['建议先允许浏览器定位', '如定位失败会提示原因'],
        tone: 'okay'
      }
    ]
  }

  return [
    buildFishingRecommendation(outdoorWeather.value),
    buildHikingRecommendation(outdoorWeather.value)
  ]
})

const calendarDays = computed<CalendarDayItem[]>(() => {
  const current = today.value
  const firstDay = new Date(calendarYear.value, calendarMonth.value, 1)
  const startOffset = (firstDay.getDay() + 6) % 7
  const startDate = new Date(calendarYear.value, calendarMonth.value, 1 - startOffset)

  return Array.from({ length: 42 }, (_, index) => {
    const cellDate = new Date(startDate)
    cellDate.setDate(startDate.getDate() + index)
    return {
      key: `${cellDate.getFullYear()}-${cellDate.getMonth() + 1}-${cellDate.getDate()}`,
      date: new Date(cellDate),
      day: cellDate.getDate(),
      isCurrentMonth: cellDate.getMonth() === calendarMonth.value,
      isToday: isSameDay(cellDate, current),
      isSelected: isSameDay(cellDate, selectedDate.value),
      isWeekend: cellDate.getDay() === 0 || cellDate.getDay() === 6,
      lunarText: getCalendarCellLunarText(cellDate),
      highlight: getDateHighlight(cellDate),
      holidayInfo: getHolidayDateInfo(cellDate)
    }
  })
})

const shiftCalendarMonth = (step: number): void => {
  const next = new Date(calendarYear.value, calendarMonth.value + step, 1)
  calendarYear.value = next.getFullYear()
  calendarMonth.value = next.getMonth()
  syncSelectedDateToCalendarMonth()
}

const selectCalendarDate = (date: Date): void => {
  selectedDate.value = new Date(date)
  calendarYear.value = date.getFullYear()
  calendarMonth.value = date.getMonth()
}

const shiftSelectedDate = (step: number): void => {
  const next = new Date(selectedDate.value)
  next.setDate(selectedDate.value.getDate() + step)
  selectCalendarDate(next)
}

const handleDateInput = (value: string): void => {
  if (!value) return
  selectCalendarDate(parseDateKey(value))
}

const syncSelectedDateToCalendarMonth = (): void => {
  const maxDay = new Date(calendarYear.value, calendarMonth.value + 1, 0).getDate()
  const nextDay = Math.min(selectedDate.value.getDate(), maxDay)
  selectedDate.value = new Date(calendarYear.value, calendarMonth.value, nextDay)
}

const goToday = (): void => {
  selectedDate.value = new Date(today.value)
  calendarYear.value = today.value.getFullYear()
  calendarMonth.value = today.value.getMonth()
}

const loadBaiduNews = async (): Promise<void> => {
  newsLoading.value = true
  newsError.value = ''

  const endpoints = [
    { url: '/api/baidu-hot?platform=pc&tab=realtime', type: 'json' as const },
    { url: '/api/baidu-hot?tab=realtime', type: 'json' as const }
  ]

  try {
    let parsed: NewsItem[] = []
    for (const endpoint of endpoints) {
      try {
        const response = await fetch(endpoint.url)
        if (!response.ok) continue
        if (endpoint.type === 'json') {
          const data = await response.json()
          parsed = parseBaiduNewsFromApi(data)
        }
        if (parsed.length > 0) break
      } catch {
        continue
      }
    }

    if (parsed.length === 0) {
      throw new Error('百度热搜暂时获取失败，请稍后刷新重试。')
    }

    newsItems.value = parsed.slice(0, 10)
    cacheNewsItems(newsItems.value)
  } catch (error) {
    const cachedItems = getCachedNewsItems()
    if (cachedItems.length > 0) {
      newsItems.value = cachedItems
      newsError.value = ''
    } else {
      newsItems.value = fallbackNewsItems
      newsError.value = ''
    }
  } finally {
    newsLoading.value = false
  }
}

const loadOutdoorContext = async (): Promise<void> => {
  if (typeof window === 'undefined' || !('geolocation' in navigator)) {
    outdoorError.value = '当前浏览器不支持定位，无法生成今日钓鱼和爬山建议。'
    return
  }

  outdoorLoading.value = true
  outdoorError.value = ''

  try {
    const position = await getBrowserPosition()
    outdoorLocation.value = {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      accuracy: position.coords.accuracy
    }
    const regionOptions = await resolveOutdoorRegions(position.coords.latitude, position.coords.longitude)
    outdoorCity.value = regionOptions[0] || '当前位置'

    const params = new URLSearchParams({
      latitude: position.coords.latitude.toFixed(6),
      longitude: position.coords.longitude.toFixed(6),
      current: 'temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m',
      daily: 'temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max,uv_index_max',
      timezone: 'auto',
      forecast_days: '1'
    })
    const response = await fetch(`https://api.open-meteo.com/v1/forecast?${params.toString()}`)
    if (!response.ok) {
      throw new Error('天气服务暂时不可用，请稍后刷新重试。')
    }

    const data = await response.json()
    outdoorWeather.value = {
      temperature: Number(data.current?.temperature_2m ?? 0),
      apparentTemperature: Number(data.current?.apparent_temperature ?? 0),
      humidity: Number(data.current?.relative_humidity_2m ?? 0),
      precipitation: Number(data.current?.precipitation ?? 0),
      windSpeed: Number(data.current?.wind_speed_10m ?? 0),
      weatherCode: Number(data.current?.weather_code ?? 0),
      maxTemp: data.daily?.temperature_2m_max?.[0] ?? null,
      minTemp: data.daily?.temperature_2m_min?.[0] ?? null,
      precipitationProbability: data.daily?.precipitation_probability_max?.[0] ?? null,
      windMax: data.daily?.wind_speed_10m_max?.[0] ?? null,
      uvMax: data.daily?.uv_index_max?.[0] ?? null
    }
  } catch (error) {
    outdoorWeather.value = null
    outdoorCity.value = ''
    outdoorError.value = getOutdoorErrorMessage(error)
  } finally {
    outdoorLoading.value = false
  }
}

const getHolidayDateInfo = (date: Date): HolidayDateInfo | null => {
  const dateKey = formatDateKey(date)
  const adjustedName = adjustedWorkdays2026[dateKey]
  if (adjustedName) return { name: adjustedName, type: 'work' }

  const period = holidayPeriods2026.find((item) => isDateInRange(dateKey, item.start, item.end))
  return period ? { name: period.name, type: 'rest' } : null
}

const isDateRangeInMonth = (start: string, end: string, year: number, month: number): boolean => {
  const monthStart = formatDateKey(new Date(year, month, 1))
  const monthEnd = formatDateKey(new Date(year, month + 1, 0))
  return start <= monthEnd && end >= monthStart
}

const isDateInRange = (dateKey: string, start: string, end: string): boolean => {
  return dateKey >= start && dateKey <= end
}

const formatHolidayRange = (period: HolidayPeriod): string => {
  const start = parseDateKey(period.start)
  const end = parseDateKey(period.end)
  return `${start.getMonth() + 1}/${start.getDate()}-${end.getMonth() + 1}/${end.getDate()}`
}

const formatDateKey = (date: Date): string => {
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

const parseDateKey = (dateKey: string): Date => {
  const [year, month, day] = dateKey.split('-').map(Number)
  return new Date(year, month - 1, day)
}

const getCalendarCellLunarText = (date: Date): string => {
  const highlight = getDateHighlight(date)
  if (highlight) return highlight.label
  const lunarInfo = getLunarInfo(date)
  return lunarInfo.dayNumber === 1 ? lunarInfo.monthText : lunarInfo.dayText
}

const getCalendarCellCaption = (dayItem: CalendarDayItem): string => {
  if (dayItem.highlight) return dayItem.highlight.label
  return dayItem.lunarText
}

const getCalendarCellTitle = (dayItem: CalendarDayItem): string => {
  if (dayItem.highlight && dayItem.holidayInfo) {
    return `${dayItem.highlight.label} · ${dayItem.holidayInfo.name}`
  }
  return dayItem.holidayInfo?.name || dayItem.highlight?.label || dayItem.lunarText
}

const getDayOfYear = (date: Date): number => {
  const start = new Date(date.getFullYear(), 0, 1)
  return Math.floor((date.getTime() - start.getTime()) / 86400000) + 1
}

const getWeekOfYear = (date: Date): number => {
  const firstDay = new Date(date.getFullYear(), 0, 1)
  const firstMondayOffset = (firstDay.getDay() + 6) % 7
  return Math.ceil((getDayOfYear(date) + firstMondayOffset) / 7)
}

const getChineseZodiac = (date: Date): string => {
  const animals = ['鼠', '牛', '虎', '兔', '龙', '蛇', '马', '羊', '猴', '鸡', '狗', '猪']
  return animals[(date.getFullYear() - 4) % animals.length]
}

const getGanzhiInfo = (date: Date) => {
  const stems = ['甲', '乙', '丙', '丁', '戊', '己', '庚', '辛', '壬', '癸']
  const branches = ['子', '丑', '寅', '卯', '辰', '巳', '午', '未', '申', '酉', '戌', '亥']
  const monthIndex = (date.getFullYear() * 12 + date.getMonth() + 14) % 60
  const dayIndex = Math.floor(date.getTime() / 86400000 + 40) % 60

  return {
    month: `${stems[monthIndex % 10]}${branches[monthIndex % 12]}`,
    day: `${stems[dayIndex % 10]}${branches[dayIndex % 12]}`
  }
}

const getNextSolarTerm = (date: Date) => {
  for (let offset = 0; offset <= 35; offset += 1) {
    const candidate = new Date(date)
    candidate.setDate(date.getDate() + offset)
    const term = getSolarTerm(candidate)
    if (term) return { name: term.name, days: offset }
  }
  return null
}

const getPreviousSolarTerm = (date: Date) => {
  for (let offset = 0; offset <= 35; offset += 1) {
    const candidate = new Date(date)
    candidate.setDate(date.getDate() - offset)
    const term = getSolarTerm(candidate)
    if (term) return { name: term.name, date: candidate }
  }
  return null
}

const formatSolarTermDate = (date: Date): string => `${date.getMonth() + 1}月${date.getDate()}日`

const getBrowserPosition = (): Promise<GeolocationPosition> => {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 10000,
      maximumAge: 300000
    })
  })
}

const getOutdoorErrorMessage = (error: unknown): string => {
  if (typeof error === 'object' && error !== null && 'code' in error) {
    const geoCode = Number((error as { code?: number }).code)
    if (geoCode === 1) return '未获得定位权限，右侧建议暂时无法根据你所在位置生成。'
    if (geoCode === 2) return '当前位置获取失败，请检查设备定位服务后重试。'
    if (geoCode === 3) return '定位超时，请稍后刷新页面再试。'
  }
  if (error instanceof Error) return error.message
  return '天气信息加载失败，请稍后刷新页面再试。'
}

const parseBaiduNewsFromApi = (payload: unknown): NewsItem[] => {
  if (!payload || typeof payload !== 'object') return []

  const cards = Array.isArray((payload as { data?: { cards?: unknown[] } }).data?.cards)
    ? (payload as { data: { cards: unknown[] } }).data.cards
    : []

  const content = cards
    .flatMap((card) => {
      if (!card || typeof card !== 'object') return []
      const items = (card as { content?: unknown[] }).content
      return Array.isArray(items) ? items : []
    })

  return content
    .map((item) => {
      if (!item || typeof item !== 'object') return null
      const row = item as Record<string, unknown>
      const title = sanitizeNewsTitle(
        typeof row.word === 'string'
          ? row.word
          : typeof row.query === 'string'
            ? row.query
            : typeof row.title === 'string'
              ? row.title
              : ''
      )
      if (!title) return null

      const rawUrl = typeof row.url === 'string' ? row.url : ''
      const url = rawUrl.startsWith('http')
        ? rawUrl
        : `https://www.baidu.com/s?wd=${encodeURIComponent(title)}`

      const hotText = typeof row.hotScore === 'number'
        ? String(row.hotScore)
        : typeof row.hotScore === 'string'
          ? row.hotScore
          : typeof row.hotDesc === 'string'
            ? row.hotDesc
            : ''

      const tag = typeof row.labelName === 'string'
        ? row.labelName
        : typeof row.label === 'string'
          ? row.label
          : ''

      return {
        title,
        url,
        hot: [tag.trim(), hotText.trim()].filter(Boolean).join('|')
      } satisfies NewsItem
    })
    .filter((item: NewsItem | null): item is NewsItem => Boolean(item))
}

const sanitizeNewsTitle = (title: string): string => {
  return title
    .replace(/^#?\d+\s*[.、-]?\s*/, '')
    .replace(/\s*-\s*百度热搜.*$/, '')
    .trim()
}

const newsRankClass = (index: number): string => {
  if (index === 0) return 'top-one'
  if (index <= 2) return 'top-three'
  return 'normal'
}

const resolveNewsBadge = (index: number): { label: '热' | '新'; tone: 'hot' | 'new' } | null => {
  const source = newsItems.value[index]?.hot?.split('|')[0]?.trim() || ''
  if (source.includes('热') || source.includes('沸')) {
    return { label: '热', tone: 'hot' }
  }
  if (source.includes('新')) {
    return { label: '新', tone: 'new' }
  }
  return null
}

const cacheNewsItems = (items: NewsItem[]): void => {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.setItem(NEWS_CACHE_KEY, JSON.stringify(items))
  } catch {
    // Ignore storage write failures.
  }
}

const getCachedNewsItems = (): NewsItem[] => {
  if (typeof window === 'undefined') return []
  try {
    const raw = window.localStorage.getItem(NEWS_CACHE_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) return []
    return parsed
      .map((item) => {
        if (!item || typeof item !== 'object') return null
        const title = typeof item.title === 'string' ? item.title : ''
        if (!title) return null
        return {
          title,
          hot: typeof item.hot === 'string' ? item.hot : '',
          url: typeof item.url === 'string' ? item.url : ''
        } satisfies NewsItem
      })
      .filter((item: NewsItem | null): item is NewsItem => Boolean(item))
      .slice(0, 10)
  } catch {
    return []
  }
}

const resolveOutdoorRegions = async (latitude: number, longitude: number): Promise<string[]> => {
  try {
    const params = new URLSearchParams({
      format: 'jsonv2',
      lat: latitude.toFixed(6),
      lon: longitude.toFixed(6),
      'accept-language': 'zh-CN'
    })
    const response = await fetch(`https://nominatim.openstreetmap.org/reverse?${params.toString()}`)
    if (!response.ok) return ['当前位置']
    const data = await response.json()
    const address = data.address || {}
    const city = address.city || address.town || address.county || address.state || ''
    const district = address.city_district || address.suburb || address.borough || address.quarter || ''
    const combined = [city, district].filter(Boolean).join(' ')
    return [combined || city || district || data.name || '当前位置']
  } catch {
    return ['当前位置']
  }
}

const getWeatherLabel = (code: number): string => {
  if (code === 0) return '晴朗'
  if ([1, 2].includes(code)) return '多云'
  if (code === 3) return '阴天'
  if ([45, 48].includes(code)) return '有雾'
  if ([51, 53, 55, 56, 57].includes(code)) return '毛毛雨'
  if ([61, 63, 65, 66, 67, 80, 81, 82].includes(code)) return '下雨'
  if ([71, 73, 75, 77, 85, 86].includes(code)) return '降雪'
  if ([95, 96, 99].includes(code)) return '雷雨'
  return '天气平稳'
}

const isSevereWeather = (code: number): boolean => [65, 67, 75, 82, 86, 95, 96, 99].includes(code)

const buildFishingRecommendation = (weather: OutdoorWeatherSnapshot): OutdoorRecommendationCard => {
  let score = 58
  const tips: string[] = []
  if (weather.precipitation <= 0.2) {
    score += 10
    tips.push('降水很少，岸边停留会更从容。')
  } else if (weather.precipitation <= 1) {
    score += 2
    tips.push('有一点零星降水，带轻便雨具更稳妥。')
  } else {
    score -= 18
    tips.push('降水偏明显，水边活动舒适度会下降。')
  }
  if (weather.windSpeed <= 16) {
    score += 12
    tips.push('风力偏轻，抛竿和观察水面会更舒服。')
  } else if (weather.windSpeed <= 26) {
    score += 2
    tips.push('风速一般，尽量选择背风位置。')
  } else {
    score -= 16
    tips.push('风偏大，水面扰动和驻留体验都会变差。')
  }
  if (weather.apparentTemperature >= 16 && weather.apparentTemperature <= 28) {
    score += 10
  } else if (weather.apparentTemperature < 8 || weather.apparentTemperature > 33) {
    score -= 10
    tips.push('体感温度不太友好，注意补水或保暖。')
  }
  if (isSevereWeather(weather.weatherCode)) {
    score -= 24
    tips.push('存在明显强对流或恶劣天气信号，不建议久留水边。')
  }

  const finalScore = clampScore(score)
  return {
    key: 'fishing',
    label: '钓鱼',
    verdict: finalScore >= 72 ? '适合出钓，优先选背风安静点位' : finalScore >= 55 ? '可以出钓，但建议缩短停留时间' : '不太建议出钓，舒适度和稳定性偏弱',
    level: finalScore >= 72 ? '较适合' : finalScore >= 55 ? '可酌情' : '不建议',
    score: finalScore,
    summary: `综合天气为${getWeatherLabel(weather.weatherCode)}，这次判断主要看降水、风速和体感温度。分数越高，越适合较长时间停留在水边。`,
    tips: tips.slice(0, 3),
    tone: finalScore >= 72 ? 'great' : finalScore >= 55 ? 'okay' : 'poor'
  }
}

const buildHikingRecommendation = (weather: OutdoorWeatherSnapshot): OutdoorRecommendationCard => {
  let score = 60
  const tips: string[] = []
  if (weather.precipitation <= 0.2 && (weather.precipitationProbability ?? 0) < 35) {
    score += 12
    tips.push('雨水影响很小，路况通常会更稳定。')
  } else if ((weather.precipitationProbability ?? 0) >= 60 || weather.precipitation > 1) {
    score -= 20
    tips.push('降水概率偏高，山路湿滑风险会增加。')
  }
  if (weather.apparentTemperature >= 12 && weather.apparentTemperature <= 24) {
    score += 14
    tips.push('体感温度比较舒服，适合中等强度步行。')
  } else if (weather.apparentTemperature < 5 || weather.apparentTemperature > 31) {
    score -= 14
    tips.push('体感温度偏冷或偏热，登山消耗会更明显。')
  }
  if ((weather.windMax ?? weather.windSpeed) <= 24) {
    score += 6
  } else if ((weather.windMax ?? weather.windSpeed) > 36) {
    score -= 14
    tips.push('阵风偏大，山脊和开阔地会更难受。')
  }
  if ((weather.uvMax ?? 0) >= 8) {
    score -= 8
    tips.push('紫外线较强，建议做好遮阳和补水。')
  }
  if (isSevereWeather(weather.weatherCode)) {
    score -= 24
    tips.push('若有雷雨或强天气信号，今天应避免上山。')
  }

  const finalScore = clampScore(score)
  return {
    key: 'hiking',
    label: '爬山',
    verdict: finalScore >= 74 ? '适合爬山，可安排正常强度活动' : finalScore >= 56 ? '适合轻量出行，建议走短线' : '更建议改为室内或低强度活动',
    level: finalScore >= 74 ? '较适合' : finalScore >= 56 ? '可酌情' : '不建议',
    score: finalScore,
    summary: `综合天气为${getWeatherLabel(weather.weatherCode)}，主要参考降水概率、体感温度、紫外线和风力。分数越高，越适合户外步行和爬山。`,
    tips: tips.slice(0, 3),
    tone: finalScore >= 74 ? 'great' : finalScore >= 56 ? 'okay' : 'poor'
  }
}

const clampScore = (score: number): number => Math.max(0, Math.min(100, Math.round(score)))

const getConstellationInfo = (date: Date) => {
  const month = date.getMonth() + 1
  const day = date.getDate()
  const code = month * 100 + day
  const items = [
    { threshold: 120, name: '摩羯座', dateRange: '12.22 - 1.19', description: '务实稳重，适合按节奏推进计划。' },
    { threshold: 219, name: '水瓶座', dateRange: '1.20 - 2.18', description: '思路活跃，适合尝试新的安排和路线。' },
    { threshold: 321, name: '双鱼座', dateRange: '2.19 - 3.20', description: '感受力强，今天更适合顺着状态安排节奏。' },
    { threshold: 420, name: '白羊座', dateRange: '3.21 - 4.19', description: '行动力足，适合快速决定并马上执行。' },
    { threshold: 521, name: '金牛座', dateRange: '4.20 - 5.20', description: '偏好稳定，适合做踏实、持续性的安排。' },
    { threshold: 622, name: '双子座', dateRange: '5.21 - 6.21', description: '状态灵活，适合轻松切换不同活动。' },
    { threshold: 723, name: '巨蟹座', dateRange: '6.22 - 7.22', description: '更看重舒适感，适合兼顾节奏和休息。' },
    { threshold: 823, name: '狮子座', dateRange: '7.23 - 8.22', description: '表现欲和热情较强，适合有仪式感的安排。' },
    { threshold: 923, name: '处女座', dateRange: '8.23 - 9.22', description: '细节意识强，适合做精细化判断和准备。' },
    { threshold: 1024, name: '天秤座', dateRange: '9.23 - 10.23', description: '更重视平衡感，适合选择舒适度较高的方案。' },
    { threshold: 1123, name: '天蝎座', dateRange: '10.24 - 11.22', description: '专注度高，适合沉下心完成一件事。' },
    { threshold: 1222, name: '射手座', dateRange: '11.23 - 12.21', description: '偏向户外和探索，适合安排轻度出行。' },
    { threshold: 1232, name: '摩羯座', dateRange: '12.22 - 1.19', description: '务实稳重，适合按节奏推进计划。' }
  ]
  const currentCode = code
  return items.find((item) => currentCode < item.threshold) || items[0]
}

const rotateList = <T,>(items: T[], offset: number): T[] => {
  if (items.length === 0) return []
  const start = offset % items.length
  return [...items.slice(start), ...items.slice(0, start)]
}

const getDateHighlight = (date: Date): DayHighlight | null => {
  const lunarInfo = getLunarInfo(date)
  const lunarKey = `${lunarInfo.monthNumber}-${lunarInfo.dayNumber}`
  const lunarFestival = lunarFestivals[lunarKey]
  if (lunarFestival) return toHighlight(lunarFestival)

  const nextDate = new Date(date)
  nextDate.setDate(date.getDate() + 1)
  const nextLunarInfo = getLunarInfo(nextDate)
  if (nextLunarInfo.monthNumber === 1 && nextLunarInfo.dayNumber === 1) {
    return toHighlight({ label: '除夕', note: '辞旧迎新，页面增加年节装饰。' })
  }

  const solarTerm = getSolarTerm(date)
  return solarTerm ? toHighlight({ label: solarTerm.name, note: solarTerm.note }) : null
}

const toHighlight = (highlight: Omit<DayHighlight, 'accent' | 'soft'> & Partial<Pick<DayHighlight, 'accent' | 'soft'>>): DayHighlight => {
  return {
    label: highlight.label,
    note: highlight.note,
    accent: highlight.accent || '#b85b3d',
    soft: highlight.soft || 'rgba(184, 91, 61, 0.12)'
  }
}

const getLunarInfo = (date: Date): LunarInfo => {
  const numericParts = new Intl.DateTimeFormat('zh-CN-u-ca-chinese', { month: 'numeric', day: 'numeric' }).formatToParts(date)
  const textParts = new Intl.DateTimeFormat('zh-CN-u-ca-chinese', { year: 'numeric', month: 'long', day: 'numeric' }).formatToParts(date)

  const monthNumber = Number.parseInt(numericParts.find((part) => part.type === 'month')?.value || '1', 10)
  const dayNumber = Number.parseInt(numericParts.find((part) => part.type === 'day')?.value || '1', 10)
  const monthText = textParts.find((part) => part.type === 'month')?.value || `${monthNumber}月`
  const ganzhiYear = textParts.find((part) => part.type === 'yearName')?.value || ''

  return {
    monthNumber,
    dayNumber,
    monthText,
    dayText: formatLunarDay(dayNumber),
    ganzhiYear
  }
}

const formatLunarDay = (day: number): string => {
  const dayNames = ['', '初一', '初二', '初三', '初四', '初五', '初六', '初七', '初八', '初九', '初十', '十一', '十二', '十三', '十四', '十五', '十六', '十七', '十八', '十九', '二十', '廿一', '廿二', '廿三', '廿四', '廿五', '廿六', '廿七', '廿八', '廿九', '三十']
  return dayNames[day] || String(day)
}

const getSolarTerm = (date: Date) => {
  const year = date.getFullYear()
  if (year < 2000 || year > 2099) return null
  const yearTail = year % 100
  return solarTerms.find((term) => {
    if (term.month !== date.getMonth() + 1) return false
    const day = Math.floor(yearTail * 0.2422 + term.c) - Math.floor((yearTail - 1) / 4)
    return day === date.getDate()
  }) || null
}

const isSameDay = (left: Date, right: Date): boolean => {
  return left.getFullYear() === right.getFullYear()
    && left.getMonth() === right.getMonth()
    && left.getDate() === right.getDate()
}

onMounted(() => {
  void loadOutdoorContext()
  void loadBaiduNews()
})
</script>

<style scoped>
.calendar-page {
  --theme-accent: #245b55;
  --theme-soft: rgba(36, 91, 85, 0.1);
  position: relative;
  min-height: 100vh;
  overflow: hidden;
  color: #152421;
  background:
    linear-gradient(180deg, rgba(246, 250, 248, 0.95), rgba(255, 255, 255, 0.98)),
    url('https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1800&q=80') center top / cover fixed;
}

.calendar-page.festive {
  background:
    linear-gradient(180deg, var(--theme-soft), rgba(255, 255, 255, 0.96) 34%),
    linear-gradient(180deg, rgba(246, 250, 248, 0.95), rgba(255, 255, 255, 0.98)),
    url('https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1800&q=80') center top / cover fixed;
}

.festival-decoration {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

.festival-line {
  display: none;
}

.festival-line.top { top: 76px; }
.festival-line.bottom { top: 246px; }

.festival-knot {
  position: absolute;
  top: 104px;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--theme-accent);
  opacity: 0.18;
}

.festival-knot.left { left: 24px; }
.festival-knot.right { right: 24px; }

.calendar-header {
  position: sticky;
  top: 0;
  z-index: 10;
  backdrop-filter: blur(18px);
  background: rgba(250, 252, 251, 0.72);
  border-bottom: 1px solid rgba(21, 36, 33, 0.08);
}

.calendar-header-inner,
.calendar-section {
  width: min(1440px, calc(100% - 40px));
  margin: 0 auto;
}

.calendar-header-inner {
  min-height: 74px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.brand {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  color: inherit;
  text-decoration: none;
}

.brand-mark {
  width: 44px;
  height: 44px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 14px;
  color: #fff;
  font-size: 20px;
  font-weight: 800;
  background: linear-gradient(135deg, var(--theme-accent), #6ba497);
}

.brand-text strong {
  display: block;
  font-size: 18px;
}

.brand-text small {
  color: #70817b;
  font-size: 12px;
}

.header-nav a {
  color: #40514c;
  text-decoration: none;
  font-weight: 600;
}

.header-nav a:hover {
  color: var(--theme-accent);
}

.calendar-section {
  padding: 16px 0 22px;
}

.calendar-shell {
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.calendar-main {
  display: grid;
  grid-template-columns: minmax(0, 2.56fr) minmax(300px, 0.92fr);
  gap: 18px;
  align-items: stretch;
}

.calendar-primary {
  display: grid;
  grid-template-columns: minmax(0, 1.62fr) minmax(330px, 0.98fr);
  gap: 18px;
  height: 100%;
  min-height: 100%;
  align-items: stretch;
}

.calendar-info-layout {
  display: grid;
  grid-template-rows: auto 1fr;
  grid-template-columns: 1fr;
  gap: 16px;
  height: 100%;
}

.calendar-board,
.calendar-summary-panel,
.calendar-almanac-panel,
.outdoor-side-panel {
  border: 1px solid rgba(213, 84, 66, 0.1);
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(255, 252, 251, 0.96));
  box-shadow: 0 14px 28px rgba(132, 48, 38, 0.06);
}

.calendar-board {
  padding: 18px 18px 16px;
}

.calendar-board-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  margin-bottom: 14px;
}

.calendar-date-input {
  flex: 0 0 190px;
  width: 190px;
}

.calendar-date-input input {
  width: 100%;
  height: 52px;
  padding: 0 16px;
  border: 1px solid rgba(223, 85, 63, 0.34);
  border-radius: 12px;
  color: #1e2d43;
  background: #fffdfd;
  font: inherit;
  font-size: 17px;
}

.calendar-toolbar-actions {
  display: flex;
  flex: 1 1 320px;
  min-width: 0;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
}

.calendar-toolbar-copy {
  min-width: 0;
  max-width: 180px;
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  gap: 2px;
  text-align: center;
}

.calendar-toolbar-copy strong {
  color: #24344f;
  font-size: 19px;
}

.calendar-toolbar-copy span {
  color: #8b5b54;
  font-size: 12px;
}

.calendar-ghost-button,
.calendar-arrow-button,
.calendar-today-button {
  border: 0;
  border-radius: 12px;
  font: inherit;
  cursor: pointer;
  transition: transform 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.calendar-ghost-button,
.calendar-arrow-button {
  width: 46px;
  height: 46px;
  color: #d55342;
  background: rgba(239, 98, 77, 0.1);
}

.calendar-today-button {
  height: 44px;
  padding: 0 16px;
  color: #fff;
  background: linear-gradient(135deg, #e05b4a, #cb4637);
}

.calendar-ghost-button:hover,
.calendar-arrow-button:hover,
.calendar-today-button:hover {
  transform: translateY(-1px);
  box-shadow: 0 10px 24px rgba(213, 84, 66, 0.16);
}

.holiday-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding: 0 0 12px;
}

.holiday-strip span {
  padding: 6px 11px;
  border-radius: 999px;
  color: #b8483a;
  background: rgba(237, 112, 88, 0.12);
  font-size: 13px;
  font-weight: 700;
}

.calendar-weekdays {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  column-gap: 10px;
  margin-bottom: 6px;
}

.calendar-days-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  grid-auto-rows: 78px;
  column-gap: 10px;
  row-gap: 8px;
}

.calendar-weekday {
  min-height: 24px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #23344f;
  font-size: 14px;
  font-weight: 600;
}

.calendar-weekday:nth-child(6),
.calendar-weekday:nth-child(7) {
  color: #d55342;
}

.calendar-cell {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  justify-content: flex-start;
  padding: 8px 10px 8px;
  border: 0;
  border-radius: 14px;
  color: #000;
  background: transparent;
  font: inherit;
  cursor: pointer;
  box-shadow: inset 0 0 0 1px transparent;
  transition: background 0.16s ease, box-shadow 0.16s ease, transform 0.16s ease;
}

.calendar-cell:hover {
  background: rgba(223, 90, 72, 0.08);
}

.calendar-cell-head {
  display: flex;
  align-items: flex-start;
  justify-content: flex-start;
  gap: 8px;
  min-height: 26px;
}

.calendar-cell strong {
  order: 1;
  display: block;
  font-size: 24px;
  line-height: 0.95;
  letter-spacing: -0.5px;
}

.calendar-cell small {
  display: -webkit-box;
  margin-top: auto;
  color: #111;
  font-size: 12px;
  line-height: 1.22;
  text-align: left;
  word-break: break-word;
  overflow: hidden;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.calendar-cell.weekend,
.calendar-cell.holiday:not(.selected) {
  color: #d55342;
}

.calendar-cell.rest {
  background: linear-gradient(180deg, rgba(214, 234, 214, 0.98), rgba(237, 246, 237, 0.96));
}

.calendar-cell.work {
  color: #222;
  background: linear-gradient(180deg, rgba(241, 242, 245, 0.98), rgba(248, 248, 250, 0.96));
}

.calendar-cell.today {
  box-shadow: inset 0 0 0 2px rgba(213, 84, 66, 0.3);
}

.calendar-cell.selected {
  color: #fff;
  background: linear-gradient(180deg, #df5b4b, #cf4d3d);
  box-shadow:
    inset 0 0 0 1px rgba(255, 255, 255, 0.14),
    0 16px 24px rgba(207, 77, 61, 0.22);
}

.calendar-cell.selected strong,
.calendar-cell.selected small,
.calendar-cell.selected.rest small,
.calendar-cell.selected.work small,
.calendar-cell.selected .rest-mark {
  color: rgba(255, 255, 255, 0.92);
}

.calendar-cell.muted {
  opacity: 0.34;
}

.calendar-cell.work small,
.calendar-cell.work .rest-mark {
  color: #3f444a;
}

.calendar-cell.rest small {
  color: #c84c3d;
}

.rest-mark {
  order: 2;
  margin-left: auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 20px;
  color: #fff;
  padding: 0 6px;
  border-radius: 999px;
  background: #6fb267;
  font-size: 10px;
  line-height: 1;
  font-weight: 700;
  flex: 0 0 auto;
}

.rest-mark.today {
  background: rgba(213, 84, 66, 0.12);
  color: #d55342;
}

.calendar-cell.selected .rest-mark {
  background: rgba(255, 255, 255, 0.18);
}

.calendar-summary-panel,
.calendar-almanac-panel {
  padding: 14px 14px;
}

.calendar-side-head {
  display: grid;
  gap: 8px;
  text-align: left;
}

.calendar-side-head strong {
  display: block;
  color: #1f304a;
  font-size: 17px;
  line-height: 1.35;
}

.calendar-side-badge {
  display: inline-flex;
  color: #dd5746;
  font-size: 14px;
  font-weight: 800;
}

.calendar-day-switcher {
  display: grid;
  grid-template-columns: 44px 1fr 44px;
  align-items: center;
  gap: 10px;
  margin-top: 12px;
}

.calendar-day-switcher strong {
  text-align: center;
  color: #d55342;
  font-size: 46px;
  line-height: 1;
  letter-spacing: -2px;
}

.calendar-lunar-summary {
  text-align: left;
  color: #1f304a;
  font-size: 14px;
  font-weight: 700;
}

.calendar-lunar-row {
  margin-top: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
}

.calendar-constellation-inline {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  min-width: 0;
  text-align: right;
  flex: 0 0 auto;
}

.calendar-constellation-inline strong,
.calendar-constellation-inline p {
  margin: 0;
  color: #20314c;
  font-size: 13px;
  line-height: 1.4;
}

.calendar-constellation-inline p {
  color: #7c584f;
  font-weight: 600;
}

.calendar-solar-terms {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 14px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px dashed rgba(213, 84, 66, 0.2);
}

.calendar-solar-terms p {
  margin: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: #6f564d;
  font-size: 13px;
  line-height: 1.5;
}

.calendar-solar-terms strong {
  color: #d55342;
  font-size: 14px;
  text-align: right;
}

.calendar-yi-ji {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  align-content: start;
  margin-top: 14px;
}

.yi-ji-card {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr);
  gap: 12px;
  padding: 12px;
  border-radius: 14px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(213, 84, 66, 0.08);
}

.yi-ji-card span {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 50px;
  border-radius: 8px;
  color: #fff;
  font-size: 24px;
  font-weight: 800;
  line-height: 1;
  align-self: center;
}

.yi-ji-card p {
  margin: 0;
  color: #1f304a;
  font-size: 15px;
  line-height: 1.55;
}

.good-card span { background: #4ca63f; }
.bad-card span { background: #d55342; }

.constellation-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 4px 9px;
  border-radius: 999px;
  color: #d55342;
  background: rgba(213, 84, 66, 0.1);
  font-size: 11px;
  font-weight: 700;
}

.news-card {
  padding: 2px 0 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.news-card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.news-card-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.news-fire {
  font-size: 22px;
  line-height: 1;
}

.news-card-head strong {
  color: #ff6a1a;
  font-size: 16px;
  line-height: 1.2;
}

.news-more-link {
  color: #9aa3b2;
  text-decoration: none;
  font-size: 13px;
  font-weight: 600;
}

.news-more-link::after {
  content: '›';
  margin-left: 4px;
  font-size: 15px;
}

.news-more-link:hover {
  color: #ff7d2e;
}

.news-state {
  margin-top: 10px;
  color: #5f4e49;
  font-size: 12px;
  line-height: 1.5;
}

.news-state.error {
  color: #b34839;
}

.news-list {
  margin: 12px 0 0;
  padding: 0;
  list-style: none;
  display: grid;
  gap: 2px;
}

.news-list li {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr);
  align-items: center;
  gap: 10px;
  min-height: 34px;
}

.news-rank {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  justify-self: center;
  align-self: center;
  color: #9aa3b2;
  font-size: 16px;
  font-weight: 700;
  line-height: 1;
}

.news-rank.top-one,
.news-rank.top-three {
  color: #ff6a1a;
}

.news-link,
.news-text {
  width: 100%;
  min-width: 0;
  min-height: 34px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  color: #222f47;
  text-decoration: none;
  font-size: 14px;
  line-height: 1.5;
  border-radius: 10px;
  transition: background 0.16s ease, color 0.16s ease;
}

.news-link:hover {
  color: #d55342;
  background: rgba(255, 115, 74, 0.06);
}

.news-title-text {
  min-width: 0;
  flex: 1 1 auto;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.news-tag {
  flex: 0 0 auto;
  min-width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 6px;
  border-radius: 999px;
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}

.news-tag.hot {
  background: linear-gradient(180deg, #ff6a1a, #ff4b27);
}

.news-tag.new {
  background: linear-gradient(180deg, #ffbe33, #ff9d00);
}

.outdoor-side-panel {
  display: flex;
  flex-direction: column;
  align-self: stretch;
  padding: 14px;
}

.outdoor-panel-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}

.outdoor-panel-head strong {
  color: #20314c;
  font-size: 17px;
  line-height: 1.35;
}

.outdoor-panel-head span {
  flex: 0 0 auto;
  text-align: right;
  color: #8b5b54;
  font-size: 12px;
  line-height: 1.35;
}

.outdoor-weather-overview {
  position: relative;
  overflow: hidden;
  margin-top: 12px;
  padding: 12px;
  border-radius: 14px;
  background: linear-gradient(180deg, rgba(255, 244, 241, 0.96), rgba(255, 251, 250, 0.98));
  box-shadow: inset 0 0 0 1px rgba(213, 84, 66, 0.08);
}

.outdoor-weather-overview strong {
  position: relative;
  z-index: 1;
  display: block;
  color: #d55342;
  font-size: 17px;
  line-height: 1.4;
}

.outdoor-weather-overview p {
  margin: 8px 0 0;
  color: #5c4a45;
  font-size: 13px;
  line-height: 1.5;
}

.outdoor-metric-row {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 10px;
}

.weather-scene {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
  border-radius: inherit;
  opacity: 0.95;
}

.weather-drop,
.weather-gust,
.weather-ray,
.weather-sun,
.weather-cloud {
  position: absolute;
}

.weather-drop {
  top: -14px;
  width: 2px;
  height: 16px;
  border-radius: 999px;
  background: linear-gradient(180deg, rgba(137, 187, 255, 0), rgba(137, 187, 255, 0.9));
  animation: weather-rain 1.8s linear infinite;
}

.weather-drop:nth-child(1) { left: 66%; animation-delay: 0s; }
.weather-drop:nth-child(2) { left: 72%; animation-delay: 0.25s; }
.weather-drop:nth-child(3) { left: 78%; animation-delay: 0.5s; }
.weather-drop:nth-child(4) { left: 84%; animation-delay: 0.1s; }
.weather-drop:nth-child(5) { left: 88%; animation-delay: 0.7s; }
.weather-drop:nth-child(6) { left: 92%; animation-delay: 0.4s; }
.weather-drop:nth-child(7) { left: 81%; animation-delay: 1s; }
.weather-drop:nth-child(8) { left: 95%; animation-delay: 0.85s; }

.weather-gust {
  right: 12px;
  width: 72px;
  height: 18px;
  border-top: 2px solid rgba(138, 169, 201, 0.58);
  border-radius: 999px;
  animation: weather-wind 3.4s ease-in-out infinite;
}

.weather-gust::after {
  content: '';
  position: absolute;
  right: -10px;
  top: -2px;
  width: 18px;
  height: 12px;
  border-top: 2px solid rgba(138, 169, 201, 0.45);
  border-right: 2px solid transparent;
  border-radius: 999px;
}

.weather-gust:nth-child(1) { top: 18px; }
.weather-gust:nth-child(2) { top: 44px; width: 58px; animation-delay: 0.5s; }
.weather-gust:nth-child(3) { top: 70px; width: 82px; animation-delay: 1s; }

.weather-sun {
  top: 14px;
  right: 18px;
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 35%, rgba(255, 250, 214, 0.95), rgba(255, 193, 62, 0.96));
  box-shadow: 0 0 24px rgba(255, 194, 64, 0.35);
}

.weather-ray {
  top: 34px;
  right: 38px;
  width: 2px;
  height: 16px;
  border-radius: 999px;
  background: rgba(255, 194, 64, 0.6);
  transform-origin: center -6px;
  animation: weather-pulse 2.8s ease-in-out infinite;
}

.weather-ray:nth-child(2) { transform: rotate(0deg) translateY(-26px); }
.weather-ray:nth-child(3) { transform: rotate(60deg) translateY(-26px); }
.weather-ray:nth-child(4) { transform: rotate(120deg) translateY(-26px); }
.weather-ray:nth-child(5) { transform: rotate(180deg) translateY(-26px); }
.weather-ray:nth-child(6) { transform: rotate(240deg) translateY(-26px); }
.weather-ray:nth-child(7) { transform: rotate(300deg) translateY(-26px); }

.weather-cloud {
  top: 18px;
  right: 16px;
  width: 74px;
  height: 26px;
  border-radius: 999px;
  background: rgba(203, 214, 228, 0.68);
  filter: blur(0.2px);
  animation: weather-float 5.2s ease-in-out infinite;
}

.weather-cloud::before,
.weather-cloud::after {
  content: '';
  position: absolute;
  border-radius: 50%;
  background: inherit;
}

.weather-cloud::before {
  left: 12px;
  bottom: 8px;
  width: 26px;
  height: 26px;
}

.weather-cloud::after {
  left: 34px;
  bottom: 10px;
  width: 30px;
  height: 30px;
}

.weather-cloud.cloud-b {
  top: 42px;
  right: 54px;
  width: 52px;
  height: 18px;
  opacity: 0.72;
  animation-delay: 1.1s;
}

.outdoor-weather-overview.weather-rain {
  background: linear-gradient(180deg, rgba(239, 245, 255, 0.98), rgba(252, 253, 255, 0.98));
}

.outdoor-weather-overview.weather-wind {
  background: linear-gradient(180deg, rgba(246, 249, 255, 0.98), rgba(255, 252, 250, 0.98));
}

.outdoor-weather-overview.weather-sun {
  background: linear-gradient(180deg, rgba(255, 248, 223, 0.98), rgba(255, 252, 243, 0.98));
}

.outdoor-weather-overview.weather-cloud {
  background: linear-gradient(180deg, rgba(244, 247, 251, 0.98), rgba(252, 251, 249, 0.98));
}

@keyframes weather-rain {
  0% {
    transform: translate3d(0, 0, 0) rotate(16deg);
    opacity: 0;
  }
  18% {
    opacity: 1;
  }
  100% {
    transform: translate3d(-22px, 112px, 0) rotate(16deg);
    opacity: 0;
  }
}

@keyframes weather-wind {
  0%, 100% {
    transform: translateX(0);
    opacity: 0.42;
  }
  50% {
    transform: translateX(-12px);
    opacity: 0.88;
  }
}

@keyframes weather-pulse {
  0%, 100% {
    opacity: 0.45;
  }
  50% {
    opacity: 0.88;
  }
}

@keyframes weather-float {
  0%, 100% {
    transform: translateX(0);
  }
  50% {
    transform: translateX(-8px);
  }
}

.outdoor-metric-row span,
.outdoor-state-card {
  padding: 10px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.92);
}

.outdoor-metric-row small {
  display: block;
  color: #8b5b54;
  font-size: 12px;
}

.outdoor-metric-row strong {
  display: block;
  margin-top: 4px;
  color: #21324c;
  font-size: 14px;
}

.outdoor-state-card {
  margin-top: 12px;
  color: #5c4a45;
  font-size: 13px;
  line-height: 1.5;
}

.outdoor-state-card.error {
  color: #b34839;
  background: rgba(255, 239, 236, 0.96);
}

.outdoor-card-stack {
  display: grid;
  gap: 10px;
  margin-top: 12px;
}

.outdoor-activity-card {
  padding: 12px;
  border-radius: 14px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(213, 84, 66, 0.08);
}

.outdoor-activity-card.great {
  background: linear-gradient(180deg, rgba(234, 247, 235, 0.98), rgba(248, 254, 248, 0.98));
}

.outdoor-activity-card.okay {
  background: linear-gradient(180deg, rgba(255, 248, 234, 0.98), rgba(255, 252, 245, 0.98));
}

.outdoor-activity-card.poor {
  background: linear-gradient(180deg, rgba(255, 239, 236, 0.98), rgba(255, 248, 246, 0.98));
}

.outdoor-card-top {
  display: flex;
  align-items: flex-start;
  flex-direction: column;
  gap: 8px;
}

.outdoor-card-top strong {
  color: #20314c;
  font-size: 15px;
  line-height: 1.35;
}

.outdoor-card-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 8px;
}

.outdoor-score-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 4px 9px;
  border-radius: 999px;
  color: #d55342;
  background: rgba(213, 84, 66, 0.1);
  font-size: 11px;
  font-weight: 700;
}

.outdoor-score-text {
  color: #6d5953;
  font-size: 12px;
  font-weight: 600;
}

.outdoor-activity-tag {
  display: inline-flex;
  padding: 5px 10px;
  border-radius: 999px;
  color: #fff;
  background: linear-gradient(135deg, #df5a49, #cb4738);
  font-size: 12px;
  font-weight: 700;
}

.outdoor-activity-card p {
  margin: 10px 0 0;
  color: #4f423e;
  font-size: 13px;
  line-height: 1.5;
}

.outdoor-bullet-list {
  margin: 8px 0 0;
  padding-left: 18px;
  color: #6d5953;
  font-size: 12px;
  line-height: 1.55;
}

@media (max-width: 980px) {
  .header-nav {
    display: none;
  }

  .calendar-header-inner,
  .calendar-section {
    width: min(100%, calc(100% - 32px));
  }

  .calendar-main,
  .calendar-primary,
  .calendar-info-layout {
    grid-template-columns: 1fr;
  }

  .calendar-board-toolbar {
    align-items: stretch;
  }

  .calendar-toolbar-actions {
    justify-content: space-between;
  }

  .outdoor-card-stack {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 680px) {
  .calendar-header-inner,
  .calendar-section {
    width: min(100%, calc(100% - 24px));
  }

  .brand-text small {
    display: none;
  }

  .calendar-shell {
    padding: 0;
    border-radius: 0;
  }

  .calendar-board,
  .calendar-summary-panel,
  .calendar-almanac-panel,
  .outdoor-side-panel {
    border-radius: 14px;
  }

  .calendar-board {
    padding: 14px;
  }

  .calendar-date-input input {
    width: 100%;
    font-size: 16px;
  }

  .calendar-date-input {
    width: 100%;
    flex-basis: 100%;
  }

  .calendar-toolbar-actions {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 10px;
  }

  .calendar-toolbar-copy {
    grid-column: 1 / -1;
    order: -1;
  }

  .calendar-ghost-button,
  .calendar-arrow-button,
  .calendar-today-button {
    width: 100%;
  }

  .calendar-weekdays {
    column-gap: 8px;
  }

  .calendar-days-grid {
    grid-auto-rows: 72px;
    gap: 8px;
  }

  .calendar-weekday {
    min-height: 28px;
    font-size: 14px;
  }

  .calendar-cell {
    padding: 10px 9px;
    border-radius: 12px;
  }

  .calendar-cell-head {
    min-height: 28px;
  }

  .calendar-cell strong {
    font-size: 21px;
  }

  .calendar-cell small {
    font-size: 12px;
  }

  .rest-mark {
    min-width: 24px;
    height: 20px;
    padding: 0 6px;
    font-size: 11px;
  }

  .calendar-day-switcher strong {
    font-size: 64px;
  }

  .calendar-lunar-row {
    flex-direction: column;
    align-items: flex-start;
  }

  .calendar-constellation-inline {
    justify-content: flex-start;
    flex-wrap: wrap;
    text-align: left;
  }

  .calendar-solar-terms {
    grid-template-columns: 1fr;
  }

  .yi-ji-card {
    grid-template-columns: 52px minmax(0, 1fr);
  }

  .yi-ji-card span {
    min-height: 72px;
    font-size: 24px;
  }

  .yi-ji-card p,
  .outdoor-activity-card p {
    font-size: 15px;
  }

  .outdoor-metric-row,
  .outdoor-card-stack {
    grid-template-columns: 1fr;
  }

  .calendar-yi-ji {
    grid-template-columns: 1fr;
  }

  .outdoor-panel-head {
    flex-direction: column;
  }

  .outdoor-panel-head span {
    max-width: none;
    text-align: left;
  }
}
</style>
