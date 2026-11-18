import { CloudOutlined, FileTextOutlined, ReloadOutlined, UserOutlined } from '@ant-design/icons';
import { Progress, Tag } from 'antd';
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

/** 租户用量页（P10/TEN-002）：用量统计 + 套餐配额进度展示。 */
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
        description="进入页面时统计当前租户的资源使用情况；超过套餐配额将限制开单/建用户/上传。"
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
          <Card title="套餐配额（TEN-002）" style={{ marginTop: 16 }}>
            {data?.planName && (
              <Typography.Paragraph>
                当前套餐：<Tag color="blue">{data.planName}</Tag>
              </Typography.Paragraph>
            )}
            <QuotaRow label="运单数" current={data?.waybillCount ?? 0} max={data?.maxWaybills ?? null} unit="票" />
            <QuotaRow label="用户数" current={data?.userCount ?? 0} max={data?.maxUsers ?? null} unit="人" />
            <QuotaRow
              label="存储"
              current={data?.storageBytes ?? 0}
              max={data?.maxStorageBytes ?? null}
              unit={formatBytes(data?.storageBytes ?? 0)}
              maxUnit={data?.maxStorageBytes != null ? formatBytes(data.maxStorageBytes) : null}
            />
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8 }}>
              未显示进度条的配额项表示套餐未设置上限；配额由「系统 → 租户配额」设置。
            </Typography.Paragraph>
          </Card>
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

function QuotaRow({
  label, current, max, unit, maxUnit,
}: {
  label: string;
  current: number;
  max: number | null;
  unit: string;
  maxUnit?: string | null;
}) {
  if (max === null) {
    return (
      <div style={{ marginBottom: 12 }}>
        <Typography.Text strong>{label}</Typography.Text>{' '}
        <Typography.Text type="secondary">{unit} / 不限制</Typography.Text>
      </div>
    );
  }

  const percent = max > 0 ? Math.min(100, Math.round((current / max) * 100)) : 100;
  return (
    <div style={{ marginBottom: 12 }}>
      <div>
        <Typography.Text strong>{label}</Typography.Text>{' '}
        <Typography.Text type="secondary">
          {unit} / {maxUnit ?? max}（{percent}%）
        </Typography.Text>
        {percent >= 100 && <Tag color="red">已满</Tag>}
      </div>
      <Progress
        percent={percent}
        status={percent >= 100 ? 'exception' : percent >= 80 ? 'active' : 'normal'}
        showInfo={false}
      />
    </div>
  );
}
