import { PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Flex, Input, Popconfirm, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { listRoutes, type RouteItem } from '../api/routes';

/** 线路列表页（P2 前端）。 */
export function RouteListPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');

  const listQuery = useQuery({
    queryKey: ['routes', page, keyword],
    queryFn: () => listRoutes(page, 10, keyword || undefined),
  });

  const columns: ColumnsType<RouteItem> = [
    { title: '编码', dataIndex: 'code', key: 'code', width: 130, render: (v: string) => <span className="ltl-code">{v}</span> },
    { title: '名称', dataIndex: 'name', key: 'name' },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '操作',
      key: 'actions',
      width: 160,
      fixed: 'right',
      render: (_: unknown, record: RouteItem) => (
        <Button type="link" size="small" onClick={() => navigate(`/routes/${record.id}`)}>
          编辑（站点/方案）
        </Button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="线路管理"
        description="维护业务线路及其站点、运输方案。"
        actions={
          <Popconfirm title="新建线路请在编辑页完善站点与运输方案" disabled>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/routes/new')}>
              新建线路
            </Button>
          </Popconfirm>
        }
      />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="搜索编码/名称"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onPressEnter={() => {
              setKeyword(keyword.trim());
              setPage(1);
            }}
            style={{ width: 280 }}
            allowClear
          />
          <Button icon={<ReloadOutlined />} onClick={() => listQuery.refetch()} />
        </Flex>
      </div>

      <Card styles={{ body: { padding: 0 } }}>
        <Table<RouteItem>
          rowKey="id"
          columns={columns}
          dataSource={listQuery.data?.items}
          loading={listQuery.isLoading}
          size="middle"
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState description="暂无线路" /> }}
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
