import { http } from '../../../shared/api/http';

export interface WaybillListItem {
  id: string;
  waybillNo: string;
  status: string;
  cargoStatus: string;
  transportStatus: string;
  deliveryStatus: string;
  freightStatus: string;
  codStatus: string;
  originBranchId: string;
  destBranchId: string;
  totalAmount: number | null;
  createdAt: string;
}

export interface WaybillPackage {
  id: string;
  packageNo: number;
  cargoName: string;
  quantity: number;
  weightKg: number;
  volumeM3: number;
  remark: string | null;
}

export interface WaybillDetail {
  id: string;
  waybillNo: string;
  status: string;
  cargoStatus: string;
  transportStatus: string;
  deliveryStatus: string;
  freightStatus: string;
  codStatus: string;
  originBranchId: string;
  destBranchId: string;
  customerId: string;
  routeId: string | null;
  paymentType: string;
  totalAmount: number | null;
  codAmount: number | null;
  senderName: string;
  senderPhone: string;
  senderAddress: string;
  receiverName: string;
  receiverPhone: string;
  receiverAddress: string;
  remark: string | null;
  createdAt: string;
  packages: WaybillPackage[];
}

export interface WaybillEvent {
  id: string;
  eventType: string;
  detail: string | null;
  operatorId: string | null;
  occurredAt: string;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function listWaybills(
  page: number,
  pageSize: number,
  waybillNo?: string,
): Promise<Page<WaybillListItem>> {
  const response = await http.get<Page<WaybillListItem>>('/waybills', {
    params: { page, pageSize, waybillNo },
  });
  return response.data;
}

export async function createWaybill(payload: {
  originBranchId: string;
  destBranchId: string;
  customerId: string;
  ruleSetId: string;
  paymentType: string;
  senderName: string;
  senderPhone: string;
  senderAddress: string;
  receiverName: string;
  receiverPhone: string;
  receiverAddress: string;
  remark?: string;
  declaredValue?: number;
  insuranceFee?: number;
  packages: { cargoName: string; quantity: number; weightKg: number; volumeM3: number }[];
}): Promise<{ waybillId: string; waybillNo: string; totalAmount: number }> {
  const response = await http.post<{ waybillId: string; waybillNo: string; totalAmount: number }>('/waybills', payload);
  return response.data;
}

export async function getWaybill(id: string): Promise<WaybillDetail> {
  const response = await http.get<WaybillDetail>(`/waybills/${id}`);
  return response.data;
}

export async function listWaybillEvents(id: string): Promise<WaybillEvent[]> {
  const response = await http.get<WaybillEvent[]>(`/waybills/${id}/events`);
  return response.data;
}

export async function confirmWaybill(id: string): Promise<void> {
  await http.post(`/waybills/${id}/confirm`);
}

export async function cancelWaybill(id: string, reason?: string): Promise<void> {
  await http.post(`/waybills/${id}/cancel`, { reason });
}

export interface CargoReceiptResult {
  receipt: { id: string; receivedPieces: number; receivedWeightKg: number; receivedVolumeM3: number };
  waybill: { waybillNo: string; declaredPieces: number; declaredWeightKg: number; declaredVolumeM3: number };
  pieceDifference: number;
  weightDifferenceKg: number;
  volumeDifferenceM3: number;
}

export async function recordReceipt(
  waybillId: string,
  branchId: string,
  payload: { receivedPieces: number; receivedWeightKg: number; receivedVolumeM3: number; note?: string },
): Promise<CargoReceiptResult> {
  const response = await http.post<CargoReceiptResult>('/warehouse/receipts', {
    waybillId,
    branchId,
    ...payload,
  });
  return response.data;
}

export async function createSortingTask(waybillId: string, branchId: string, note?: string): Promise<void> {
  await http.post('/warehouse/sorting-tasks', { waybillId, branchId, note });
}

export async function completeSortingTask(taskId: string): Promise<void> {
  await http.post(`/warehouse/sorting-tasks/${taskId}/complete`);
}

export interface SortingTask {
  id: string;
  waybillId: string;
  branchId: string;
  status: string;
  completedAt: string | null;
  note: string | null;
  createdAt: string;
}

export async function listSortingTasks(waybillId: string): Promise<SortingTask[]> {
  const response = await http.get<SortingTask[]>('/warehouse/sorting-tasks', { params: { waybillId } });
  return response.data;
}
