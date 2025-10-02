import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

export default defineConfig({
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
        target: 'http://159.75.184.108:8090',
        changeOrigin: true
      }
    }
  },
  optimizeDeps: {
    force: true
  },
  build: {
    sourcemap: true
  }
})