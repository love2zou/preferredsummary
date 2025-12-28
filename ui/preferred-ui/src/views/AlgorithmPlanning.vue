<template>
  <div class="planning-container">
    <div class="header">
      <h1>AI算法规划</h1>
      <p class="sub">选择需要的算法分析模块</p>
    </div>

    <div class="cards">
      <el-card class="card" shadow="hover" @click="openFireAnalysis">
        <div class="card-body">
          <div class="icon">🔥</div>
          <div class="info">
            <h3>火灾算法分析</h3>
            <p>新窗口打开，占位页暂未实现内容</p>
          </div>
        </div>
      </el-card>

      <el-card class="card" shadow="hover" @click="goWaveFileAnalysis">
        <div class="card-body">
          <div class="icon">📁</div>
          <div class="info">
            <h3>录波文件分析</h3>
            <p>进入应用内占位页，后续可扩展功能</p>
          </div>
        </div>
      </el-card>
    </div>
  </div>
  
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const openFireAnalysis = (): void => {
  window.open('/fire-analysis', '_blank')
}

const goWaveFileAnalysis = (): void => {
  router.push('/wavefile-analysis')
}
</script>

<style scoped>
.planning-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #f0f4ff 0%, #f7f9fc 100%);
  padding: 24px;
}

.header {
  max-width: 1200px;
  margin: 0 auto 12px;
}

.header h1 {
  margin: 0;
  font-size: 22px;
  color: #1f2d3d;
}

.sub { color: #596c89; margin-top: 6px; }

.cards {
  max-width: 1200px;
  margin: 0 auto;
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.card { cursor: pointer; border-radius: 12px; }

.card-body {
  display: flex;
  align-items: center;
  gap: 14px;
}

.icon { font-size: 28px; }

.info h3 { margin: 0 0 4px; font-size: 18px; color: #1f2d3d; }
.info p { margin: 0; color: #606266; font-size: 13px; }

@media (max-width: 768px) {
  .cards { grid-template-columns: 1fr; }
}
</style>

