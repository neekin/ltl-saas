/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // 本地开发代理到后端 API（dotnet run，默认 http://localhost:5080；
      // 避开 macOS ControlCenter/AirPlay 占用的 5000 端口）
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
  },
});
