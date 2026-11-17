import { useQuery } from '@tanstack/react-query';
import { Table, Tag, Typography } from 'antd';
import { useState } from 'react';
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

  return (
    <div>
      <Typography.Title level={4}>异常件工作台</Typography.Title>
      <Typography.Paragraph type="secondary">
        列出拒收/差异运单，追踪责任网点和处理结果。
      </Typography.Paragraph>
      <Table
        rowKey="id"
        loading={rejectedQuery.isLoading}
        dataSource={rejectedQuery.data?.items}
        pagination={{ current: page, pageSize: 20, total: rejectedQuery.data?.total, onChange: setPage }}
        columns={[
          { title: '运单号', dataIndex: 'waybillNo' },
          {
            title: '交付状态',
            dataIndex: 'deliveryStatus',
            render: (s: string) => <Tag color="red">{s}</Tag>,
          },
          { title: '货物状态', dataIndex: 'cargoStatus' },
        ]}
      />
    </div>
  );
}
