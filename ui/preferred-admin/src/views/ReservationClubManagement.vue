<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>预约门店管理</h2>
            <p>维护预约 APP 使用的门店与会所基础信息</p>
          </div>
          <el-button type="primary" @click="handleAdd">新增门店</el-button>
        </div>
      </template>

      <div class="toolbar">
        <el-input v-model="searchForm.keyword" placeholder="搜索门店编码 / 名称 / 城市" clearable @keyup.enter="handleSearch" />
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="handleReset">重置</el-button>
      </div>

      <el-table :data="clubList" v-loading="loading" border stripe height="calc(100vh - 300px)">
        <el-table-column prop="clubCode" label="门店编码" min-width="120" />
        <el-table-column prop="clubName" label="门店名称" min-width="180" />
        <el-table-column prop="city" label="城市" width="110" />
        <el-table-column prop="district" label="区域" width="110" />
        <el-table-column prop="businessHours" label="营业时间" min-width="140" />
        <el-table-column label="启用状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'">{{ row.isActive ? '启用' : '停用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="seqNo" label="排序" width="80" />
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

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑门店' : '新增门店'" width="640px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="门店编码" prop="clubCode">
          <el-input v-model="form.clubCode" :disabled="isEdit" />
        </el-form-item>
        <el-form-item label="门店名称" prop="clubName">
          <el-input v-model="form.clubName" />
        </el-form-item>
        <el-form-item label="城市" prop="city">
          <el-input v-model="form.city" />
        </el-form-item>
        <el-form-item label="区域">
          <el-input v-model="form.district" />
        </el-form-item>
        <el-form-item label="门店地址">
          <el-input v-model="form.address" />
        </el-form-item>
        <el-form-item label="营业时间">
          <el-input v-model="form.businessHours" placeholder="例如 06:00-23:00" />
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
import reservationAdminApi, { type ReservationClub, type ReservationClubEditParams } from '@/api/reservationAdmin'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const searchForm = reactive({
  keyword: ''
})

const clubList = ref<ReservationClub[]>([])
const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const createDefaultForm = (): ReservationClubEditParams => ({
  clubCode: '',
  clubName: '',
  city: '',
  district: '',
  address: '',
  businessHours: '',
  isActive: true,
  seqNo: 0
})

const form = reactive<ReservationClubEditParams>(createDefaultForm())

const rules: FormRules = {
  clubCode: [{ required: true, message: '请输入门店编码', trigger: 'blur' }],
  clubName: [{ required: true, message: '请输入门店名称', trigger: 'blur' }],
  city: [{ required: true, message: '请输入城市', trigger: 'blur' }]
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getClubList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: searchForm.keyword.trim()
    })
    clubList.value = response.data
    pagination.total = response.total
  } finally {
    loading.value = false
  }
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
  const detail = await reservationAdminApi.getClubDetail(id)
  isEdit.value = true
  editingId.value = id
  Object.assign(form, {
    clubCode: detail.clubCode,
    clubName: detail.clubName,
    city: detail.city,
    district: detail.district ?? '',
    address: detail.address ?? '',
    businessHours: detail.businessHours ?? '',
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
      ? await reservationAdminApi.updateClub(editingId.value, form)
      : await reservationAdminApi.createClub(form)

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

const handleDelete = async (row: ReservationClub): Promise<void> => {
  await ElMessageBox.confirm(`确定删除门店“${row.clubName}”吗？`, '删除确认', { type: 'warning' })
  const response = await reservationAdminApi.deleteClub(row.id)
  if (!response.success) {
    ElMessage.error(response.message || '删除失败')
    return
  }
  ElMessage.success(response.message || '删除成功')
  void loadData()
}

onMounted(() => {
  void loadData()
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

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
