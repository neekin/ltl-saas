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
