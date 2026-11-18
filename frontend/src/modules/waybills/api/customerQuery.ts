/**
 * 客户运单查询 API（P10）：匿名端点，凭租户编码 + 运单号 + 查询码查单票。
 * 不走带 JWT 刷新的 http 客户端（客户无登录态），直接用同源 fetch。
 */
export interface CustomerPackageView {
  packageNo: number;
  cargoName: string;
  quantity: number;
  weightKg: number;
  volumeM3: number;
}

export interface CustomerWaybillView {
  waybillNo: string;
  status: string;
  cargoStatus: string;
  transportStatus: string;
  deliveryStatus: string;
  freightStatus: string;
  senderName: string;
  senderPhoneMasked: string;
  receiverName: string;
  receiverPhoneMasked: string;
  totalAmount: number | null;
  createdAt: string;
  packages: CustomerPackageView[];
}

export async function queryWaybillAsCustomer(
  tenantCode: string,
  waybillNo: string,
  queryCode: string,
): Promise<CustomerWaybillView> {
  const response = await fetch('/api/public/waybill-query', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tenantCode, waybillNo, queryCode }),
  });
  if (response.status === 401) {
    throw new Error('运单号或查询码不正确');
  }
  if (!response.ok) {
    throw new Error('查询失败，请稍后重试');
  }
  return (await response.json()) as CustomerWaybillView;
}
