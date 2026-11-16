import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Descriptions, Spin, Typography } from 'antd';
import { fetchSystemStatus } from '../api/health';

/**
 * 系统状态页：验证前后端通过健康检查接口联通（P0 验收标准）。
 * 覆盖加载、错误、成功三种状态（AGENTS.md §9.4）。
 */
export function SystemStatusPage() {
  const { data, isPending, isError, error } = useQuery({
    queryKey: ['system-status'],
    queryFn: fetchSystemStatus,
  });

  if (isPending) {
    return (
      <Card loading title="系统状态">
        <Spin tip="正在检查后端服务…" />
      </Card>
    );
  }

  if (isError) {
    return (
      <Card title="系统状态">
        <Alert
          type="error"
          showIcon
          title="无法连接后端服务"
          description={error instanceof Error ? error.message : String(error)}
        />
      </Card>
    );
  }

  return (
    <Card title="系统状态">
      <Alert type="success" showIcon title="后端服务正常" style={{ marginBottom: 16 }} />
      <Descriptions column={1} size="small">
        <Descriptions.Item label="服务名称">{data.name}</Descriptions.Item>
        <Descriptions.Item label="健康状态">{data.status}</Descriptions.Item>
      </Descriptions>
      <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
        前后端已通过健康检查接口联通。
      </Typography.Paragraph>
    </Card>
  );
}
