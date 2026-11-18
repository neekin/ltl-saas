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

export interface WaybillPrintView {
  waybillId: string;
  waybillNo: string;
  waybillStatus: string;
  cargoStatus: string;
  transportStatus: string;
  deliveryStatus: string;
  freightStatus: string;
  codStatus: string;
  paymentType: string;
  senderName: string;
  senderPhone: string;
  senderAddress: string;
  receiverName: string;
  receiverPhone: string;
  receiverAddress: string;
  totalAmount: number | null;
  codAmount: number | null;
  packages: {
    packageNo: number;
    cargoName: string;
    quantity: number;
    weightKg: number;
    volumeM3: number;
    remark: string | null;
  }[];
  declaredPieces: number;
  declaredWeightKg: number;
  declaredVolumeM3: number;
  createdAt: string;
  createdBy: string | null;
}

export async function getWaybillPrintView(id: string): Promise<WaybillPrintView> {
  const response = await http.get<WaybillPrintView>(`/waybills/${id}/print`);
  return response.data;
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
  /** COD 代收金额（独立资金业务，不计入运费应收）；不传 = 无 COD */
  codAmount?: number;
  packages: { cargoName: string; quantity: number; weightKg: number; volumeM3: number }[];
}): Promise<{ waybillId: string; waybillNo: string; totalAmount: number; queryCode?: string }> {
  const response = await http.post<{
    waybillId: string;
    waybillNo: string;
    totalAmount: number;
    queryCode?: string;
  }>('/waybills', payload);
  return response.data;
}

export async function getWaybill(id: string): Promise<WaybillDetail> {
  const response = await http.get<WaybillDetail>(`/waybills/${id}`);
  return response.data;
}

/**
 * 移动端按完整运单号定位单据。
 * 列表接口是模糊查询，必须在前端核对完整单号，避免把首条相似单据用于现场作业。
 */
export async function getWaybillByNo(waybillNo: string): Promise<WaybillDetail | null> {
  const normalized = waybillNo.trim();
  if (!normalized) return null;
  const page = await listWaybills(1, 100, normalized);
  const exact = page.items.find((item) => item.waybillNo === normalized);
  return exact ? getWaybill(exact.id) : null;
}

/** P10：重新生成客户查询码（waybill.read，换码作废旧码）。 */
export async function resetQueryCode(id: string): Promise<string> {
  const response = await http.post<{ queryCode: string }>(`/waybills/${id}/query-code`);
  return response.data.queryCode;
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
  receipt: {
    id: string;
    receivedPieces: number;
    receivedWeightKg: number;
    receivedVolumeM3: number;
  };
  waybill: {
    waybillNo: string;
    declaredPieces: number;
    declaredWeightKg: number;
    declaredVolumeM3: number;
  };
  pieceDifference: number;
  weightDifferenceKg: number;
  volumeDifferenceM3: number;
}

export async function recordReceipt(
  waybillId: string,
  branchId: string,
  payload: {
    receivedPieces: number;
    receivedWeightKg: number;
    receivedVolumeM3: number;
    note?: string;
  },
): Promise<CargoReceiptResult> {
  const response = await http.post<CargoReceiptResult>('/warehouse/receipts', {
    waybillId,
    branchId,
    ...payload,
  });
  return response.data;
}

export async function createSortingTask(
  waybillId: string,
  branchId: string,
  note?: string,
): Promise<void> {
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
  const response = await http.get<SortingTask[]>('/warehouse/sorting-tasks', {
    params: { waybillId },
  });
  return response.data;
}
