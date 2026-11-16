import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SystemStatusPage } from './SystemStatusPage';

vi.mock('../api/health', () => ({
  fetchSystemStatus: vi.fn(),
}));

const { fetchSystemStatus } = await import('../api/health');
const mockedFetch = vi.mocked(fetchSystemStatus);

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SystemStatusPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SystemStatusPage', () => {
  beforeEach(() => {
    mockedFetch.mockReset();
  });

  it('后端健康检查成功时展示成功状态与服务信息', async () => {
    mockedFetch.mockResolvedValueOnce({ name: 'LTL SaaS API', status: 'ok' });

    renderPage();

    expect(await screen.findByText('后端服务正常')).toBeInTheDocument();
    expect(screen.getByText('LTL SaaS API')).toBeInTheDocument();
  });

  it('后端不可达时展示明确错误状态', async () => {
    mockedFetch.mockRejectedValueOnce(new Error('网络错误'));

    renderPage();

    expect(await screen.findByText('无法连接后端服务')).toBeInTheDocument();
  });
});
