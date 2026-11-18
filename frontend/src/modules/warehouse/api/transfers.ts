import { http } from '../../../shared/api/http';

export interface BranchTransferItem {
  id: string;
  seq: number;
  cargoName: string;
  declaredQuantity: number;
  receivedQuantity: number | null;
  brokenQuantity: number | null;
  remark: string | null;
}

export interface BranchTransfer {
  id: string;
  transferNo: string;
  fromBranchId: string;
  toBranchId: string;
  status: string;
  items: BranchTransferItem[];
  note: string | null;
  createdAt: string;
  submittedAt: string | null;
  dispatchedAt: string | null;
  receivedAt: string | null;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function listTransfers(page = 1, pageSize = 10): Promise<Page<BranchTransfer>> {
  const response = await http.get<Page<BranchTransfer>>('/warehouse/transfers', {
    params: { page, pageSize },
  });
  return response.data;
}

export async function getTransfer(id: string): Promise<BranchTransfer> {
  const response = await http.get<BranchTransfer>(`/warehouse/transfers/${id}`);
  return response.data;
}

export async function createTransfer(payload: {
  fromBranchId: string;
  toBranchId: string;
  items: { cargoName: string; quantity: number; remark?: string }[];
  note?: string;
}): Promise<BranchTransfer> {
  const response = await http.post<BranchTransfer>('/warehouse/transfers', payload);
  return response.data;
}

export async function submitTransfer(id: string): Promise<void> {
  await http.post(`/warehouse/transfers/${id}/submit`);
}

export async function dispatchTransfer(id: string): Promise<void> {
  await http.post(`/warehouse/transfers/${id}/dispatch`);
}

export async function receiveTransfer(
  id: string,
  items: { itemId: string; receivedQuantity: number; brokenQuantity: number; remark?: string }[],
): Promise<void> {
  await http.post(`/warehouse/transfers/${id}/receive`, { items });
}

export async function cancelTransfer(id: string): Promise<void> {
  await http.post(`/warehouse/transfers/${id}/cancel`);
}
