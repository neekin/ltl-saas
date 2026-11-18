import { expect, test } from '@playwright/test';
import { selectByLabel, submitWaybill } from './helpers';

/**
 * E2E 场景 4（AGENTS §10）：月结和部分收款。
 * 流程：月结开单（50kg → ¥75）→ 财务页查询应收 → 部分收款 ¥30（PartiallyPaid）→
 *      补收 ¥45 → 结清（Settled，收款登记区消失）。
 */
test('月结运单：部分收款后补齐结清', async ({ page }) => {
  // 1. 月结开单
  await page.goto('/waybills/create');
  await selectByLabel(page, '支付方式', '月结');
  const { waybillId } = await submitWaybill(page, {
    originBranch: 'GZ01 广州',
    destBranch: 'WH02 武汉',
    customer: 'KH001 演示客户',
    ruleSet: 'std',
    receiver: ['赵六', '13600000004', '武汉市汉阳区某市场 6 号'],
    cargo: { name: '月结货', quantity: 1, weightKg: '50', volumeM3: '0.2' },
  });

  // 确认运单（应收在收款时懒创建）
  await page.getByRole('button', { name: '确认运单' }).click();
  await expect(page.getByRole('cell', { name: '已确认' })).toBeVisible();

  // 2. 财务页查询应收
  await page.goto('/finance');
  await page.getByPlaceholder('输入运单 ID 查询应收').fill(waybillId);
  await page.getByRole('button', { name: /^查\s*询$/ }).click();
  // 应收懒创建：首次收款前需先初始化应收
  await page.getByRole('button', { name: '初始化应收' }).click();
  await expect(page.getByText('应收已创建')).toBeVisible();
  await expect(page.getByText('¥ 75.00').first()).toBeVisible();

  // 3. 部分收款 ¥30 → PartiallyPaid
  const payCard = page.locator('.ant-card').filter({ hasText: '收款登记' });
  await payCard.locator('.ant-input-number input').fill('30');
  await payCard.getByRole('button', { name: /收\s*款$/ }).click();
  await expect(page.getByText('收款成功')).toBeVisible();
  await expect(page.getByText('¥ 30.00').first()).toBeVisible(); // 已收金额

  // 4. 补收 ¥45 → Settled
  await payCard.locator('.ant-input-number input').fill('45');
  await payCard.getByRole('button', { name: /收\s*款$/ }).click();
  await expect(page.getByText('收款成功')).toBeVisible();
  // 结清后收款登记区不再展示
  await expect(payCard).toHaveCount(0);
  // 应收 75 / 已收 75 / 未收 0
  await expect(page.getByText('¥ 75.00')).toHaveCount(2);
  await expect(page.getByText('¥ 0.00')).toBeVisible();
  // 收款记录两笔
  await expect(
    page.locator('.ant-card').filter({ hasText: '收款记录' }).locator('.ant-table-row'),
  ).toHaveCount(2);
});
