<template>
  <div class="page">
    <div class="mini-card card">
      <h2 class="title">关于健身</h2>
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
</script>

<style scoped>
.page { padding: 16px; }
.title { margin: 0; font-size: 20px; font-weight: 700; color: var(--primary-color-700); }
.desc { color: var(--text-secondary); margin: 6px 0 12px; font-size: 14px; }
.section-title { margin: 12px 0 8px; font-size: 16px; font-weight: 600; color: var(--text-color); }
.list { margin: 0; padding-left: 18px; color: var(--text-color); font-size: 13px; }
.card { padding: 16px; }
</style>