import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// Under Aspire (backend/QuizApp.AppHost), WithReference(api) passes the API's address in this variable.
// Without Aspire, the API runs on its launchSettings port.
const apiUrl = process.env.services__api__http__0 ?? 'http://localhost:5210'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: { '/api': apiUrl },
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
