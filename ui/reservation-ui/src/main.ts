console.log('🚀 Vite + Vue 测试开始')

console.log('🚀 启动正式的 Vue 应用')

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'

import App from './App.vue'
import router from './router'

// 引入全局主题（暖绿色、扁平化）
import './styles/theme.css'
// 新增：引入全局 APP/小程序风格样式（按钮、容器、卡片）
import './style/index.css'

console.log('📦 正在创建 Vue 应用...')

// 创建应用
const app = createApp(App)

// 注册 Pinia 状态管理
const pinia = createPinia()
app.use(pinia)

// 注册 Element Plus
app.use(ElementPlus)

// 注册所有图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

// 注册路由
app.use(router)

// 挂载应用
app.mount('#app')

console.log('✅ Vue 应用启动完成')
console.log('🎯 当前路由:', router.currentRoute.value.path)
console.log('🔗 路由系统已加载')
console.log('🎨 Element Plus 已加载')
console.log('📊 Pinia 状态管理已加载')
