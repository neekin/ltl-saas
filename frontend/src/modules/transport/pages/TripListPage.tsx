import { useQuery } from '@tanstack/react-query';
import { Button, Card, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { listTrips, type Trip } from '../api/trips';

/** 运输批次列表页（P5 前端）：批次状态、配载量一览。查询逻辑保持原样。 */
export function TripListPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);

  const listQuery = useQuery({ queryKey: ['trips', page], queryFn: () => listTrips(page, 10) });

  const columns: ColumnsType<Trip> = [
    {
      title: '批次号',
      dataIndex: 'tripNo',
      key: 'tripNo',
      width: 180,
      render: (no: string) => <span className="ltl-code">{no}</span>,
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (status: string) => <StatusTag status={status} category="trip" />,
    },
    {
      title: '配载运单',
      key: 'waybills',
      width: 130,
      render: (_, record) => `${record.waybills.filter((w) => w.active).length} 票在车`,
    },
    {
      title: '开单时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (v: string) => <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text>,
    },
    {
      title: '操作',
      key: 'actions',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
        <Button type="link" size="small" onClick={() => navigate(`/transport/trips/${record.id}`)}>
          配载工作台
        </Button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="运输批次" description="管理发车批次与配载运单，进入工作台完成装车、发车与到达登记。" />
      <Card styles={{ body: { padding: 0 } }}>
        <Table<Trip>
          rowKey="id"
          size="middle"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          scroll={{ x: 800 }}
          locale={{ emptyText: <EmptyState description="暂无运输批次" /> }}
          pagination={{
            current: page,
            pageSize: 10,
            total: listQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
            showSizeChanger: false,
          }}
        />
      </Card>
    </div>
  );
}
