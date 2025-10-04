<template>
  <div class="page">
    <div class="hero-header">
      <div class="header-left" @click="goBack" role="button" aria-label="返回">
        <el-icon class="back-btn"><ArrowLeft /></el-icon>
      </div>
      <div class="app-title">关于健身</div>
      <div class="header-right"></div>
    </div>
    <div class="mini-card card">
      <p class="desc">健身是一个长期的过程，坚持比强度更重要。</p>
      <div class="section">
        <h3 class="section-title">基础原则</h3>
        <ul class="list">
          <li>科学训练：力量、有氧与柔韧性合理搭配</li>
          <li>营养补给：蛋白质、碳水与脂肪均衡摄入</li>
          <li>充分休息：保证睡眠，促进恢复与增长</li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
const router = useRouter()
function goBack() { router.back() }
</script>

<style scoped>
.page { padding: 16px; }
/* 顶部栏样式：与 Profile.vue 的 hero-header 保持一致 */
.hero-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: rgba(255,255,255,0.85);
  backdrop-filter: saturate(180%) blur(8px);
  border-bottom: 1px solid rgba(0,0,0,0.06);
}
.app-title { font-size: 16px; font-weight: 600; color: var(--text-color); }
.back-btn { font-size: 18px; cursor: pointer; padding: 6px; border-radius: 50%; }
.header-right { width: 36px; }
.title { margin: 0; font-size: 20px; font-weight: 700; color: var(--primary-color-700); }
.desc { color: var(--text-secondary); margin: 6px 0 12px; font-size: 14px; }
.section-title { margin: 12px 0 8px; font-size: 16px; font-weight: 600; color: var(--text-color); }
.list { margin: 0; padding-left: 18px; color: var(--text-color); font-size: 13px; }
.card { padding: 16px; }
</style>