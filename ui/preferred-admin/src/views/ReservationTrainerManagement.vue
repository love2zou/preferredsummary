<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>预约教练管理</h2>
            <p>维护预约 APP 的教练档案、标签和展示信息</p>
          </div>
          <el-button type="primary" @click="handleAdd">新增教练</el-button>
        </div>
      </template>

      <div class="toolbar toolbar--grid">
        <el-input v-model="searchForm.keyword" placeholder="搜索教练名称 / 头衔 / 亮点" clearable @keyup.enter="handleSearch" />
        <el-select v-model="searchForm.clubId" placeholder="筛选门店" clearable>
          <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.isActive" placeholder="启用状态" clearable>
          <el-option label="启用" :value="true" />
          <el-option label="停用" :value="false" />
        </el-select>
        <div class="toolbar__actions">
          <el-button type="primary" @click="handleSearch">搜索</el-button>
          <el-button @click="handleReset">重置</el-button>
        </div>
      </div>

      <el-table :data="trainerList" v-loading="loading" border stripe height="calc(100vh - 320px)">
        <el-table-column prop="displayName" label="教练名称" min-width="140" />
        <el-table-column prop="userName" label="绑定用户" min-width="140" />
        <el-table-column prop="clubName" label="所属门店" min-width="140" />
        <el-table-column prop="title" label="头衔" min-width="150" />
        <el-table-column prop="basePrice" label="课单价" width="100" />
        <el-table-column prop="goals" label="目标标签" min-width="160">
          <template #default="{ row }">{{ row.goals.join('、') }}</template>
        </el-table-column>
        <el-table-column label="推荐" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isRecommended ? 'success' : 'info'">{{ row.isRecommended ? '是' : '否' }}</el-tag>
          </template>
        </el-table-column>
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

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑教练' : '新增教练'" width="860px" :close-on-click-modal="false">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <div class="form-grid">
          <el-form-item label="绑定用户" prop="userId">
            <el-select v-model="form.userId" filterable placeholder="请选择用户">
              <el-option v-for="item in userOptions" :key="item.id" :label="item.label" :value="item.id" />
            </el-select>
          </el-form-item>
          <el-form-item label="所属门店" prop="clubId">
            <el-select v-model="form.clubId" filterable placeholder="请选择门店">
              <el-option v-for="item in clubOptions" :key="item.id" :label="item.label" :value="item.id" />
            </el-select>
          </el-form-item>
          <el-form-item label="展示名称" prop="displayName">
            <el-input v-model="form.displayName" />
          </el-form-item>
          <el-form-item label="头衔" prop="title">
            <el-input v-model="form.title" />
          </el-form-item>
          <el-form-item label="性别" prop="gender">
            <el-select v-model="form.gender">
              <el-option label="男" value="男" />
              <el-option label="女" value="女" />
            </el-select>
          </el-form-item>
          <el-form-item label="训练区域">
            <el-input v-model="form.trainingArea" />
          </el-form-item>
          <el-form-item label="从业年限">
            <el-input-number v-model="form.yearsOfExperience" :min="0" :max="50" style="width: 100%" />
          </el-form-item>
          <el-form-item label="课程单价">
            <el-input-number v-model="form.basePrice" :min="0" :precision="2" style="width: 100%" />
          </el-form-item>
          <el-form-item label="评分">
            <el-input-number v-model="form.rating" :min="0" :max="5" :step="0.1" :precision="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="评价数">
            <el-input-number v-model="form.reviewCount" :min="0" style="width: 100%" />
          </el-form-item>
          <el-form-item label="服务人数">
            <el-input-number v-model="form.servedClients" :min="0" style="width: 100%" />
          </el-form-item>
          <el-form-item label="好评率">
            <el-input-number v-model="form.satisfaction" :min="0" :max="100" style="width: 100%" />
          </el-form-item>
          <el-form-item label="亮点文案">
            <el-input v-model="form.highlight" />
          </el-form-item>
          <el-form-item label="主图地址">
            <el-input v-model="form.heroImageUrl" />
          </el-form-item>
          <el-form-item label="主色值">
            <el-input v-model="form.heroTone" placeholder="#eaf7ef" />
          </el-form-item>
          <el-form-item label="强调色">
            <el-input v-model="form.accentTone" placeholder="#16a34a" />
          </el-form-item>
          <el-form-item label="排序号">
            <el-input-number v-model="form.seqNo" :min="0" :max="9999" style="width: 100%" />
          </el-form-item>
          <el-form-item label="首页推荐">
            <el-switch v-model="form.isRecommended" />
          </el-form-item>
          <el-form-item label="是否启用">
            <el-switch v-model="form.isActive" />
          </el-form-item>
        </div>

        <el-form-item label="目标标签">
          <el-input v-model="goalText" type="textarea" :rows="2" placeholder="多个标签请用逗号分隔" />
        </el-form-item>
        <el-form-item label="擅长方向">
          <el-input v-model="specialtyText" type="textarea" :rows="2" placeholder="多个标签请用逗号分隔" />
        </el-form-item>
        <el-form-item label="徽章标签">
          <el-input v-model="badgeText" type="textarea" :rows="2" placeholder="多个标签请用逗号分隔" />
        </el-form-item>
        <el-form-item label="资质认证">
          <el-input v-model="certificationText" type="textarea" :rows="2" placeholder="多个标签请用逗号分隔" />
        </el-form-item>
        <el-form-item label="教练简介">
          <el-input v-model="form.introduction" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="补充介绍">
          <el-input v-model="form.story" type="textarea" :rows="3" />
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
  type ReservationTrainer,
  type ReservationTrainerEditParams
} from '@/api/reservationAdmin'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const clubOptions = ref<LookupOption[]>([])
const userOptions = ref<LookupOption[]>([])
const trainerList = ref<ReservationTrainer[]>([])

