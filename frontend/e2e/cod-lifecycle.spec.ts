import { expect, test } from '@playwright/test';
import { selectByLabel, submitWaybill } from './helpers';

/**
 * E2E 场景 5（AGENTS §10）：COD 收取、结算和付款登记。
 * 流程：开单（COD 代收 ¥200）→ COD 工作台登记收款 ¥200 →
 *      COD 管理页创建结算 ¥200 → 审批 → 付款给发货人。
 * COD 是独立资金业务，不计入运费应收（AGENTS §6.5）。
 */
test('COD：收款 → 结算 → 审批 → 付款全链路', async ({ page }) => {
  // 1. 开单并填写 COD 代收金额 ¥200
  await page.goto('/waybills/create');
  const { waybillId } = await submitWaybill(page, {
    originBranch: 'GZ01 广州',
    destBranch: 'WH02 武汉',
    customer: 'KH001 演示客户',
    ruleSet: 'std',
    receiver: ['钱七', '13500000005', '武汉市江岸区某小区 9 号'],
    cargo: { name: 'COD 货', quantity: 1, weightKg: '10', volumeM3: '0.05' },
  }, { codAmount: '200' });

  // 运单详情：COD 代收 200（独立资金，不计入运费）
  await expect(page.getByRole('cell', { name: /¥ 200\.00（独立资金/ })).toBeVisible();

  // 2. COD 工作台：登记收款 ¥200
  await page.goto('/finance/cod');
  await page.getByPlaceholder('输入运单 ID 查询 COD 订单').fill(waybillId);
  await page.getByRole('button', { name: /^查\s*询$/ }).click();
  // COD 订单懒创建：首次收款前需先初始化
  await page.getByRole('button', { name: '初始化 COD 订单' }).click();
  await expect(page.getByText('COD 订单已创建')).toBeVisible();
  await expect(page.getByText('¥ 200.00').first()).toBeVisible();
  const workbench = page.locator('.ant-card').filter({ hasText: 'COD 订单' });
  await workbench.locator('.ant-input-number input').fill('200');
  await workbench.getByRole('button', { name: '登记收款' }).click();
  await expect(page.getByText('COD 收款成功')).toBeVisible();

  // 3. COD 管理：创建结算 ¥200 → 审批 → 付款
  await page.goto('/finance/cod/manage');
  await page.getByPlaceholder('输入运单 ID 查询 COD 订单').fill(waybillId);
  await page.getByRole('button', { name: /^查\s*询$/ }).click();
  await expect(page.getByText('可结算上限 ¥ 200.00')).toBeVisible();
  const settleCard = page.locator('.ant-card').filter({ hasText: '创建结算单' });
  await settleCard.locator('.ant-input-number input').fill('200');
  await settleCard.getByRole('button', { name: '创建结算' }).click();
  await expect(page.getByText('结算单已创建', { exact: true })).toBeVisible();

  const approvalCard = page.locator('.ant-card').filter({ hasText: '审批和付款' });
  await approvalCard.getByRole('button', { name: /审批 ¥ 200\.00/ }).click();
  // Popconfirm 确认
  await page.locator('.ant-popover:not(.ant-popover-hidden)').getByRole('button', { name: /^确\s*定$/ }).click();
  await expect(page.getByText('已审批')).toBeVisible();

  await approvalCard.getByRole('button', { name: /付款 ¥ 200\.00/ }).click();
  await page.locator('.ant-popover:not(.ant-popover-hidden)').getByRole('button', { name: /^确\s*定$/ }).click();
  await expect(page.getByText('已付款给发货人')).toBeVisible();
});
