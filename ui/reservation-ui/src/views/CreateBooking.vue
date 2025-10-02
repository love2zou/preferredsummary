<template>
  <div class="page">
    <div class="navbar">
      <span class="navbar-back" @click="$router.go(-1)">
        <i class="fas fa-arrow-left"></i> 返回
      </span>
      <span class="navbar-title">预约课程</span>
      <span></span>
    </div>

    <div class="booking-container">
      <!-- 日期选择 -->
      <div class="card">
        <div class="card-header">
          <i class="fas fa-calendar-alt"></i>
          选择日期
        </div>
        <div class="card-body">
          <el-date-picker
            v-model="selectedDate"
            type="date"
            placeholder="选择日期"
            size="large"
            style="width: 100%"
            :disabled-date="disabledDate"
            @change="handleDateChange"
          />
        </div>
      </div>

      <!-- 时间段选择 -->
      <div class="card" v-if="selectedDate">
        <div class="card-header">
          <i class="fas fa-clock"></i>
          选择时间段
        </div>
        <div class="card-body">
          <div v-if="loading" class="loading-state">
            <el-skeleton :rows="3" animated />
          </div>
          <div v-else-if="availableSlots.length === 0" class="empty-state">
            <i class="fas fa-calendar-times"></i>
            <p>该日期暂无可用时间段</p>
          </div>
          <div v-else class="time-picker">
            <div
              v-for="slot in availableSlots"
              :key="slot.id"
              class="time-slot"
              :class="{
                selected: selectedSlot?.id === slot.id,
                disabled: !slot.available
              }"
              @click="selectTimeSlot(slot)"
            >
              <div class="slot-time">{{ slot.time }}</div>
              <div v-if="slot.available && slot.trainerName" class="slot-trainer">
                {{ slot.trainerName }}
              </div>
              <div v-else-if="!slot.available" class="slot-status">
                已预约
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 预约信息确认 -->
      <div class="card" v-if="selectedSlot">
        <div class="card-header">
          <i class="fas fa-info-circle"></i>
          预约信息
        </div>
        <div class="card-body">
          <div class="booking-summary">
            <div class="summary-item">
              <span class="label">日期：</span>
              <span class="value">{{ formatDate(selectedDate) }}</span>
            </div>
            <div class="summary-item">
              <span class="label">时间：</span>
              <span class="value">{{ selectedSlot.time }}</span>
            </div>
            <div class="summary-item">
              <span class="label">教练：</span>
              <span class="value">{{ selectedSlot.trainerName }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 提交按钮 -->
      <div class="submit-section" v-if="selectedSlot">
        <el-button
          type="primary"
          size="large"
          class="submit-btn"
          :loading="submitting"
          @click="handleSubmit"
        >
          确认预约
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useBookingStore, type TimeSlot } from '@/stores/booking'

const router = useRouter()
const bookingStore = useBookingStore()

const selectedDate = ref<Date | null>(null)
const selectedSlot = ref<TimeSlot | null>(null)
const loading = ref(false)
const submitting = ref(false)

const availableSlots = computed(() => bookingStore.availableSlots)

// 禁用过去的日期
const disabledDate = (date: Date) => {
  return date < new Date(new Date().setHours(0, 0, 0, 0))
}

// 格式化日期显示
const formatDate = (date: Date | null) => {
  if (!date) return ''
  return date.toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    weekday: 'long'
  })
}

// 日期改变时获取可用时间段
const handleDateChange = async (date: Date | null) => {
  if (!date) return
  
  selectedSlot.value = null
  loading.value = true
  
  try {
    const dateString = date.toISOString().split('T')[0]
    await bookingStore.fetchAvailableSlots(dateString)
  } catch (error) {
    ElMessage.error('获取时间段失败')
  } finally {
    loading.value = false
  }
}

// 选择时间段
const selectTimeSlot = (slot: TimeSlot) => {
  if (!slot.available) return
  selectedSlot.value = slot
}

// 提交预约
const handleSubmit = async () => {
  if (!selectedDate.value || !selectedSlot.value) return
  
  submitting.value = true
  
  try {
    const bookingData = {
      trainerId: selectedSlot.value.trainerId!,
      trainerName: selectedSlot.value.trainerName!,
      date: selectedDate.value.toISOString().split('T')[0],
      timeSlot: selectedSlot.value.time
    }
    
    await bookingStore.createBooking(bookingData)
    ElMessage.success('预约成功！')
    router.push('/booking')
    
  } catch (error) {
    ElMessage.error('预约失败，请重试')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.booking-container {
  padding: 16px;
  padding-bottom: 80px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.loading-state {
  padding: 16px 0;
}

.empty-state {
  text-align: center;
  padding: 32px 16px;
  color: var(--text-secondary);
}

.empty-state i {
  font-size: 48px;
  margin-bottom: 16px;
  display: block;
}

.time-picker {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.time-slot {
  padding: 16px 12px;
  text-align: center;
  border: 2px solid var(--border-color);
  border-radius: var(--border-radius);
  cursor: pointer;
  transition: all 0.3s ease;
  background: var(--white);
}

.time-slot:hover:not(.disabled) {
  border-color: var(--primary-color);
  transform: translateY(-2px);
}

.time-slot.selected {
  background-color: var(--primary-color);
  color: var(--white);
  border-color: var(--primary-color);
}

.time-slot.disabled {
  background-color: #f5f5f5;
  color: var(--text-secondary);
  cursor: not-allowed;
  border-color: #e0e0e0;
}

.slot-time {
  font-weight: 600;
  font-size: 16px;
  margin-bottom: 4px;
}

.slot-trainer {
  font-size: 12px;
  opacity: 0.8;
}

.slot-status {
  font-size: 12px;
  color: var(--text-secondary);
}

.booking-summary {
  space-y: 12px;
}

.summary-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--border-color);
}

.summary-item:last-child {
  border-bottom: none;
}

.label {
  color: var(--text-secondary);
  font-weight: 500;
}

.value {
  font-weight: 600;
  color: var(--text-color);
}

.submit-section {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 16px;
  background: var(--white);
  border-top: 1px solid var(--border-color);
}

.submit-btn {
  width: 100%;
  height: 48px;
  font-size: 16px;
  font-weight: 600;
}

@media (max-width: 768px) {
  .time-picker {
    grid-template-columns: 1fr;
  }
}
</style>