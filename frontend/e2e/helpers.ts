import type { Page } from '@playwright/test';

/** antd6 可见下拉选项以 title 承载标签；已关闭下拉仍短暂挂载，限定非 hidden 容器并取最后渲染的。 */
export async function selectByLabel(page: Page, label: string, optionLabel: string) {
  const select = page
    .locator('.ant-form-item')
    .filter({ has: page.locator('label', { hasText: label }) })
    .locator('.ant-select');
  await select.click();
  await page
    .locator(
      `.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option[title="${optionLabel}"]`,
    )
    .last()
    .click();
}

/** 开单表单卡片定位器。 */
export function formCard(page: Page, title: string) {
  return page.locator('.ant-card').filter({ hasText: title });
}

/** 添加一个货物件并填入数量/重量/体积。 */
export async function addPackage(
  page: Page,
  cargo: { name: string; quantity: number; weightKg: string; volumeM3: string },
) {
  await page.getByRole('button', { name: '添加货物件' }).click();
  const row = page.locator('.ant-table-row');
  await row.getByPlaceholder('货物名称').fill(cargo.name);
  await row.locator('.ant-input-number input').nth(1).fill(cargo.weightKg);
  await row.locator('.ant-input-number input').nth(2).fill(cargo.volumeM3);
}

export interface WaybillDraft {
  originBranch: string;
  destBranch: string;
  customer: string;
  ruleSet: string;
  sender?: [string, string, string];
  receiver: [string, string, string];
  cargo: { name: string; quantity: number; weightKg: string; volumeM3: string };
}

/** 填写开单表单并提交，返回运单号；成功后停留在运单详情页。 */
export async function submitWaybill(
  page: Page,
  draft: WaybillDraft,
  options?: { codAmount?: string },
) {
  await page.goto('/waybills/create');
  await page.getByText('新建运单').waitFor();

  await selectByLabel(page, '始发网点', draft.originBranch);
  await selectByLabel(page, '目的网点', draft.destBranch);
  await selectByLabel(page, '客户', draft.customer);
  await selectByLabel(page, '价卡', draft.ruleSet);

  if (options?.codAmount) {
    await page
      .locator('.ant-form-item')
      .filter({ has: page.locator('label', { hasText: 'COD 代收金额' }) })
      .locator('.ant-input-number input')
      .fill(options.codAmount);
  }

  const sender = draft.sender ?? draft.receiver;
  await formCard(page, '收发货人').getByPlaceholder('发货人姓名').fill(sender[0]);
  await formCard(page, '收发货人').getByPlaceholder('联系电话').first().fill(sender[1]);
  await formCard(page, '收发货人').getByPlaceholder('详细发货地址').fill(sender[2]);
  await formCard(page, '收发货人').getByPlaceholder('收货人姓名').fill(draft.receiver[0]);
  await formCard(page, '收发货人').getByPlaceholder('联系电话').nth(1).fill(draft.receiver[1]);
  await formCard(page, '收发货人').getByPlaceholder('详细收货地址').fill(draft.receiver[2]);

  await addPackage(page, draft.cargo);
  await page.getByRole('button', { name: '开单并固化费用' }).click();

  const createdModal = page.locator('.ant-modal-confirm').filter({ hasText: '运单已创建' });
  await createdModal.waitFor();
  const waybillNo = (await createdModal.locator('.ltl-code').first().innerText()).trim();
  await createdModal.getByRole('button', { name: '完成并查看运单详情' }).click();
  await page.waitForURL(/\/waybills\/[0-9a-f-]{36}$/i);
  const waybillId = page.url().split('/').pop()!;
  return { waybillNo, waybillId };
}
