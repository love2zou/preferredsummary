<template>
  <div class="home-container">
    <el-row :gutter="20">
      <!-- 欢迎卡片 -->
      <el-col :span="24">
        <el-card class="welcome-card">
          <div class="welcome-content">
            <div class="welcome-text">
              <h1>欢迎使用后台管理系统</h1>
              <p>当前时间：{{ currentTime }}</p>
              <p>欢迎您，{{ authStore.user?.username || '用户' }}！</p>
            </div>
            <div class="welcome-icon">
              <el-icon size="80" color="#409EFF"><House /></el-icon>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 系统资源监控卡片 -->
    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :span="8">
        <el-card class="resource-card">
          <div class="resource-content">
            <div class="resource-icon">
              <el-icon size="40" color="#67C23A"><Cpu /></el-icon>
            </div>
            <div class="resource-info">
              <div class="resource-number">{{ latestResource.cpuUsage }}%</div>
              <div class="resource-label">CPU使用率</div>
            </div>
          </div>
          <el-progress 
            :percentage="latestResource.cpuUsage" 
            :color="getProgressColor(latestResource.cpuUsage)"
            :show-text="false"
            :stroke-width="6"
          />
        </el-card>
      </el-col>
      
      <el-col :span="8">
        <el-card class="resource-card">
          <div class="resource-content">
            <div class="resource-icon">
              <el-icon size="40" color="#E6A23C"><MemoryCard /></el-icon>
            </div>
            <div class="resource-info">
              <div class="resource-number">{{ latestResource.memoryUsage }}%</div>
              <div class="resource-label">内存使用率</div>
            </div>
          </div>
          <el-progress 
            :percentage="latestResource.memoryUsage" 
            :color="getProgressColor(latestResource.memoryUsage)"
            :show-text="false"
            :stroke-width="6"
          />
        </el-card>
      </el-col>
      
      <el-col :span="8">
        <el-card class="resource-card">
          <div class="resource-content">
            <div class="resource-icon">
              <el-icon size="40" color="#F56C6C"><HardDisk /></el-icon>
            </div>
            <div class="resource-info">
              <div class="resource-number">{{ latestResource.diskUsage }}%</div>
              <div class="resource-label">磁盘使用率 {{ formatBytes(latestResource.diskUsed) }} / {{ formatBytes(latestResource.diskTotal) }}</div>
            </div>
          </div>
          <el-progress 
            :percentage="latestResource.diskUsage" 
            :color="getProgressColor(latestResource.diskUsage)"
            :show-text="false"
            :stroke-width="6"
          />
        </el-card>
      </el-col>
    </el-row>

    <!-- 系统资源趋势图 -->
    <el-row style="margin-top: 20px">
      <el-col :span="24">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>系统资源趋势（过去24小时）</span>
              <el-button size="small" @click="refreshChartData">
                <el-icon><Refresh /></el-icon>
                刷新
              </el-button>
            </div>
          </template>
          <div ref="chartContainer" style="width: 100%; height: 400px;"></div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 统计卡片 -->
    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon">
              <el-icon size="40" color="#67C23A"><User /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-number">{{ stats.totalUsers }}</div>
              <div class="stat-label">总用户数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon">
              <el-icon size="40" color="#E6A23C"><DataLine /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-number">{{ stats.todayVisits }}</div>
              <div class="stat-label">今日访问</div>
            </div>
          </div>
        </el-card>
      </el-col>
      
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon">
              <el-icon size="40" color="#F56C6C"><Bell /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-number">{{ stats.notifications }}</div>
              <div class="stat-label">待处理通知</div>
            </div>
          </div>
        </el-card>
      </el-col>
      
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon">
              <el-icon size="40" color="#909399"><Setting /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-number">{{ stats.systemStatus }}</div>
              <div class="stat-label">系统状态</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 快捷操作 -->
    <el-row style="margin-top: 20px">
      <el-col :span="24">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>快捷操作</span>
            </div>
          </template>
          <div class="quick-actions">
            <el-button type="primary" @click="$router.push('/user-management')">
              <el-icon><User /></el-icon>
              用户管理
            </el-button>
            <el-button type="success">
              <el-icon><Plus /></el-icon>
              新增用户
            </el-button>
            <el-button type="info">
              <el-icon><Setting /></el-icon>
              系统设置
            </el-button>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import systemResourceApi, { type SystemResource } from '@/api/systemResource'
