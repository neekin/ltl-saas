import { CloudOutlined, FileTextOutlined, ReloadOutlined, UserOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Row, Statistic, Typography } from 'antd';
import { PageHeader } from '../../../shared/components';
import { getTenantUsage } from '../api/reports';

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

/** 租户用量页（P10）：统计展示，不涉及套餐与配额限制。 */
export function TenantUsagePage() {
  const query = useQuery({ queryKey: ['tenant-usage'], queryFn: getTenantUsage });
  const data = query.data;
  const updatedAt = query.dataUpdatedAt
    ? new Date(query.dataUpdatedAt).toLocaleString('zh-CN')
    : '尚未统计';

  return (
    <div>
      <PageHeader
        title="租户用量"
        description="进入页面时统计当前租户的资源使用情况；可手动刷新，不涉及套餐与配额限制。"
        actions={
          <Button
            icon={<ReloadOutlined />}
            loading={query.isFetching}
            onClick={() => void query.refetch()}
          >
            刷新统计
          </Button>
        }
      />

      {query.isError ? (
        <Alert
          type="error"
          showIcon
          message="用量统计加载失败"
          description={query.error?.message ?? '请稍后重试，未将失败结果显示为零用量。'}
          action={
            <Button size="small" onClick={() => void query.refetch()}>
              重试
            </Button>
          }
        />
      ) : (
        <>
          <Typography.Paragraph type="secondary">统计时间：{updatedAt}</Typography.Paragraph>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} lg={6}>
              <Card loading={query.isLoading}>
                <Statistic
                  title="运单数"
                  value={data?.waybillCount ?? 0}
                  prefix={<FileTextOutlined />}
                  suffix="票"
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card loading={query.isLoading}>
                <Statistic
                  title="用户数"
                  value={data?.userCount ?? 0}
                  prefix={<UserOutlined />}
                  suffix="人"
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card loading={query.isLoading}>
                <Statistic
                  title="文件数"
                  value={data?.fileCount ?? 0}
                  prefix={<CloudOutlined />}
                  suffix="个"
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card loading={query.isLoading}>
                <Statistic title="存储用量" value={formatBytes(data?.storageBytes ?? 0)} />
              </Card>
            </Col>
          </Row>
          <Card title="说明" style={{ marginTop: 16 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              用量数据由服务端按当前租户统计，包含全部历史运单与文件；文件存储用量基于已上传对象元数据。
            </Typography.Paragraph>
          </Card>
        </>
      )}
    </div>
  );
}
