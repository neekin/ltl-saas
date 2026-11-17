import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Input, Popconfirm, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
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

  const columns = [
    { title: '编码', dataIndex: 'code', key: 'code' },
    { title: '名称', dataIndex: 'name', key: 'name' },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <Tag color={status === 'Active' ? 'green' : 'default'}>{status === 'Active' ? '启用' : '停用'}</Tag>,
    },
    {
      title: '操作',
      key: 'actions',
      render: (_: unknown, record: RouteItem) => (
        <Button type="link" size="small" onClick={() => navigate(`/routes/${record.id}`)}>
          编辑（站点/方案）
        </Button>
      ),
    },
  ];

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="搜索编码/名称"
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          onSearch={(value) => {
            setKeyword(value);
            setPage(1);
          }}
          style={{ width: 260 }}
          allowClear
        />
        <Button icon={<ReloadOutlined />} onClick={() => listQuery.refetch()} />
        <Popconfirm
          title="新建线路请在编辑页完善站点与运输方案"
          disabled
        >
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate(`/routes/new`)}>
            新建线路
          </Button>
        </Popconfirm>
      </Space>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={listQuery.data?.items}
        loading={listQuery.isLoading}
        pagination={{ current: page, pageSize: 10, total: listQuery.data?.total, onChange: setPage }}
      />
    </div>
  );
}

