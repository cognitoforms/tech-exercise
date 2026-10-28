import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    // The API runs separately in development; see README.
    proxy: { '/api': 'http://localhost:5210' },
  },
  build: {
    // The backend serves the built SPA from its wwwroot.
    outDir: fileURLToPath(new URL('../backend/QuizApp.Api/wwwroot', import.meta.url)),
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
  },
})
