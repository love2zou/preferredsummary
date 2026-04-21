<template>
  <div class="pdf-intro-page">
    <header class="intro-hero">
      <div class="hero-copy">
        <p class="eyebrow">PDF 工具</p>
        <h1>拆分、合并本地 PDF</h1>
        <p class="hero-text">文件只在浏览器中处理，不上传服务器。适合整理合同、报告、资料包和归档文件。</p>
        <el-button type="primary" size="large" @click="goWorkbench">进入 PDF 工具台</el-button>
      </div>
      <div class="hero-panel">
        <div class="pdf-sheet sheet-back"></div>
        <div class="pdf-sheet sheet-front">
          <span>PDF</span>
          <strong>Split & Merge</strong>
        </div>
      </div>
    </header>

    <main class="intro-content">
      <section class="tool-grid">
        <article class="tool-card">
          <div class="tool-icon split-icon">
            <el-icon><DocumentCopy /></el-icon>
          </div>
          <h2>PDF 拆分</h2>
          <p>按页码范围提取页面，支持单页、多段页码和每 N 页分组。</p>
          <ul>
            <li>合同拆页</li>
            <li>资料提取</li>
            <li>报告分发</li>
          </ul>
        </article>

        <article class="tool-card">
          <div class="tool-icon merge-icon">
            <el-icon><Files /></el-icon>
          </div>
          <h2>PDF 合并</h2>
          <p>选择多个 PDF，调整顺序后合并为一个文件。</p>
          <ul>
            <li>资料归档</li>
            <li>报告合成</li>
            <li>附件整理</li>
          </ul>
        </article>
      </section>

      <section class="guide-band">
        <div>
          <h2>使用流程</h2>
          <p>选择本地 PDF 文件，设置拆分或合并规则，生成后直接下载结果。</p>
        </div>
        <el-button type="primary" plain @click="goWorkbench">开始处理</el-button>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { DocumentCopy, Files } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'

const router = useRouter()

const goWorkbench = () => {
  void router.push('/pdf-tools/workbench')
}
</script>

<style scoped>
.pdf-intro-page {
  min-height: 100vh;
  color: #172033;
  background: #f5f7fb;
}

.intro-hero {
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(280px, 0.85fr);
  gap: 36px;
  align-items: center;
  min-height: 420px;
  padding: 56px min(7vw, 92px) 48px;
  background: linear-gradient(135deg, rgba(22, 119, 255, 0.12), rgba(40, 190, 150, 0.1)), #ffffff;
  border-bottom: 1px solid #e5e9f2;
}

.eyebrow {
  margin: 0 0 12px;
  color: #1677ff;
  font-weight: 700;
}

.hero-copy h1 {
  margin: 0;
  font-size: 42px;
  line-height: 1.15;
  letter-spacing: 0;
}

.hero-text {
  max-width: 560px;
  margin: 18px 0 28px;
  color: #5c667a;
  font-size: 17px;
  line-height: 1.8;
}

.hero-panel {
  position: relative;
  min-height: 260px;
}

.pdf-sheet {
  position: absolute;
  width: min(260px, 70vw);
  height: 330px;
  border-radius: 8px;
  box-shadow: 0 18px 42px rgba(36, 49, 82, 0.18);
}

.sheet-back {
  right: 40px;
  top: 0;
  background: repeating-linear-gradient(#ffffff, #ffffff 22px, #e9edf5 23px);
  transform: rotate(7deg);
}

.sheet-front {
  right: 92px;
  top: 24px;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  padding: 28px;
  color: #ffffff;
  background: linear-gradient(160deg, #1677ff, #28be96);
}

.sheet-front span {
  width: max-content;
  padding: 5px 9px;
  border: 1px solid rgba(255, 255, 255, 0.65);
  border-radius: 6px;
  font-size: 13px;
}

.sheet-front strong {
  margin-top: 14px;
  font-size: 26px;
  letter-spacing: 0;
}

.intro-content {
  max-width: 1120px;
  margin: 0 auto;
  padding: 34px 24px 56px;
}

.tool-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 22px;
}

.tool-card {
  padding: 26px;
  background: #ffffff;
  border: 1px solid #e5e9f2;
  border-radius: 8px;
}

.tool-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  border-radius: 8px;
  color: #ffffff;
  font-size: 24px;
}

.split-icon {
  background: #1677ff;
}

.merge-icon {
  background: #28a67d;
}

.tool-card h2,
.guide-band h2 {
  margin: 18px 0 10px;
  font-size: 22px;
  letter-spacing: 0;
}

.tool-card p,
.guide-band p {
  margin: 0;
  color: #5c667a;
  line-height: 1.7;
}

.tool-card ul {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  padding: 0;
  margin: 18px 0 0;
  list-style: none;
}

.tool-card li {
  padding: 6px 10px;
  color: #43516a;
  background: #f0f4fa;
  border-radius: 6px;
  font-size: 13px;
}

.guide-band {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  margin-top: 24px;
  padding: 26px;
  background: #ffffff;
  border: 1px solid #e5e9f2;
  border-radius: 8px;
}

.guide-band h2 {
  margin-top: 0;
}

@media (max-width: 760px) {
  .intro-hero,
  .tool-grid {
    grid-template-columns: 1fr;
  }

  .intro-hero {
    padding: 36px 20px;
  }

  .hero-copy h1 {
    font-size: 32px;
  }

  .hero-panel {
    min-height: 220px;
  }

  .pdf-sheet {
    height: 260px;
  }

  .sheet-back {
    right: 12px;
  }

  .sheet-front {
    right: 50px;
  }

  .guide-band {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
