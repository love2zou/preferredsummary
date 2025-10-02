import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import 'element-plus/dist/index.css'
import './assets/main.css'

import App from './App.vue'
import router from './router'
import { useAuthStore } from '@/stores/auth'

const app = createApp(App)

// 注册Element Plus图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(ElementPlus)
app.use(createPinia())
app.use(router)

// 应用挂载后设置定期检查登录过期
app.mount('#app')

// 每5分钟检查一次登录是否过期
setInterval(() => {
  const authStore = useAuthStore()
  if (authStore.isLoggedIn) {
    authStore.checkTokenExpiration()
  }
}, 5 * 60 * 1000) // 5分钟
