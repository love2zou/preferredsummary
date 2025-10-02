<template>
  <div class="page-container">
    <!-- 页面标题 -->
    <div class="page-header">
      <el-icon class="back-btn" @click="goBack"><ArrowLeft /></el-icon>
      <div class="page-title">预约课程</div>
      <div class="header-placeholder"></div>
    </div>

    <!-- 日期选择 -->
    <div class="mini-card">
      <div class="section-title">选择日期</div>
      <div class="date-selector">
        <div 
          v-for="date in availableDates" 
          :key="date.value"
          class="date-item"
          :class="{ active: selectedDate === date.value }"
          @click="selectedDate = date.value"
        >
          <div class="date-day">{{ date.day }}</div>
          <div class="date-date">{{ date.date }}</div>
        </div>
      </div>
    </div>

    <!-- 课程类型选择 -->
    <div class="mini-card">
      <div class="section-title">选择课程</div>
      <div class="course-types">
        <div 
          v-for="course in courseTypes" 
          :key="course.id"
          class="course-type-item"
          :class="{ active: selectedCourse === course.id }"
          @click="selectedCourse = course.id"
        >
          <div class="course-icon">
            <el-icon><Trophy /></el-icon>
          </div>
          <div class="course-info">
            <div class="course-name">{{ course.name }}</div>
            <div class="course-duration">{{ course.duration }}分钟</div>
          </div>
          <div class="course-price">¥{{ course.price }}</div>
        </div>
      </div>
    </div>

    <!-- 时间段选择 -->
    <div class="mini-card">
      <div class="section-title">选择时间</div>
      <div class="time-slots">
        <div 
          v-for="slot in timeSlots" 
          :key="slot.id"
          class="time-slot"
          :class="{ 
            active: selectedTimeSlot === slot.id,
            disabled: !slot.available 
          }"
          @click="selectTimeSlot(slot)"
        >
          <div class="slot-time">{{ slot.time }}</div>
          <div class="slot-trainer">{{ slot.trainer }}</div>
          <div class="slot-status">
            <span v-if="slot.available" class="available">可预约</span>
            <span v-else class="unavailable">已满</span>
          </div>
        </div>
      </div>
    </div>

    <!-- 预约信息确认 -->
    <div v-if="selectedTimeSlot" class="mini-card booking-summary">
      <div class="section-title">预约信息</div>
      <div class="summary-content">
        <div class="summary-item">
          <span class="label">日期：</span>
          <span class="value">{{ getSelectedDateText() }}</span>
        </div>
        <div class="summary-item">
          <span class="label">课程：</span>
          <span class="value">{{ getSelectedCourseText() }}</span>
        </div>
        <div class="summary-item">
          <span class="label">时间：</span>
          <span class="value">{{ getSelectedTimeSlotText() }}</span>
        </div>
        <div class="summary-item">
          <span class="label">教练：</span>
          <span class="value">{{ getSelectedTrainerText() }}</span>
        </div>
        <div class="summary-item total">
          <span class="label">费用：</span>
          <span class="value price">¥{{ getSelectedCoursePrice() }}</span>
        </div>
      </div>
    </div>

    <!-- 预约按钮 -->
    <div class="booking-actions">
      <button 
        class="mini-button primary booking-btn"
        :disabled="!canBook || loading"
        @click="handleBooking"
      >
        <span v-if="loading">预约中...</span>
        <span v-else>确认预约</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, Trophy } from '@element-plus/icons-vue'

const router = useRouter()
const loading = ref(false)

// 选择的数据
const selectedDate = ref('')
const selectedCourse = ref<number | null>(null)
const selectedTimeSlot = ref<number | null>(null)

// 可选日期（接下来7天）
const availableDates = ref([
  { value: '2024-01-15', day: '今天', date: '1/15' },
  { value: '2024-01-16', day: '明天', date: '1/16' },
  { value: '2024-01-17', day: '周三', date: '1/17' },
  { value: '2024-01-18', day: '周四', date: '1/18' },
  { value: '2024-01-19', day: '周五', date: '1/19' },
  { value: '2024-01-20', day: '周六', date: '1/20' },
  { value: '2024-01-21', day: '周日', date: '1/21' }
])

// 课程类型
const courseTypes = ref([
  { id: 1, name: '力量训练', duration: 60, price: 120 },
  { id: 2, name: '有氧运动', duration: 45, price: 100 },
  { id: 3, name: '瑜伽课程', duration: 75, price: 150 },
  { id: 4, name: '普拉提', duration: 60, price: 130 },
  { id: 5, name: '功能性训练', duration: 50, price: 110 }
])

// 时间段
const timeSlots = ref([
  { id: 1, time: '09:00-10:00', trainer: '李教练', available: true },
  { id: 2, time: '10:00-11:00', trainer: '王教练', available: true },
  { id: 3, time: '11:00-12:00', trainer: '张教练', available: false },
  { id: 4, time: '14:00-15:00', trainer: '李教练', available: true },
  { id: 5, time: '15:00-16:00', trainer: '赵教练', available: true },
  { id: 6, time: '16:00-17:00', trainer: '王教练', available: false },
  { id: 7, time: '17:00-18:00', trainer: '陈教练', available: true },
  { id: 8, time: '18:00-19:00', trainer: '李教练', available: true },
  { id: 9, time: '19:00-20:00', trainer: '张教练', available: true }
])

