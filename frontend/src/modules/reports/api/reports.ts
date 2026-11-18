import { http } from '../../../shared/api/http';

export interface DashboardMetrics {
  totalWaybills: number;
  monthlyWaybills: number;
  totalCargoWeightKg: number;
  totalCargoVolumeM3: number;
  totalFreightRevenue: number;
  totalReceivableAmount: number;
  totalSettledAmount: number;
  totalOutstandingAmount: number;
  totalCodCollected: number;
  totalCodPaidOut: number;
  activeBranchCount: number;
  activeCustomerCount: number;
}

export async function getDashboard(month?: string): Promise<DashboardMetrics> {
  const response = await http.get<DashboardMetrics>('/reports/dashboard', {
    params: { month },
  });
  return response.data;
}

/** 维度报表行（网点/线路/客户）。 */
export interface DimensionMetric {
  id: string;
  code: string;
  name: string;
  waybillCount: number;
  freightRevenue: number;
  totalWeightKg: number;
  totalVolumeM3: number;
}

export type ReportDimension = 'branches' | 'routes' | 'customers';

/** 租户用量（P10：仅供展示，无配额强制）。 */




/** 按维度查询运营报表，month 为 yyyyMM 可选。 */
export async function getDimensionReport(
  dimension: ReportDimension,
  month?: string,
): Promise<DimensionMetric[]> {
  const response = await http.get<DimensionMetric[]>(`/reports/${dimension}`, {
    params: { month },
  });
  return response.data;
}

// ===== P11：租户配额（TEN-002） =====
export interface TenantUsageWithQuota {
  waybillCount: number;
  userCount: number;
  fileCount: number;
  storageBytes: number;
  planName: string | null;
  maxWaybills: number | null;
  maxUsers: number | null;
  maxStorageBytes: number | null;
}

/** 租户用量（含套餐配额；Max 为 null = 不限制）。 */
export async function getTenantUsage(): Promise<TenantUsageWithQuota> {
  const response = await http.get<TenantUsageWithQuota>('/reports/usage');
  return response.data;
}

/** 设置租户套餐配额（quota.manage）。 */
export async function setTenantQuota(payload: {
  tenantId: string;
  planName?: string | null;
  maxWaybills?: number | null;
  maxUsers?: number | null;
  maxStorageBytes?: number | null;
}): Promise<void> {
  await http.put('/tenancy/quota', payload);
}
