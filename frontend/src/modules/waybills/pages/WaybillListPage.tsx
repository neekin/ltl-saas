import { PlusOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Input, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { listWaybills } from '../api/waybills';

function statusTag(status: string) {
  const map: Record<string, { color: string; text: string }> = {
    Draft: { color: 'orange', text: '草稿' },
    Confirmed: { color: 'green', text: '已确认' },
    Cancelled: { color: 'default', text: '已取消' },
  };
  const item = map[status];
  return item ? <Tag color={item.color}>{item.text}</Tag> : <Tag>{status}</Tag>;
}

function cargoTag(status: string) {
  const map: Record<string, string> = {
    PendingReceive: '待收货',
    Received: '已收货',
    Sorted: '已分拣',
  };
  return <Tag>{map[status] ?? status}</Tag>;
}

/** 运单列表页（P4 前端）。 */
export function WaybillListPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [waybillNo, setWaybillNo] = useState('');

  const listQuery = useQuery({
    queryKey: ['waybills', page, waybillNo],
    queryFn: () => listWaybills(page, 10, waybillNo || undefined),
  });

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="按运单号搜索"
          value={waybillNo}
          onChange={(e) => setWaybillNo(e.target.value)}
          onSearch={(value) => {
            setWaybillNo(value);
            setPage(1);
          }}
          style={{ width: 260 }}
          allowClear
        />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/waybills/create')}>
          开单
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        pagination={{ current: page, pageSize: 10, total: listQuery.data?.total, onChange: setPage }}
        columns={[
          { title: '运单号', dataIndex: 'waybillNo', key: 'waybillNo' },
          {
            title: '运单状态',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => statusTag(status),
          },
          {
            title: '货物状态',
            dataIndex: 'cargoStatus',
            key: 'cargoStatus',
            render: (status: string) => cargoTag(status),
          },
          {
            title: '运费财务',
            dataIndex: 'freightStatus',
            key: 'freightStatus',
            render: (status: string) => {
              const map: Record<string, string> = { Unpaid: '未收款', PartiallyPaid: '部分收款', Settled: '已结清' };
              return <Tag>{map[status] ?? status}</Tag>;
            },
          },
          {
            title: '总费用',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            render: (amount: number | null) => (amount === null ? '—' : `${amount.toFixed(2)} 元`),
          },
          {
            title: '操作',
            key: 'actions',
            render: (_: unknown, record: { id: string }) => (
              <Button type="link" size="small" onClick={() => navigate(`/waybills/${record.id}`)}>
                详情
              </Button>
            ),
          },
        ]}
      />
    </div>
  );
}