const searchForm = reactive<{
  keyword: string
  clubId?: number
  isActive?: boolean
}>({
  keyword: '',
  clubId: undefined,
  isActive: undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const createDefaultForm = (): ReservationTrainerEditParams => ({
  userId: 0,
  clubId: 0,
  displayName: '',
  title: '',
  gender: '男',
  yearsOfExperience: 0,
  rating: 4.8,
  reviewCount: 0,
  servedClients: 0,
  satisfaction: 95,
  basePrice: 0,
  trainingArea: '',
  highlight: '',
  introduction: '',
  story: '',
  heroImageUrl: '',
  heroTone: '#eaf7ef',
  accentTone: '#16a34a',
  isRecommended: false,
  isActive: true,
  seqNo: 0,
  goals: [],
  specialties: [],
  badges: [],
  certifications: []
})

const form = reactive<ReservationTrainerEditParams>(createDefaultForm())
const goalText = ref('')
const specialtyText = ref('')
const badgeText = ref('')
const certificationText = ref('')

const rules: FormRules = {
  userId: [{ required: true, message: '请选择绑定用户', trigger: 'change' }],
  clubId: [{ required: true, message: '请选择所属门店', trigger: 'change' }],
  displayName: [{ required: true, message: '请输入展示名称', trigger: 'blur' }],
  title: [{ required: true, message: '请输入头衔', trigger: 'blur' }],
  gender: [{ required: true, message: '请选择性别', trigger: 'change' }]
}

const normalizeTagList = (text: string): string[] =>
  text
    .split(/[,，\n]/)
    .map((item) => item.trim())
    .filter(Boolean)

const syncTagFieldsToForm = (): void => {
  form.goals = normalizeTagList(goalText.value)
  form.specialties = normalizeTagList(specialtyText.value)
  form.badges = normalizeTagList(badgeText.value)
  form.certifications = normalizeTagList(certificationText.value)
}

const syncTagFieldsFromDetail = (detail: ReservationTrainer): void => {
  goalText.value = detail.goals.join('，')
  specialtyText.value = detail.specialties.join('，')
  badgeText.value = detail.badges.join('，')
  certificationText.value = detail.certifications.join('，')
}

const loadLookups = async (): Promise<void> => {
  const [clubs, users] = await Promise.all([
    reservationAdminApi.getClubOptions(),
    reservationAdminApi.getUserOptions()
  ])
  clubOptions.value = clubs
  userOptions.value = users
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getTrainerList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: searchForm.keyword.trim(),
      clubId: searchForm.clubId,
      isActive: searchForm.isActive
    })
    trainerList.value = response.data
    pagination.total = response.total
  } finally {
    loading.value = false
  }
}

const handleSearch = (): void => {
  pagination.page = 1
  void loadData()
}

const handleReset = (): void => {
  searchForm.keyword = ''
  searchForm.clubId = undefined
  searchForm.isActive = undefined
  pagination.page = 1
  void loadData()
}

const handleSizeChange = (): void => {
  pagination.page = 1
  void loadData()
}

const resetForm = (): void => {
  Object.assign(form, createDefaultForm())
  goalText.value = ''
  specialtyText.value = ''
  badgeText.value = ''
  certificationText.value = ''
  formRef.value?.clearValidate()
}

const handleAdd = (): void => {
  isEdit.value = false
  editingId.value = null
  resetForm()
  dialogVisible.value = true
}

const handleEdit = async (id: number): Promise<void> => {
  const detail = await reservationAdminApi.getTrainerDetail(id)
  isEdit.value = true
  editingId.value = id
  Object.assign(form, {
    userId: detail.userId,
    clubId: detail.clubId,
    displayName: detail.displayName,
    title: detail.title,
    gender: detail.gender,
    yearsOfExperience: detail.yearsOfExperience,
    rating: detail.rating,
    reviewCount: detail.reviewCount,
    servedClients: detail.servedClients,
    satisfaction: detail.satisfaction,
    basePrice: detail.basePrice,
    trainingArea: detail.trainingArea ?? '',
    highlight: detail.highlight ?? '',
    introduction: detail.introduction ?? '',
    story: detail.story ?? '',
    heroImageUrl: detail.heroImageUrl ?? '',
    heroTone: detail.heroTone ?? '',
    accentTone: detail.accentTone ?? '',
    isRecommended: detail.isRecommended,
    isActive: detail.isActive,
    seqNo: detail.seqNo,
    goals: detail.goals,
    specialties: detail.specialties,
    badges: detail.badges,
    certifications: detail.certifications
  })
  syncTagFieldsFromDetail(detail)
  dialogVisible.value = true
}

const handleSubmit = async (): Promise<void> => {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  syncTagFieldsToForm()
  submitting.value = true
  try {
    const response = isEdit.value && editingId.value
      ? await reservationAdminApi.updateTrainer(editingId.value, form)
      : await reservationAdminApi.createTrainer(form)

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

const handleDelete = async (row: ReservationTrainer): Promise<void> => {
  await ElMessageBox.confirm(`确定删除教练“${row.displayName}”吗？`, '删除确认', { type: 'warning' })
  const response = await reservationAdminApi.deleteTrainer(row.id)
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

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 16px;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
