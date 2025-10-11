import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

export default defineConfig(({ mode }) => {
  const apiTarget = mode === 'development'
    ? 'http://localhost:5000'
    : 'http://159.75.184.108:8080'

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url))
      }
    },
    server: {
      port: 3001,
      host: '0.0.0.0',
      open: true,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true
        }
      }
    },
    optimizeDeps: {
      force: true
    },
    build: {
      // 生产环境关闭 source map、压缩体积报告以提升速度
      sourcemap: false,
      reportCompressedSize: false
    }
  }
})