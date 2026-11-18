import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Select, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { listNotifications, type NotificationRecord } from '../api/notifications';

const EVENT_TYPES = [
  { value: '', label: '全部事件' },
  { value: 'waybill.received', label: '收货登记' },
  { value: 'waybill.departed', label: '发车运输' },
  { value: 'waybill.arrived', label: '到达目的网点' },
  { value: 'waybill.delivered', label: '签收' },
];

/** 通知记录页（NOTIFY-001）：运单关键事件自动生成的通知，仅查询。 */
export function NotificationListPage() {
  const [page, setPage] = useState(1);
  const [eventType, setEventType] = useState('');

  const query = useQuery({
    queryKey: ['notifications', page, eventType],
    queryFn: () => listNotifications(page, 20, eventType || undefined),
  });

  const columns: ColumnsType<NotificationRecord> = [
    {
      title: '通道',
      dataIndex: 'channel',
      width: 90,
      render: (v: string) => <Tag>{v}</Tag>,
    },
    { title: '事件', dataIndex: 'eventType', width: 150 },
    {
      title: '收件人',
      dataIndex: 'recipient',
      width: 150,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    { title: '内容', dataIndex: 'content', ellipsis: true },
    {
      title: '状态',
      dataIndex: 'status',
      width: 110,
      render: (v: string) => (
        <StatusTag status={v === 'Sent' ? 'Active' : v === 'Failed' ? 'Rejected' : 'Draft'} />
      ),
    },
    {
      title: '时间',
      dataIndex: 'createdAt',
      width: 170,
      render: (v: string) => (
        <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="通知记录"
        description="运单收货/发车/到达/签收事件自动生成的客户通知（NOTIFY-001）；短信/微信服务商接入前以日志通道落记录。"
      />

      <Card styles={{ body: { padding: 0 } }}>
        <div className="ltl-filter-bar" style={{ padding: 16 }}>
          <Select
            style={{ width: 200 }}
            value={eventType}
            options={EVENT_TYPES}
            onChange={(v) => {
              setEventType(v);
              setPage(1);
            }}
          />
          <Button
            loading={query.isFetching && !query.isError}
            onClick={() => void query.refetch()}
          >
            刷新
          </Button>
        </div>

        {query.isError ? (
          <Alert
            type="error"
            showIcon
            message="通知记录加载失败"
            description={query.error?.message ?? '请稍后重试。'}
            action={
              <Button size="small" onClick={() => void query.refetch()}>
                重试
              </Button>
            }
            style={{ margin: 16 }}
          />
        ) : (
          <Table<NotificationRecord>
            rowKey="id"
            loading={query.isLoading}
            dataSource={query.data?.items}
            columns={columns}
            size="middle"
            scroll={{ x: 850 }}
            locale={{ emptyText: <EmptyState description="暂无通知记录" /> }}
            pagination={{
              current: page,
              pageSize: 20,
              total: query.data?.total ?? 0,
              onChange: setPage,
              showTotal: (total) => `共 ${total} 条`,
              showSizeChanger: false,
            }}
          />
        )}
      </Card>
    </div>
  );
}
