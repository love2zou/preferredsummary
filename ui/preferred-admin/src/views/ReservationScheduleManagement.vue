<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>教练排班管理</h2>
            <p>维护预约页可选的日期和时间段</p>
          </div>
          <el-button type="primary" @click="handleAdd">新增排班</el-button>
        </div>
      </template>

      <div class="toolbar toolbar--grid">
        <el-select v-model="searchForm.trainerProfileId" placeholder="筛选教练" clearable filterable>
          <el-option v-for="item in trainerOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.clubId" placeholder="筛选门店" clearable filterable>
          <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-date-picker v-model="searchForm.scheduleDate" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" />
        <el-select v-model="searchForm.isAvailable" placeholder="可预约状态" clearable>
          <el-option label="可预约" :value="true" />
          <el-option label="不可预约" :value="false" />
        </el-select>
        <div class="toolbar__actions">
          <el-button type="primary" @click="handleSearch">搜索</el-button>
          <el-button @click="handleReset">重置</el-button>
        </div>
      </div>

      <el-table :data="scheduleList" v-loading="loading" border stripe height="calc(100vh - 320px)">
        <el-table-column prop="trainerName" label="教练" min-width="140" />
        <el-table-column prop="clubName" label="门店" min-width="140" />
        <el-table-column prop="scheduleDate" label="日期" width="120" />
        <el-table-column prop="startTime" label="开始时间" width="100" />
        <el-table-column prop="endTime" label="结束时间" width="100" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isAvailable ? 'success' : 'info'">{{ row.isAvailable ? '可预约' : '不可约' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="handleEdit(row.id)">编辑</el-button>
            <el-button link type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-model:current-page="pagination.page"
        v-model:page-size="pagination.pageSize"
        :total="pagination.total"
        layout="total, sizes, prev, pager, next, jumper"
        :page-sizes="[10, 20, 50]"
        class="pager"
        @current-change="loadData"
        @size-change="handleSizeChange"
      />
    </el-card>

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑排班' : '新增排班'" width="640px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="所属教练" prop="trainerProfileId">
          <el-select v-model="form.trainerProfileId" filterable>
            <el-option v-for="item in trainerOptions" :key="item.id" :label="item.label" :value="item.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="所属门店" prop="clubId">
          <el-select v-model="form.clubId" filterable>
            <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="排班日期" prop="scheduleDate">
          <el-date-picker v-model="form.scheduleDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="开始时间" prop="startTime">
          <el-time-picker v-model="startClock" format="HH:mm" value-format="HH:mm" style="width: 100%" @change="syncTimeFields" />
        </el-form-item>
        <el-form-item label="结束时间" prop="endTime">
          <el-time-picker v-model="endClock" format="HH:mm" value-format="HH:mm" style="width: 100%" @change="syncTimeFields" />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="form.seqNo" :min="0" :max="9999" style="width: 100%" />
        </el-form-item>
        <el-form-item label="是否可预约">
          <el-switch v-model="form.isAvailable" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import reservationAdminApi, {
  type LookupOption,
  type ReservationScheduleSlot,
  type ReservationScheduleSlotEditParams
} from '@/api/reservationAdmin'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const trainerOptions = ref<LookupOption[]>([])
const clubOptions = ref<LookupOption[]>([])
const scheduleList = ref<ReservationScheduleSlot[]>([])
const startClock = ref('09:00')
const endClock = ref('10:00')

