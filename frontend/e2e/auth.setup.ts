import { expect, test as setup } from '@playwright/test';
import { mkdirSync } from 'node:fs';

/**
 * 登录 setup：通过 UI 登录一次，保存 storageState 供业务用例复用。
 * 账号来自 Program.cs 的 SeedDevelopmentData（demo / admin / Admin@123）。
 */
const stateFile = 'e2e/.auth/state.json';

setup('登录并保存会话', async ({ page }) => {
  mkdirSync('e2e/.auth', { recursive: true });

  await page.goto('/login');
  await page.getByPlaceholder('租户编码').fill('demo');
  await page.getByPlaceholder('用户名').fill('admin');
  await page.getByPlaceholder('密码').fill('Admin@123');
  await page.getByRole('button', { name: /^登\s*录$/ }).click();

  // 登录成功后跳转到经营看板，布局头部显示当前用户姓名
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByText('管理员', { exact: true }).first()).toBeVisible({
    timeout: 15_000,
  });

  await page.context().storageState({ path: stateFile });
});
