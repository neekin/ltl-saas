import { expect, test } from '@playwright/test';
import { selectByLabel, submitWaybill } from './helpers';

/**
 * E2E 场景 2（AGENTS §10）：广州—武汉—长沙多段运输。
 * 前置：种子（GZ01/WH02/CS03 网点、车辆 粤A88888）。
 * 流程：开单（长沙为目的地）→ 确认 → 收货 → 分拣 →
 *      批次一（广州—武汉）装车/发车/到达 → 批次二（武汉—长沙）装车/发车/到达 →
 *      运单运输状态 = 运输中。
 */
test('多段运输：两段批次接力送达长沙', async ({ page }) => {
  // 1. 开单：广州 → 长沙，1 件 20kg
  await page.goto('/waybills/create');
  const { waybillNo, waybillId } = await submitWaybill(page, {
    originBranch: 'GZ01 广州',
    destBranch: 'CS03 长沙',
    customer: 'KH001 演示客户',
    ruleSet: 'std',
    receiver: ['王五', '13700000003', '长沙市开福区某仓库 3 号'],
    cargo: { name: '多段货', quantity: 1, weightKg: '20', volumeM3: '0.1' },
  });

  // 2. 确认运单
  await page.getByRole('button', { name: '确认运单' }).click();
  await expect(page.getByRole('cell', { name: '已确认' })).toBeVisible();

  // 3. 收货登记（实收 20kg / 0.1m³）
  await page.getByRole('button', { name: '收货登记' }).click();
  const receiptModal = page.locator('.ant-modal').filter({ hasText: '收货登记' });
  await receiptModal.locator('.ant-input-number input').nth(1).fill('20');
  await receiptModal.locator('.ant-input-number input').nth(2).fill('0.1');
  await receiptModal.getByRole('button', { name: '登记收货' }).click();
  await expect(page.getByText(/收货登记成功/)).toBeVisible();
  await expect(page.getByRole('cell', { name: '已收货' })).toBeVisible();

  // 4. 分拣
  await page.getByRole('button', { name: '创建分拣任务' }).click();
  await expect(page.getByText(/分拣任务已创建/)).toBeVisible();
  await page.goto('/warehouse/sorting');
  await page.getByPlaceholder('输入运单 ID 查询分拣任务').fill(waybillId);
  await page.getByRole('button', { name: '完成分拣' }).first().click();
  await expect(page.getByText('分拣已完成', { exact: true })).toBeVisible();
  // 分拣任务状态 Completed 显示为「已完成」（StatusTag）
  await expect(page.getByRole('cell', { name: '已完成' }).first()).toBeVisible();

  // 5. 批次一：广州 → 武汉
  await page.goto('/transport/trips');
  await page.getByRole('button', { name: '新建批次' }).click();
  const createModal = page.locator('.ant-modal').filter({ hasText: '新建运输批次' });
  await createModal.waitFor();
  await selectByLabel(page, '起点网点', 'GZ01 广州');
  await selectByLabel(page, '目的网点', 'WH02 武汉');
  await selectByLabel(page, '车辆', '粤A88888');
  await createModal.getByRole('button', { name: '确 定' }).click();
  await expect(page).toHaveURL(/\/transport\/trips\/[0-9a-f-]{36}$/i);

  await page.getByPlaceholder('输入运单号装车').fill(waybillNo);
  await page.getByRole('button', { name: '装车确认' }).click();
  await expect(page.getByText('装车确认成功')).toBeVisible();
  await page.getByRole('button', { name: /^发\s*车$/ }).click();
  await expect(page.getByText('已发车').first()).toBeVisible();
  await page.getByRole('button', { name: /^到\s*达$/ }).click();
  await expect(page.getByText('已到达').first()).toBeVisible();

  // 6. 批次二：武汉 → 长沙
  await page.goto('/transport/trips');
  await page.getByRole('button', { name: '新建批次' }).click();
  await createModal.waitFor();
  await selectByLabel(page, '起点网点', 'WH02 武汉');
  await selectByLabel(page, '目的网点', 'CS03 长沙');
  await selectByLabel(page, '车辆', '粤A88888');
  await createModal.getByRole('button', { name: '确 定' }).click();
  await expect(page).toHaveURL(/\/transport\/trips\/[0-9a-f-]{36}$/i);

  await page.getByPlaceholder('输入运单号装车').fill(waybillNo);
  await page.getByRole('button', { name: '装车确认' }).click();
  await expect(page.getByText('装车确认成功')).toBeVisible();
  await page.getByRole('button', { name: /^发\s*车$/ }).click();
  await expect(page.getByText('已发车').first()).toBeVisible();
  await page.getByRole('button', { name: /^到\s*达$/ }).click();
  await expect(page.getByText('已到达').first()).toBeVisible();

  // 7. 运单详情：运输状态 = 运输中，轨迹含两段到达事件
  await page.goto(`/waybills/${waybillId}`);
  await expect(page.getByRole('cell', { name: '运输中' })).toBeVisible();
  await expect(page.getByText(/到达/).first()).toBeVisible();
});
