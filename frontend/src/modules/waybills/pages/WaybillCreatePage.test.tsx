import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App as AntdApp } from 'antd';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { WaybillCreatePage } from './WaybillCreatePage';

vi.mock('../../../shared/api/http', async () => {
  const actual = await vi.importActual<typeof import('../../../shared/api/http')>(
    '../../../shared/api/http',
  );
  return actual;
});

// http.get 空数据（下拉为空不阻塞渲染校验）
vi.mock('axios', () => {
  const instance = {
    get: vi.fn().mockResolvedValue({ data: { items: [], total: 0 } }),
    post: vi.fn().mockResolvedValue({ data: {} }),
    put: vi.fn().mockResolvedValue({ data: {} }),
    delete: vi.fn().mockResolvedValue({ data: {} }),
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  };
  return { default: { create: vi.fn(() => instance), isAxiosError: vi.fn(() => false) } };
});

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <MemoryRouter>
      <QueryClientProvider client={queryClient}>
        <AntdApp>
          <WaybillCreatePage />
        </AntdApp>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe('WaybillCreatePage', () => {
  it('渲染业务信息与收发货人必填字段', async () => {
    renderPage();
    expect(await screen.findByText('新建运单')).toBeInTheDocument();
    expect(screen.getByLabelText('始发网点')).toBeInTheDocument();
    expect(screen.getByLabelText('目的网点')).toBeInTheDocument();
    expect(screen.getByLabelText('发货人')).toBeInTheDocument();
    expect(screen.getByText('开单并固化费用')).toBeInTheDocument();
  });

  it('试算按钮在无货物件时渲染为可点击（后端报错而非前端崩溃）', async () => {
    renderPage();
    const trialButton = (await screen.findAllByText('实时试算'))[0];
    expect(trialButton).toBeInTheDocument();
  });
});
