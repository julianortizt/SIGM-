import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Vercel sirve este portal bajo /admin/ en el mismo dominio.
  base: '/admin/',
  server: {
    port: 3002,
    strictPort: true,
  },
})