import { useAuthStore } from '@/stores/auth'
import {
  Bell,
  Cpu,
  DataLine,
  House,
  Plus,
  Refresh,
  Setting,
  User
} from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { ElMessage } from 'element-plus'
import { nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'

const authStore = useAuthStore()

// 当前时间
const currentTime = ref('')
let timeInterval: NodeJS.Timeout
let resourceInterval: NodeJS.Timeout
let chart: echarts.ECharts | null = null
const chartContainer = ref<HTMLDivElement>()

// 最新系统资源数据
// 最新系统资源数据
const latestResource = reactive<SystemResource>({
  id: 0,
  hostName: '',
  cpuUsage: 0,
  memoryUsage: 0,
  diskName: '',
  diskUsage: 0,
  diskTotal: 0,
  diskUsed: 0,
  diskFree: 0,
  crtTime: '',
  updTime: ''
})

// 统计数据（模拟数据）
const stats = reactive({
  totalUsers: 1234,
  todayVisits: 567,
  notifications: 8,
  systemStatus: '正常'
})

// 更新时间
const updateTime = () => {
  currentTime.value = new Date().toLocaleString('zh-CN')
}

// 获取进度条颜色
const getProgressColor = (percentage: number) => {
  if (percentage < 50) return '#67C23A'
  if (percentage < 80) return '#E6A23C'
  return '#F56C6C'
}

// 格式化字节数
const formatBytes = (gbValue: number) => {
    if (gbValue === 0) return '0 GB'
    if (gbValue < 1) {
      return (gbValue * 1024).toFixed(2) + ' MB'
    } else if (gbValue >= 1024) {
      return (gbValue / 1024).toFixed(2) + ' TB'
    } else {
      return gbValue.toFixed(2) + ' GB'
    }
}

// 获取最新系统资源数据
const loadLatestResource = async () => {
  try {
    const response = await systemResourceApi.getLatestSystemResource()
    if (response.success) {
      Object.assign(latestResource, response.data)
    }
  } catch (error) {
    console.error('获取系统资源数据失败:', error)
    // 设置默认值，避免页面报错
    Object.assign(latestResource, {
      id: 0,
      cpuUsage: 0,
      memoryUsage: 0,
      diskUsage: 0,
      diskTotal: 0,
      diskUsed: 0,
      diskFree: 0,
      crtTime: new Date().toISOString(),
      updTime: new Date().toISOString()
    })
  }
}

// 初始化图表
const initChart = async () => {
  await nextTick()
  if (!chartContainer.value) return
  
  chart = echarts.init(chartContainer.value)
  
  const option = {
    title: {
      text: '系统资源使用率趋势',
      top: 0,
      left: 'center',
      textStyle: {
        fontSize: 16,
        color: '#303133'
      }
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'cross'
      },
      formatter: function(params: any) {
        let result = params[0].axisValueLabel + '<br/>'
        params.forEach((param: any) => {
          result += param.marker + param.seriesName + ': ' + param.value + '%<br/>'
        })
        return result
      }
    },
    legend: {
      data: ['CPU使用率', '内存使用率', '磁盘使用率'],
      top: 30
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '3%',
      top: '15%',
      containLabel: true
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: [],
      axisLabel: {
        formatter: function(value: string) {
          const date = new Date(value)
          return date.getHours().toString().padStart(2, '0') + ':' + 
                 date.getMinutes().toString().padStart(2, '0')
        }
      }
    },
    yAxis: {
      type: 'value',
      min: 0,
      max: 100,
      axisLabel: {
        formatter: '{value}%'
      }
    },
    series: [
      {
        name: 'CPU使用率',
        type: 'line',
        smooth: true,
        data: [],
        itemStyle: {
          color: '#67C23A'
        },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(103, 194, 58, 0.3)'
            }, {
              offset: 1, color: 'rgba(103, 194, 58, 0.1)'
            }]
          }
        }
      },
      {
        name: '内存使用率',
        type: 'line',
        smooth: true,
        data: [],
        itemStyle: {
          color: '#E6A23C'
        },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(230, 162, 60, 0.3)'
            }, {
              offset: 1, color: 'rgba(230, 162, 60, 0.1)'
            }]
          }
        }
      },
      {
        name: '磁盘使用率',
        type: 'line',
        smooth: true,
        data: [],
        itemStyle: {
          color: '#F56C6C'
        },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(245, 108, 108, 0.3)'
            }, {
              offset: 1, color: 'rgba(245, 108, 108, 0.1)'
            }]
          }
        }
      }
    ]
  }
  
  chart.setOption(option)
  
  // 监听窗口大小变化
  window.addEventListener('resize', () => {
    chart?.resize()
  })
}

