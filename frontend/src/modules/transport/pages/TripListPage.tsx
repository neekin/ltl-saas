import { useQuery } from '@tanstack/react-query';
import { Button, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { listTrips, type Trip } from '../api/trips';

function statusTag(status: string) {
  const map: Record<string, { color: string; text: string }> = {
    Planned: { color: 'orange', text: '已计划' },
    Departed: { color: 'blue', text: '已发车' },
    Arrived: { color: 'green', text: '已到达' },
  };
  const item = map[status];
  return item ? <Tag color={item.color}>{item.text}</Tag> : <Tag>{status}</Tag>;
}

/** 运输批次列表页（P5 前端）。 */
export function TripListPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);

  const listQuery = useQuery({ queryKey: ['trips', page], queryFn: () => listTrips(page, 10) });

  const columns: ColumnsType<Trip> = [
    { title: '批次号', dataIndex: 'tripNo' },
    {
      title: '状态',
      dataIndex: 'status',
      render: (status) => statusTag(status),
    },
    {
      title: '配载运单',
      key: 'waybills',
      render: (_, record) => `${record.waybills.filter((w) => w.active).length} 票在车`,
    },
    {
      title: '操作',
      key: 'actions',
      render: (_, record) => (
        <Button type="link" size="small" onClick={() => navigate(`/transport/trips/${record.id}`)}>
          配载工作台
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Typography.Title level={4}>运输批次</Typography.Title>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        pagination={{ current: page, pageSize: 10, total: listQuery.data?.total, onChange: setPage }}
        columns={columns}
      />
    </div>
  );
}
