import { expect, test } from '@playwright/test';

/**
 * E2E 场景 1（AGENTS §10）：现付直达运单全流程。
 * 前置：开发种子数据（demo 租户、GZ01/WH02 网点、客户 KH001、标准价卡 std：按重 1.5 元/kg）。
 * 流程：登录态下开单 → 实时试算 → 固化费用 → 详情页核验状态与应收。
 */

/** 通过 Form.Item 标签定位 antd Select 并选择选项（antd6 可见选项用 title 属性承载标签）。 */
async function selectByLabel(
  page: import('@playwright/test').Page,
  label: string,
  optionLabel: string,
) {
  const select = page
    .locator('.ant-form-item')
    .filter({ has: page.locator('label', { hasText: label }) })
    .locator('.ant-select');
  await select.click();
  // 上一次已关闭的下拉仍会短暂挂载在 DOM，取当前打开（非 hidden）下拉中的可见选项。
  await page
    .locator(`.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option[title="${optionLabel}"]`)
    .last()
    .click();
}

test('现付直达运单：开单→试算→固化→详情核验', async ({ page }) => {
  // 1. 进入开单页
  await page.goto('/waybills/create');
  await expect(page.getByText('新建运单')).toBeVisible();

  // 2. 业务信息：GZ01 广州 → WH02 武汉，客户 KH001，价卡 std，支付方式默认现付
  await selectByLabel(page, '始发网点', 'GZ01 广州');
  await selectByLabel(page, '目的网点', 'WH02 武汉');
  await selectByLabel(page, '客户', 'KH001 演示客户');
  await selectByLabel(page, '价卡', 'std');
  await expect(
    page
      .locator('.ant-form-item')
      .filter({ has: page.locator('label', { hasText: '支付方式' }) })
      .getByText('现付', { exact: true }),
  ).toBeVisible();

  // 3. 收发货人（发货人信息在选定客户后由档案预填，覆盖为实际值更稳）
  const card = (title: string) => page.locator('.ant-card').filter({ hasText: title });
  await card('收发货人').getByPlaceholder('发货人姓名').fill('张三');
  await card('收发货人').getByPlaceholder('联系电话').first().fill('13800000001');
  await card('收发货人').getByPlaceholder('详细发货地址').fill('广州市白云区物流园 1 号');
  await card('收发货人').getByPlaceholder('收货人姓名').fill('李四');
  await card('收发货人').getByPlaceholder('联系电话').nth(1).fill('13900000002');
  await card('收发货人').getByPlaceholder('详细收货地址').fill('武汉市东西湖区舵落口大市场 8 号');

  // 4. 货物件：1 件 10 kg → 按重计费 10 × 1.5 = 15.00 元
  await page.getByRole('button', { name: '添加货物件' }).click();
  const row = page.locator('.ant-table-row');
  await row.getByPlaceholder('货物名称').fill('标准纸箱');
  await row.locator('.ant-input-number input').nth(1).fill('10');
  await row.locator('.ant-input-number input').nth(2).fill('0.05');
  await expect(page.getByText('Σ 1 件 / 10.00 kg')).toBeVisible();

  // 5. 实时试算：命中标准运价，应收合计 ¥ 15.00
  await page.getByRole('button', { name: '实时试算' }).click();
  await expect(page.getByText(/试算结果：命中 标准运价/)).toBeVisible();
  await expect(page.getByText('¥ 15.00').first()).toBeVisible();

  // 6. 开单并固化费用：成功弹窗展示运单号与查询码
  await page.getByRole('button', { name: '开单并固化费用' }).click();
  const createdModal = page.locator('.ant-modal-confirm').filter({ hasText: '运单已创建' });
  await expect(createdModal).toBeVisible();
  const waybillNo = (await createdModal.locator('.ltl-code').first().innerText()).trim();
  expect(waybillNo.length).toBeGreaterThan(6);
  await expect(createdModal.getByText('查询码', { exact: true })).toBeVisible();

  // 7. 查看运单详情：草稿态核验支付方式与应收，再确认运单
  await createdModal.getByRole('button', { name: '完成并查看运单详情' }).click();
  await expect(page).toHaveURL(/\/waybills\/[0-9a-f-]{36}$/i);
  await expect(page.getByRole('cell', { name: '现付' })).toBeVisible();
  await expect(page.getByRole('cell', { name: '¥ 15.00' })).toBeVisible();

  // 8. 确认运单：状态由草稿变为已确认
  await page.getByRole('button', { name: '确认运单' }).click();
  await expect(page.getByRole('cell', { name: '已确认' })).toBeVisible();
});