// 计算属性
const canBook = computed(() => {
  return selectedDate.value && selectedCourse.value && selectedTimeSlot.value
})

// 方法
const goBack = () => {
  router.go(-1)
}

const selectTimeSlot = (slot: any) => {
  if (!slot.available) {
    ElMessage.warning('该时间段已满，请选择其他时间')
    return
  }
  selectedTimeSlot.value = slot.id
}

const getSelectedDateText = () => {
  const date = availableDates.value.find(d => d.value === selectedDate.value)
  return date ? `${date.day} ${date.date}` : ''
}

const getSelectedCourseText = () => {
  const course = courseTypes.value.find(c => c.id === selectedCourse.value)
  return course ? course.name : ''
}

const getSelectedTimeSlotText = () => {
  const slot = timeSlots.value.find(s => s.id === selectedTimeSlot.value)
  return slot ? slot.time : ''
}

const getSelectedTrainerText = () => {
  const slot = timeSlots.value.find(s => s.id === selectedTimeSlot.value)
  return slot ? slot.trainer : ''
}

const getSelectedCoursePrice = () => {
  const course = courseTypes.value.find(c => c.id === selectedCourse.value)
  return course ? course.price : 0
}

const handleBooking = async () => {
  if (!canBook.value) {
    ElMessage.warning('请完善预约信息')
    return
  }

  loading.value = true
  try {
    // 模拟预约API调用
    await new Promise(resolve => setTimeout(resolve, 2000))
    
    ElMessage.success('预约成功！')
    router.push('/member/home')
  } catch (error) {
    ElMessage.error('预约失败，请重试')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  // 默认选择今天
  selectedDate.value = availableDates.value[0].value
})
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  background: var(--card-bg);
  border-bottom: 1px solid var(--border-color);
}

.back-btn {
  font-size: 20px;
  cursor: pointer;
  padding: 8px;
  border-radius: 50%;
  transition: background-color 0.3s;
}

.back-btn:hover {
  background: var(--bg-color);
}

.header-placeholder {
  width: 36px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-color);
  padding: 16px 16px 12px;
}

.date-selector {
  display: flex;
  gap: 8px;
  padding: 0 16px 16px;
  overflow-x: auto;
}

.date-item {
  min-width: 60px;
  text-align: center;
  padding: 12px 8px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
}

.date-item.active {
  border-color: var(--primary-color);
  background: #f0f9ff;
}

.date-day {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.date-date {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-color);
}

.date-item.active .date-day,
.date-item.active .date-date {
  color: var(--primary-color);
}

.course-types {
  padding: 0 16px 16px;
}

.course-type-item {
  display: flex;
  align-items: center;
  padding: 16px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.3s;
}

.course-type-item.active {
  border-color: var(--primary-color);
  background: #f0f9ff;
}

.course-icon {
  font-size: 24px;
  color: var(--primary-color);
  margin-right: 12px;
}

.course-info {
  flex: 1;
}

.course-name {
  font-size: 16px;
  font-weight: 500;
  color: var(--text-color);
  margin-bottom: 4px;
}

.course-duration {
  font-size: 14px;
  color: var(--text-secondary);
}

.course-price {
  font-size: 18px;
  font-weight: 600;
  color: var(--primary-color);
}

.time-slots {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
  padding: 0 16px 16px;
}

.time-slot {
  padding: 16px 12px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s;
}

.time-slot.active {
  border-color: var(--primary-color);
  background: #f0f9ff;
}

.time-slot.disabled {
  opacity: 0.5;
  cursor: not-allowed;
  background: #f5f5f5;
}

.slot-time {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-color);
  margin-bottom: 4px;
}

.slot-trainer {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.slot-status {
  font-size: 12px;
}

.available {
  color: var(--primary-color);
}

.unavailable {
  color: var(--danger-color);
}

.booking-summary {
  margin-bottom: 80px;
}

.summary-content {
  padding: 0 16px 16px;
}

.summary-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0;
  border-bottom: 1px solid var(--border-color);
}

.summary-item:last-child {
  border-bottom: none;
}

.summary-item.total {
  padding-top: 16px;
  margin-top: 8px;
  border-top: 2px solid var(--border-color);
}

.label {
  font-size: 14px;
  color: var(--text-secondary);
}

.value {
  font-size: 14px;
  color: var(--text-color);
  font-weight: 500;
}

.value.price {
  font-size: 18px;
  color: var(--primary-color);
  font-weight: 600;
}

.booking-actions {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 16px;
  background: var(--card-bg);
  border-top: 1px solid var(--border-color);
}

.booking-btn {
  width: 100%;
  height: 48px;
  font-size: 16px;
}

.booking-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>