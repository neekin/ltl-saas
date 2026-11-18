import { useQuery } from '@tanstack/react-query';
import { CloudOutlined, FileTextOutlined, UserOutlined } from '@ant-design/icons';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import { PageHeader } from '../../../shared/components';
import { getTenantUsage } from '../api/reports';

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

/** 租户用量页（P10）：展示当前租户的运单数、用户数与文件存储用量（无配额强制）。 */
export function TenantUsagePage() {
  const { data, isLoading } = useQuery({ queryKey: ['tenant-usage'], queryFn: getTenantUsage });

  return (
    <div>
      <PageHeader
        title="租户用量"
        description="当前租户的资源使用情况（统计展示，不涉及套餐与配额限制）。"
      />
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={isLoading}>
            <Statistic
              title="运单数"
              value={data?.waybillCount ?? 0}
              prefix={<FileTextOutlined />}
              suffix="票"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={isLoading}>
            <Statistic
              title="用户数"
              value={data?.userCount ?? 0}
              prefix={<UserOutlined />}
              suffix="人"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={isLoading}>
            <Statistic
              title="文件数"
              value={data?.fileCount ?? 0}
              prefix={<CloudOutlined />}
              suffix="个"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card loading={isLoading}>
            <Statistic title="存储用量" value={formatBytes(data?.storageBytes ?? 0)} />
          </Card>
        </Col>
      </Row>
      <Card title="说明" style={{ marginTop: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          用量数据由服务端按当前租户实时统计，包含全部历史运单与文件；文件系统统计基于已上传对象元数据。
        </Typography.Paragraph>
      </Card>
    </div>
  );
}
