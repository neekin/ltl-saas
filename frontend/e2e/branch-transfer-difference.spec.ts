import { expect, test } from '@playwright/test';
import { selectByLabel } from './helpers';

/**
 * E2E 场景 3（AGENTS §10）：网点调拨及数量差异。
 * 流程：创建调拨单（GZ01→WH02，申报 10 件）→ 提交 → 发出确认 →
 *      验收入库（实收 7 + 破损 2，差异 -3 留痕）→ 状态已验收。
 */
test('网点调拨：申报 10 件，验收 7+2 破损，差异留痕', async ({ page }) => {
  // 1. 创建调拨单
  await page.goto('/transfers');
  await page.getByRole('button', { name: '创建调拨单' }).click();
  await selectByLabel(page, '调出网点', 'GZ01 广州');
  await selectByLabel(page, '调入网点', 'WH02 武汉');
  await page.getByPlaceholder('货物名称').fill('整箱矿泉水');
  await page
    .locator('.ant-card')
    .filter({ hasText: '新建调拨单' })
    .locator('.ant-input-number input')
    .fill('10');
  await page.getByRole('button', { name: /^创\s*建$/, exact: true }).click();
  await expect(page.getByText('调拨单已创建')).toBeVisible();

  await expect(page.getByRole('cell', { name: '整箱矿泉水' })).toBeVisible();

  // 2. 提交 → 发出确认（两字按钮 antd 会插入空格）
  await page.getByRole('button', { name: /^提\s*交$/ }).click();
  await expect(page.getByText('已提交').first()).toBeVisible();
  await page.getByRole('button', { name: '发出确认' }).click();
  await expect(page.getByText('已发出确认').first()).toBeVisible();

  // 3. 验收：实收 7、破损 2、备注差异原因
  const row = page.locator('.ant-table-row');
  const receivedInput = row.locator('.ant-input-number input').nth(0);
  await receivedInput.fill('7');
  await expect(receivedInput).toHaveValue('7');
  const brokenInput = row.locator('.ant-input-number input').nth(1);
  await brokenInput.fill('2');
  await expect(brokenInput).toHaveValue('2');
  // 复核：第二处输入不得破坏第一处（曾因表格重渲染回退为 1）
  await expect(receivedInput).toHaveValue('7');
  await row.getByPlaceholder('短少/破损说明').fill('运输短少 3 件，其中 2 件破损');
  await page.getByRole('button', { name: '验收入库' }).click();
  await expect(page.getByText('验收完成（差异已留痕）')).toBeVisible();

  // 4. 差异留痕核验：实收 7、破损 2、差异 -3，且输入框变为只读文本
  await expect(page.getByRole('cell', { name: '7', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '2', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '-3', exact: true })).toBeVisible();
  await expect(page.getByText('运输短少 3 件，其中 2 件破损')).toBeVisible();
});
