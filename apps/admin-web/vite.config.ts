/// <reference types="vitest/config" />
import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': { target: 'http://localhost:8080', changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    // Without this, Node's own experimental `localStorage` global (which requires a
    // --localstorage-file backing) shadows jsdom's implementation, and any test touching
    // `localStorage` blows up with "Cannot read properties of undefined (reading 'clear')".
    // parent-portal's vite.config.ts already carries this same fix.
    env: {
      NODE_OPTIONS: '--no-experimental-webstorage',
    },
  },
})
