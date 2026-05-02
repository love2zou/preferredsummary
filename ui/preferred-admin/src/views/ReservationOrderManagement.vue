<template>
  <div class="page-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <div>
            <h2>预约订单管理</h2>
            <p>查看预约单，并支持人工更新上课状态</p>
          </div>
        </div>
      </template>

      <div class="toolbar toolbar--grid">
        <el-select v-model="searchForm.memberId" placeholder="筛选会员" clearable filterable>
          <el-option v-for="item in userOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.trainerProfileId" placeholder="筛选教练" clearable filterable>
          <el-option v-for="item in trainerOptions" :key="item.id" :label="item.label" :value="item.id" />
        </el-select>
        <el-select v-model="searchForm.statusCode" placeholder="订单状态" clearable>
          <el-option label="Upcoming" value="Upcoming" />
          <el-option label="Completed" value="Completed" />
          <el-option label="Cancelled" value="Cancelled" />
        </el-select>
        <div class="toolbar__actions">
          <el-button type="primary" @click="handleSearch">搜索</el-button>
          <el-button @click="handleReset">重置</el-button>
        </div>
      </div>

      <el-table :data="orderList" v-loading="loading" border stripe height="calc(100vh - 300px)">
        <el-table-column prop="reservationNo" label="预约单号" min-width="180" />
        <el-table-column prop="memberName" label="会员" min-width="120" />
        <el-table-column prop="trainerName" label="教练" min-width="120" />
        <el-table-column prop="clubName" label="门店" min-width="120" />
        <el-table-column prop="sessionName" label="课程" min-width="140" />
        <el-table-column label="预约时间" min-width="170">
          <template #default="{ row }">{{ row.reservationDate }} {{ row.startTime }}-{{ row.endTime }}</template>
        </el-table-column>
        <el-table-column prop="priceAmount" label="金额" width="100" />
        <el-table-column prop="statusCode" label="状态" width="110" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <template v-if="row.statusCode === 'Upcoming'">
              <el-button link type="success" @click="updateStatus(row.id, 'Completed')">标记完成</el-button>
              <el-button link type="danger" @click="updateStatus(row.id, 'Cancelled')">取消预约</el-button>
            </template>
            <span v-else class="muted-text">无可用操作</span>
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
  </div>
</template>

<script setup lang="ts">
import reservationAdminApi, { type LookupOption, type ReservationOrder } from '@/api/reservationAdmin'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'

const loading = ref(false)
const orderList = ref<ReservationOrder[]>([])
const userOptions = ref<LookupOption[]>([])
const trainerOptions = ref<LookupOption[]>([])

const searchForm = reactive<{
  memberId?: number
  trainerProfileId?: number
  statusCode?: string
}>({
  memberId: undefined,
  trainerProfileId: undefined,
  statusCode: undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const loadLookups = async (): Promise<void> => {
  const [users, trainers] = await Promise.all([
    reservationAdminApi.getUserOptions(),
    reservationAdminApi.getTrainerOptions()
  ])
  userOptions.value = users
  trainerOptions.value = trainers
}

const loadData = async (): Promise<void> => {
  loading.value = true
  try {
    const response = await reservationAdminApi.getOrderList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      memberId: searchForm.memberId,
      trainerProfileId: searchForm.trainerProfileId,
      statusCode: searchForm.statusCode
    })
    orderList.value = response.data
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
  searchForm.memberId = undefined
  searchForm.trainerProfileId = undefined
  searchForm.statusCode = undefined
  pagination.page = 1
  void loadData()
}

const handleSizeChange = (): void => {
  pagination.page = 1
  void loadData()
}

const updateStatus = async (orderId: number, statusCode: string): Promise<void> => {
  const actionText = statusCode === 'Completed' ? '标记完成' : '取消预约'
  await ElMessageBox.confirm(`确定要${actionText}吗？`, '操作确认', { type: 'warning' })
  const response = await reservationAdminApi.updateOrderStatus(orderId, statusCode)
  if (!response.success) {
    ElMessage.error(response.message || '操作失败')
    return
  }
  ElMessage.success(response.message || '操作成功')
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

.muted-text {
  color: #909399;
  font-size: 12px;
}
</style>
