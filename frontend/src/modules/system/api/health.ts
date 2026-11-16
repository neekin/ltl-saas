import axios from 'axios';

export interface SystemStatus {
  name: string;
  status: string;
}

/**
 * 健康检查属于平台级端点（非 /api 业务资源），直接请求根路径。
 */
export async function fetchSystemStatus(): Promise<SystemStatus> {
  const { data } = await axios.get<SystemStatus>('/health');
  return data;
}
