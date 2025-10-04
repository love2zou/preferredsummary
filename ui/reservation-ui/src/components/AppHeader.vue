<template>
  <header class="app-header">
    <button v-if="showBack" class="back-btn" @click="onBack">
      <el-icon><ArrowLeft /></el-icon>
    </button>
    <div class="app-title">{{ title }}</div>
    <div class="actions" v-if="showActions">
      <el-dropdown placement="bottom-end" trigger="click">
        <div class="pill">
          <span class="pill-item">
            <el-icon><MoreFilled /></el-icon>
          </span>
          <span class="divider"></span>
          <button class="pill-item" @click.stop="$emit('camera')">
            <el-icon><Camera /></el-icon>
          </button>
        </div>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item @click="$emit('more', 'profile')">个人信息</el-dropdown-item>
            <el-dropdown-item @click="$emit('more', 'settings')">设置</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ArrowLeft, Camera, MoreFilled } from '@element-plus/icons-vue';
import { useRouter } from 'vue-router';

defineProps<{ title: string; showBack?: boolean; showActions?: boolean }>()
const router = useRouter()
const onBack = () => router.back()
</script>

<style scoped>
.app-header { height: 48px; display: grid; grid-template-columns: 48px 1fr auto; align-items: center; padding: 0 12px; background: #fff; border-radius: var(--radius); box-shadow: var(--shadow); border: 1px solid var(--border-color); position: sticky; top: 0; z-index: 10; }
.app-title { text-align: center; font-size: 16px; font-weight: 600; color: var(--text-color); }
.back-btn { width: 32px; height: 32px; border: 0; background: transparent; color: var(--text-color); border-radius: 9999px; cursor: pointer; }
.actions { display: flex; align-items: center; }
.pill { display: inline-flex; align-items: center; border: 1px solid var(--border-color); background: #fff; border-radius: 9999px; padding: 0 6px; box-shadow: var(--shadow); }
.pill-item { width: 32px; height: 32px; display: inline-flex; align-items: center; justify-content: center; color: var(--text-color); border-radius: 9999px; cursor: pointer; }
.pill-item:hover { color: var(--primary-color); background: rgba(0,0,0,0.04); }
.divider { width: 1px; height: 16px; background: var(--border-color); margin: 0 4px; }
</style>