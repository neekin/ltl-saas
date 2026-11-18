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