// 加载图表数据
const loadChartData = async () => {
  try {
    const response = await systemResourceApi.getDailySystemResourceData()
    if (response.success && chart) {
      const data = response.data
      const times = data.map(item => item.crtTime)
      const cpuData = data.map(item => Number(item.cpuUsage))
      const memoryData = data.map(item => Number(item.memoryUsage))
      const diskData = data.map(item => Number(item.diskUsage))
      
      chart.setOption({
        xAxis: {
          data: times
        },
        series: [
          { data: cpuData },
          { data: memoryData },
          { data: diskData }
        ]
      })
    }
  } catch (error) {
    console.error('获取图表数据失败:', error)
    ElMessage.error('获取图表数据失败')
  }
}

// 刷新图表数据
const refreshChartData = async () => {
  await loadChartData()
  ElMessage.success('图表数据已刷新')
}

// 生命周期
onMounted(async () => {
  console.log('Home component mounted')
  updateTime()
  timeInterval = setInterval(updateTime, 1000)
  
  // 加载系统资源数据
  await loadLatestResource()
  resourceInterval = setInterval(loadLatestResource, 30000) // 每30秒刷新一次
  
  // 初始化图表
  await initChart()
  await loadChartData()
})

onUnmounted(() => {
  if (timeInterval) {
    clearInterval(timeInterval)
  }
  if (resourceInterval) {
    clearInterval(resourceInterval)
  }
  if (chart) {
    chart.dispose()
  }
  window.removeEventListener('resize', () => {
    chart?.resize()
  })
})
</script>

<style scoped>
.home-container {
  padding: 0;
}

.welcome-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
}

.welcome-card :deep(.el-card__body) {
  padding: 25px 30px;
}

.welcome-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.welcome-text h1 {
  margin: 0 0 12px 0;
  font-size: 26px;
  font-weight: 600;
}

.welcome-text p {
  margin: 6px 0;
  font-size: 15px;
  opacity: 0.9;
}

.welcome-icon {
  opacity: 0.8;
}

.resource-card {
  transition: transform 0.3s, box-shadow 0.3s;
}

.resource-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.1);
}

.resource-content {
  display: flex;
  align-items: center;
  padding: 10px 0;
  margin-bottom: 10px;
}

.resource-icon {
  margin-right: 16px;
}

.resource-info {
  flex: 1;
}

.resource-number {
  font-size: 24px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 4px;
}

.resource-label {
  font-size: 14px;
  color: #909399;
  margin-bottom: 2px;
}

.resource-detail {
  font-size: 12px;
  color: #C0C4CC;
}

.stat-card {
  transition: transform 0.3s, box-shadow 0.3s;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.1);
}

.stat-content {
  display: flex;
  align-items: center;
  padding: 10px 0;
}

.stat-icon {
  margin-right: 16px;
}

.stat-info {
  flex: 1;
}

.stat-number {
  font-size: 24px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 14px;
  color: #909399;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
  color: #303133;
}

.quick-actions {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.quick-actions .el-button {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* 响应式优化 */
@media (max-width: 768px) {
  .home-container {
    padding: 10px;
  }
  
  .welcome-content {
    flex-direction: column;
    text-align: center;
    gap: 15px;
  }
  
  .welcome-text h1 {
    font-size: 22px;
  }
  
  .resource-number,
  .stat-number {
    font-size: 20px;
  }
}
</style>