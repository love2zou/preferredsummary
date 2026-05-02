<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>会员课包管理</h2>
            <p>维护会员剩余课时、有效期和所属门店</p>
          </div>
          <el-button type="primary" @click="handleAdd">新增课包</el-button>
        </div>
      </template>

      <div class="toolbar toolbar--grid">
        <el-select v-model="searchForm.memberId" placeholder="筛选会员" clearable filterable>
          <el-option v-for="item in userOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.clubId" placeholder="筛选门店" clearable filterable>
          <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.statusCode" placeholder="状态" clearable>
          <el-option label="Active" value="Active" />
          <el-option label="Expired" value="Expired" />
          <el-option label="Frozen" value="Frozen" />
        </el-select>
        <div class="toolbar__actions">
          <el-button type="primary" @click="handleSearch">搜索</el-button>
          <el-button @click="handleReset">重置</el-button>
        </div>
      </div>

      <el-table :data="packageList" v-loading="loading" border stripe height="calc(100vh - 320px)">
        <el-table-column prop="memberName" label="会员" min-width="140" />
        <el-table-column prop="clubName" label="门店" min-width="140" />
        <el-table-column prop="packageName" label="课包名称" min-width="160" />
        <el-table-column prop="membershipName" label="会员类型" min-width="120" />
        <el-table-column label="课时" width="120">
          <template #default="{ row }">{{ row.remainingSessions }} / {{ row.totalSessions }}</template>
        </el-table-column>
        <el-table-column prop="effectiveDate" label="生效日期" width="120" />
        <el-table-column prop="expireDate" label="失效日期" width="120" />
        <el-table-column prop="statusCode" label="状态" width="100" />
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

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑课包' : '新增课包'" width="680px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="会员" prop="memberId">
          <el-select v-model="form.memberId" filterable>
            <el-option v-for="item in userOptions" :key="item.id" :label="item.label" :value="item.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="所属门店" prop="clubId">
          <el-select v-model="form.clubId" filterable>
            <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="课包名称" prop="packageName">
          <el-input v-model="form.packageName" />
        </el-form-item>
        <el-form-item label="会员类型">
          <el-input v-model="form.membershipName" />
        </el-form-item>
        <el-form-item label="总课时">
          <el-input-number v-model="form.totalSessions" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="剩余课时">
          <el-input-number v-model="form.remainingSessions" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="生效日期" prop="effectiveDate">
          <el-date-picker v-model="form.effectiveDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="失效日期" prop="expireDate">
          <el-date-picker v-model="form.expireDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="form.statusCode">
            <el-option label="Active" value="Active" />
            <el-option label="Expired" value="Expired" />
            <el-option label="Frozen" value="Frozen" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="form.seqNo" :min="0" :max="9999" style="width: 100%" />
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
  type ReservationMemberPackage,
  type ReservationMemberPackageEditParams
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
const userOptions = ref<LookupOption[]>([])
const clubOptions = ref<LookupOption[]>([])
const packageList = ref<ReservationMemberPackage[]>([])

const searchForm = reactive<{
  memberId?: number
  clubId?: number
  statusCode?: string
}>({
  memberId: undefined,
  clubId: undefined,
  statusCode: undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const createDefaultForm = (): ReservationMemberPackageEditParams => ({
  memberId: 0,
  clubId: 0,
  packageName: '',
  membershipName: '',
  totalSessions: 0,
  remainingSessions: 0,
  effectiveDate: '',
  expireDate: '',
  statusCode: 'Active',
  seqNo: 0
})

const form = reactive<ReservationMemberPackageEditParams>(createDefaultForm())

const rules: FormRules = {
  memberId: [{ required: true, message: '请选择会员', trigger: 'change' }],
  clubId: [{ required: true, message: '请选择门店', trigger: 'change' }],
  packageName: [{ required: true, message: '请输入课包名称', trigger: 'blur' }],
  effectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
  expireDate: [{ required: true, message: '请选择失效日期', trigger: 'change' }]
}

const loadLookups = async (): Promise<void> => {
  const [users, clubs] = await Promise.all([
    reservationAdminApi.getUserOptions(),
    reservationAdminApi.getClubOptions()
  ])
  userOptions.value = users
  clubOptions.value = clubs
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getPackageList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      memberId: searchForm.memberId,
      clubId: searchForm.clubId,
      statusCode: searchForm.statusCode
    })
    packageList.value = response.data
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
  searchForm.memberId = undefined
  searchForm.clubId = undefined
  searchForm.statusCode = undefined
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
  const detail = await reservationAdminApi.getPackageDetail(id)
  isEdit.value = true
  editingId.value = id
  Object.assign(form, {
    memberId: detail.memberId,
    clubId: detail.clubId,
    packageName: detail.packageName,
    membershipName: detail.membershipName ?? '',
    totalSessions: detail.totalSessions,
    remainingSessions: detail.remainingSessions,
    effectiveDate: detail.effectiveDate,
    expireDate: detail.expireDate,
    statusCode: detail.statusCode,
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
      ? await reservationAdminApi.updatePackage(editingId.value, form)
      : await reservationAdminApi.createPackage(form)

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

const handleDelete = async (row: ReservationMemberPackage): Promise<void> => {
  await ElMessageBox.confirm(`确定删除课包“${row.packageName}”吗？`, '删除确认', { type: 'warning' })
  const response = await reservationAdminApi.deletePackage(row.id)
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
