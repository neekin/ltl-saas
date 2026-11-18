import { PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Flex, Input, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { listWaybills, type WaybillListItem } from '../api/waybills';

/** 运单查询页（P4 前端）：按运单号检索 + 状态/金额/时间一览。查询逻辑保持原样。 */
export function WaybillListPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [waybillNo, setWaybillNo] = useState('');

  const listQuery = useQuery({
    queryKey: ['waybills', page, waybillNo],
    queryFn: () => listWaybills(page, 10, waybillNo || undefined),
  });

  const columns: ColumnsType<WaybillListItem> = [
    {
      title: '运单号',
      dataIndex: 'waybillNo',
      key: 'waybillNo',
      width: 170,
      render: (no: string) => <span className="ltl-code">{no}</span>,
    },
    {
      title: '运单状态',
      dataIndex: 'status',
      key: 'status',
      width: 110,
      render: (status: string) => <StatusTag status={status} category="waybill" />,
    },
    {
      title: '货物状态',
      dataIndex: 'cargoStatus',
      key: 'cargoStatus',
      width: 110,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '运输状态',
      dataIndex: 'transportStatus',
      key: 'transportStatus',
      width: 110,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '运费财务',
      dataIndex: 'freightStatus',
      key: 'freightStatus',
      width: 110,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '总费用',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      width: 130,
      align: 'right',
      render: (amount: number | null) =>
        amount === null ? (
          <Typography.Text type="secondary">未固化</Typography.Text>
        ) : (
          <span className="ltl-amount ltl-amount--strong">¥ {amount.toFixed(2)}</span>
        ),
    },
    {
      title: '开单时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      render: (v: string) => (
        <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text>
      ),
    },
    {
      title: '操作',
      key: 'actions',
      width: 80,
      fixed: 'right',
      render: (_: unknown, record: WaybillListItem) => (
        <Button type="link" size="small" onClick={() => navigate(`/waybills/${record.id}`)}>
          详情
        </Button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="运单查询"
        description="检索与管理全部运单，查看状态、费用与运输进度。"
        actions={
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => navigate('/waybills/create')}
          >
            开单
          </Button>
        }
      />

      <div className="ltl-filter-bar">
        <Flex gap={12} align="center" wrap="wrap">
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="输入运单号精确查询"
            value={waybillNo}
            onChange={(e) => setWaybillNo(e.target.value)}
            onPressEnter={() => {
              setWaybillNo(waybillNo.trim());
              setPage(1);
            }}
            style={{ width: 280 }}
            allowClear
          />
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              setWaybillNo('');
              setPage(1);
            }}
          >
            重置
          </Button>
        </Flex>
      </div>

      <Card styles={{ body: { padding: 0 } }}>
        <Table<WaybillListItem>
          rowKey="id"
          size="middle"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          scroll={{ x: 1100 }}
          locale={{
            emptyText: <EmptyState description="暂无运单，点击右上角「开单」创建第一张运单" />,
          }}
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
