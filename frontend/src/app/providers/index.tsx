import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App as AntdApp, ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import dayjs from 'dayjs';
import 'dayjs/locale/zh-cn';
import { RouterProvider } from 'react-router-dom';
import { router } from '../router';
import { appTheme } from './theme';

dayjs.locale('zh-cn');

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

/**
 * 全局 Provider 组合根：
 * - QueryClientProvider：服务端数据状态（AGENTS.md §9.2）
 * - ConfigProvider：Ant Design 中文语言环境（界面按 Asia/Shanghai 业务语境展示）
 * - AntdApp：统一的 message / modal / notification 上下文
 * - RouterProvider：React Router
 */
export function AppProviders() {
  return (
    <ConfigProvider locale={zhCN} theme={appTheme}>
      <AntdApp>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </AntdApp>
    </ConfigProvider>
  );
}
