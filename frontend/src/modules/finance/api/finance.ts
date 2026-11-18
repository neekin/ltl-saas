import { http } from '../../../shared/api/http';

export interface Receivable {
  id: string;
  waybillId: string;
  amount: number;
  settledAmount: number;
  outstanding: number;
  status: string;
}

export interface PaymentRecord {
  id: string;
  waybillId: string;
  amount: number;
  method: string;
  note: string | null;
  operatorUserId: string | null;
  occurredAt: string;
}

export interface CodOrder {
  id: string;
  waybillId: string;
  amount: number;
  collectedAmount: number;
  settledAmount: number;
  paidAmount: number;
  status: string;
}

export async function getReceivable(waybillId: string): Promise<Receivable | null> {
  const response = await http.get<Receivable>('/finance/receivables', { params: { waybillId } });
  return response.data;
}

export async function recordPayment(payload: {
  waybillId: string;
  amount: number;
  method: string;
  note?: string;
}): Promise<void> {
  await http.post('/finance/payments', payload);
}

export async function listPayments(waybillId: string): Promise<PaymentRecord[]> {
  const response = await http.get<PaymentRecord[]>('/finance/payments', { params: { waybillId } });
  return response.data;
}

export async function refundPayment(
  paymentId: string,
  amount: number,
  reason: string,
): Promise<void> {
  await http.post('/finance/refunds', { paymentId, amount, reason });
}

export async function getCodOrder(waybillId: string): Promise<CodOrder | null> {
  const response = await http.get<CodOrder>('/finance/cod', { params: { waybillId } });
  return response.data;
}

export async function recordCodCollection(
  waybillId: string,
  amount: number,
  note?: string,
): Promise<void> {
  await http.post('/finance/cod/collections', { waybillId, amount, note });
}

export async function createCodSettlement(waybillId: string, amount: number): Promise<void> {
  await http.post('/finance/cod/settlements', { waybillId, amount });
}

export async function approveCodSettlement(id: string): Promise<void> {
  await http.post(`/finance/cod/settlements/${id}/approve`);
}

export async function payCodSettlement(id: string): Promise<void> {
  await http.post(`/finance/cod/settlements/${id}/pay`);
}

export interface BranchPaymentSummary {
  branchId: string;
  branchCode: string;
  branchName: string;
  totalCollected: number;
  paymentCount: number;
  monthCollected: number;
  monthPaymentCount: number;
}

/** 网点收款汇总（FIN-003）：按运单始发网点聚合；month 为 yyyyMM 可选。 */
export async function getBranchPaymentSummary(month?: string): Promise<BranchPaymentSummary[]> {
  const response = await http.get<BranchPaymentSummary[]>('/finance/branch-payment-summary', {
    params: { month },
  });
  return response.data;
}

/** 应收懒初始化（首次收款前创建，金额 = 运单固化总额）。 */
export async function ensureReceivable(waybillId: string): Promise<Receivable> {
  const response = await http.post<Receivable>(
    `/finance/receivables/ensure?waybillId=${encodeURIComponent(waybillId)}`,
  );
  return response.data;
}

/** COD 订单懒初始化（首次登记收款前创建，金额 = 运单 COD 代收金额）。 */
export async function ensureCodOrder(waybillId: string): Promise<CodOrder> {
  const response = await http.post<CodOrder>(
    `/finance/cod/orders/ensure?waybillId=${encodeURIComponent(waybillId)}`,
  );
  return response.data;
}

export interface CodSettlement {
  id: string;
  codOrderId: string;
  amount: number;
  status: string;
  approvedBy: string | null;
  approvedAt: string | null;
  paidBy: string | null;
  paidAt: string | null;
}

/** 按运单列出 COD 结算单（新→旧）。 */
export async function listCodSettlements(waybillId: string): Promise<CodSettlement[]> {
  const response = await http.get<CodSettlement[]>('/finance/cod/settlements', {
    params: { waybillId },
  });
  return response.data;
}

// ===== P11：移动派单（DELIVERY-002） =====
export interface DeliveryTask {
  id: string;
  waybillId: string;
  branchId: string;
  deliveryType: string;
  status: string;
  driverId: string | null;
  assigneeUserId: string | null;
  assignedAt: string | null;
  createdAt: string;
}

/** 派单：分配员工执行配送。 */
export async function assignDelivery(id: string, assigneeUserId: string): Promise<DeliveryTask> {
  const response = await http.post<DeliveryTask>(`/deliveries/${id}/assign`, { assigneeUserId });
  return response.data;
}

/** 我的配送任务（移动端按当前用户查询待办）。 */
export async function listMyDeliveryTasks(): Promise<DeliveryTask[]> {
  const response = await http.get<DeliveryTask[]>('/deliveries/mine');
  return response.data;
}
