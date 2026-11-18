import { useQuery } from '@tanstack/react-query';
import { Card, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';

interface ExceptionItem {
  id: string;
  waybillNo: string;
  deliveryStatus: string;
  cargoStatus: string;
}

/** 异常件工作台（P6 前端）：列出拒收/异常状态运单。 */
export function ExceptionWorkbenchPage() {
  const [page, setPage] = useState(1);

  const rejectedQuery = useQuery({
    queryKey: ['exception-waybills', page],
    queryFn: async () => {
      const response = await http.get<{ items: ExceptionItem[]; total: number }>('/waybills', {
        params: { page, pageSize: 50 },
      });
      return {
        ...response.data,
        items: response.data.items.filter((w) => w.deliveryStatus === 'Rejected'),
      };
    },
  });

  const columns: ColumnsType<ExceptionItem> = [
    {
      title: '运单号',
      dataIndex: 'waybillNo',
      width: 180,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    {
      title: '交付状态',
      dataIndex: 'deliveryStatus',
      width: 130,
      render: (s: string) => <StatusTag status={s} />,
    },
    {
      title: '货物状态',
      dataIndex: 'cargoStatus',
      width: 130,
      render: (s: string) => <StatusTag status={s} />,
    },
  ];

  return (
    <div>
      <PageHeader
        title="异常件工作台"
        description="列出拒收/异常运单，便于追踪责任网点与处理结果。"
      />
      <Card styles={{ body: { padding: 0 } }}>
        <Table<ExceptionItem>
          rowKey="id"
          loading={rejectedQuery.isLoading}
          dataSource={rejectedQuery.data?.items}
          columns={columns}
          size="middle"
          scroll={{ x: 600 }}
          locale={{ emptyText: <EmptyState description="暂无异常运单" /> }}
          pagination={{
            current: page,
            pageSize: 20,
            total: rejectedQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
          }}
        />
      </Card>
    </div>
  );
}
