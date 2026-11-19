import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as reportsApi from '../api/reports';
import { DashboardPage } from './DashboardPage';

function renderWithQuery(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('DashboardPage', () => {
  it('渲染经营看板核心统计指标', async () => {
    vi.spyOn(reportsApi, 'getDashboard').mockResolvedValue({
      totalWaybills: 120,
      monthlyWaybills: 35,
      totalCargoWeightKg: 5400.5,
      totalCargoVolumeM3: 18.25,
      totalFreightRevenue: 24500,
      totalReceivableAmount: 24500,
      totalSettledAmount: 20000,
      totalOutstandingAmount: 4500,
      totalCodCollected: 3000,
      totalCodPaidOut: 1500,
      activeBranchCount: 4,
      activeCustomerCount: 18,
    });

    renderWithQuery(<DashboardPage />);
    expect(await screen.findByText('经营看板')).toBeInTheDocument();
    expect(screen.getByText('运单总量')).toBeInTheDocument();
    expect(screen.getByText('120')).toBeInTheDocument();
  });

  it('接口失败时渲染错误提示和重试按钮', async () => {
    vi.spyOn(reportsApi, 'getDashboard').mockRejectedValue(new Error('无权查看经营看板'));

    renderWithQuery(<DashboardPage />);
    expect(await screen.findByText('经营看板加载失败')).toBeInTheDocument();
    expect(screen.getByText('无权查看经营看板')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重 试' })).toBeInTheDocument();
  });
});
