import { defineConfig } from '@playwright/test';

/**
 * E2E 测试配置（AGENTS §10：Playwright 覆盖关键端到端流程）。
 * 依赖本地开发栈：后端 http://localhost:5080（PostgreSQL/Redis 需先启动）。
 */
export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    locale: 'zh-CN',
    timezoneId: 'Asia/Shanghai',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'chromium',
      use: {
        browserName: 'chromium',
        storageState: 'e2e/.auth/state.json',
      },
      dependencies: ['setup'],
    },
  ],
  webServer: [
    {
      command: 'npm run dev -- --port 5173 --strictPort',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'dotnet run --project src/Ltl.Api --no-build',
      cwd: '../backend',
      url: 'http://localhost:5080/health',
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});