const searchForm = reactive<{
  trainerProfileId?: number
  clubId?: number
  scheduleDate?: string
  isAvailable?: boolean
}>({
  trainerProfileId: undefined,
  clubId: undefined,
  scheduleDate: undefined,
  isAvailable: undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const createDefaultForm = (): ReservationScheduleSlotEditParams => ({
  trainerProfileId: 0,
  clubId: 0,
  scheduleDate: '',
  startTime: '09:00',
  endTime: '10:00',
  isAvailable: true,
  seqNo: 0
})

const form = reactive<ReservationScheduleSlotEditParams>(createDefaultForm())

const rules: FormRules = {
  trainerProfileId: [{ required: true, message: '请选择教练', trigger: 'change' }],
  clubId: [{ required: true, message: '请选择门店', trigger: 'change' }],
  scheduleDate: [{ required: true, message: '请选择日期', trigger: 'change' }],
  startTime: [{ required: true, message: '请选择开始时间', trigger: 'change' }],
  endTime: [{ required: true, message: '请选择结束时间', trigger: 'change' }]
}

const syncTimeFields = (): void => {
  form.startTime = startClock.value
  form.endTime = endClock.value
}

const loadLookups = async (): Promise<void> => {
  const [trainers, clubs] = await Promise.all([
    reservationAdminApi.getTrainerOptions(),
    reservationAdminApi.getClubOptions()
  ])
  trainerOptions.value = trainers
  clubOptions.value = clubs
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getScheduleList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      trainerProfileId: searchForm.trainerProfileId,
      clubId: searchForm.clubId,
      scheduleDate: searchForm.scheduleDate,
      isAvailable: searchForm.isAvailable
    })
    scheduleList.value = response.data
    pagination.total = response.total
  } finally {
    loading.value = false
  }
}

const resetForm = (): void => {
  Object.assign(form, createDefaultForm())
  startClock.value = '09:00'
  endClock.value = '10:00'
  formRef.value?.clearValidate()
}

const handleSearch = (): void => {
  pagination.page = 1
  void loadData()
}

const handleReset = (): void => {
  searchForm.trainerProfileId = undefined
  searchForm.clubId = undefined
  searchForm.scheduleDate = undefined
  searchForm.isAvailable = undefined
  pagination.page = 1
  void loadData()
}

const handleSizeChange = (): void => {
  pagination.page = 1
  void loadData()
}

const handleAdd = (): void => {
  isEdit.value = false
  editingId.value = null
  resetForm()
  dialogVisible.value = true
}

const handleEdit = async (id: number): Promise<void> => {
  const detail = await reservationAdminApi.getScheduleDetail(id)
  isEdit.value = true
  editingId.value = id
  Object.assign(form, {
    trainerProfileId: detail.trainerProfileId,
    clubId: detail.clubId,
    scheduleDate: detail.scheduleDate,
    startTime: detail.startTime,
    endTime: detail.endTime,
    isAvailable: detail.isAvailable,
    seqNo: detail.seqNo
  })
  startClock.value = detail.startTime
  endClock.value = detail.endTime
  dialogVisible.value = true
}

const handleSubmit = async (): Promise<void> => {
  if (!formRef.value) return
  syncTimeFields()
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  submitting.value = true
  try {
    const response = isEdit.value && editingId.value
      ? await reservationAdminApi.updateSchedule(editingId.value, form)
      : await reservationAdminApi.createSchedule(form)

    if (!response.success) {
      ElMessage.error(response.message || '保存失败')
      return
    }

    ElMessage.success(response.message || '保存成功')
    dialogVisible.value = false
    void loadData()
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (row: ReservationScheduleSlot): Promise<void> => {
  await ElMessageBox.confirm(`确定删除排班 ${row.scheduleDate} ${row.startTime}-${row.endTime} 吗？`, '删除确认', { type: 'warning' })
  const response = await reservationAdminApi.deleteSchedule(row.id)
  if (!response.success) {
    ElMessage.error(response.message || '删除失败')
    return
  }
  ElMessage.success(response.message || '删除成功')
  void loadData()
}

onMounted(() => {
  void Promise.all([loadLookups(), loadData()])
})
</script>

<style scoped>
.page-container {
  padding: 20px;
  height: calc(100vh - 60px);
  overflow-y: auto;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.card-header h2,
.card-header p {
  margin: 0;
}

.card-header p {
  color: #909399;
  font-size: 13px;
  margin-top: 6px;
}

.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.toolbar--grid > * {
  flex: 1;
}

.toolbar__actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
