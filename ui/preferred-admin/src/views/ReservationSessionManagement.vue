<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>课程类型管理</h2>
            <p>维护每位教练可售卖的预约课程类型</p>
          </div>
          <el-button type="primary" @click="handleAdd">新增课程类型</el-button>
        </div>
      </template>

      <div class="toolbar">
        <el-select v-model="searchForm.trainerProfileId" placeholder="筛选教练" clearable filterable>
          <el-option v-for="item in trainerOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-input v-model="searchForm.keyword" placeholder="搜索课程编码 / 名称" clearable @keyup.enter="handleSearch" />
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="handleReset">重置</el-button>
      </div>

      <el-table :data="sessionList" v-loading="loading" border stripe height="calc(100vh - 300px)">
        <el-table-column prop="trainerName" label="教练" min-width="140" />
        <el-table-column prop="sessionCode" label="课程编码" min-width="120" />
        <el-table-column prop="sessionName" label="课程名称" min-width="160" />
        <el-table-column prop="durationMinutes" label="时长(分钟)" width="110" />
        <el-table-column prop="price" label="价格" width="100" />
        <el-table-column label="启用" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'">{{ row.isActive ? '启用' : '停用' }}</el-tag>
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

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑课程类型' : '新增课程类型'" width="640px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="所属教练" prop="trainerProfileId">
          <el-select v-model="form.trainerProfileId" filterable>
            <el-option v-for="item in trainerOptions" :key="item.id" :label="item.label" :value="item.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="课程编码" prop="sessionCode">
          <el-input v-model="form.sessionCode" />
        </el-form-item>
        <el-form-item label="课程名称" prop="sessionName">
          <el-input v-model="form.sessionName" />
        </el-form-item>
        <el-form-item label="课程说明">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="时长(分钟)">
          <el-input-number v-model="form.durationMinutes" :min="30" :max="240" :step="30" style="width: 100%" />
        </el-form-item>
        <el-form-item label="课程价格">
          <el-input-number v-model="form.price" :min="0" :precision="2" style="width: 100%" />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="form.seqNo" :min="0" :max="9999" style="width: 100%" />
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="form.isActive" />
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
  type ReservationSessionType,
  type ReservationSessionTypeEditParams
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
const sessionList = ref<ReservationSessionType[]>([])

const searchForm = reactive<{
  trainerProfileId?: number
  keyword: string
}>({
  trainerProfileId: undefined,
  keyword: ''
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const createDefaultForm = (): ReservationSessionTypeEditParams => ({
  trainerProfileId: 0,
  sessionCode: '',
  sessionName: '',
  description: '',
  durationMinutes: 60,
  price: 0,
  isActive: true,
  seqNo: 0
})

const form = reactive<ReservationSessionTypeEditParams>(createDefaultForm())
const rules: FormRules = {
  trainerProfileId: [{ required: true, message: '请选择教练', trigger: 'change' }],
  sessionCode: [{ required: true, message: '请输入课程编码', trigger: 'blur' }],
  sessionName: [{ required: true, message: '请输入课程名称', trigger: 'blur' }]
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getSessionList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      trainerProfileId: searchForm.trainerProfileId,
      keyword: searchForm.keyword.trim()
    })
    sessionList.value = response.data
    pagination.total = response.total
  } finally {
    loading.value = false
  }
}

const loadTrainerOptions = async (): Promise<void> => {
  trainerOptions.value = await reservationAdminApi.getTrainerOptions()
}

const resetForm = (): void => {
  Object.assign(form, createDefaultForm())
  formRef.value?.clearValidate()
}

const handleSearch = (): void => {
  pagination.page = 1
  void loadData()
}

const handleReset = (): void => {
  searchForm.trainerProfileId = undefined
  searchForm.keyword = ''
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
  const detail = await reservationAdminApi.getSessionDetail(id)
  isEdit.value = true
  editingId.value = id
  Object.assign(form, {
    trainerProfileId: detail.trainerProfileId,
    sessionCode: detail.sessionCode,
    sessionName: detail.sessionName,
    description: detail.description ?? '',
    durationMinutes: detail.durationMinutes,
    price: detail.price,
    isActive: detail.isActive,
    seqNo: detail.seqNo
  })
  dialogVisible.value = true
}

const handleSubmit = async (): Promise<void> => {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  submitting.value = true
  try {
    const response = isEdit.value && editingId.value
      ? await reservationAdminApi.updateSession(editingId.value, form)
      : await reservationAdminApi.createSession(form)

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

const handleDelete = async (row: ReservationSessionType): Promise<void> => {
  await ElMessageBox.confirm(`确定删除课程“${row.sessionName}”吗？`, '删除确认', { type: 'warning' })
  const response = await reservationAdminApi.deleteSession(row.id)
  if (!response.success) {
    ElMessage.error(response.message || '删除失败')
    return
  }
  ElMessage.success(response.message || '删除成功')
  void loadData()
}

onMounted(() => {
  void Promise.all([loadTrainerOptions(), loadData()])
})
</script>

<style scoped>
.page-container {
  padding: 20px;
  height: calc(100vh - 60px);
  overflow-y: auto;
}

.card-header,
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
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
  margin-bottom: 16px;
}

.toolbar > *:first-child,
.toolbar > *:nth-child(2) {
  flex: 1;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
