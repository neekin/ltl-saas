import { useQuery } from '@tanstack/react-query';
import { Card, Table } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';

interface WaybillItem {
  id: string;
  waybillNo: string;
  status: string;
  cargoStatus: string;
  deliveryStatus: string;
}

/** 待自提货物列表 + 配送任务列表（P6 前端）。 */
export function PickupDeliveryPage() {
  const [page, setPage] = useState(1);

  const listQuery = useQuery({
    queryKey: ['pickup-waybills', page],
    queryFn: async () => {
      const response = await http.get<{ items: WaybillItem[]; total: number }>('/waybills', {
        params: { page, pageSize: 50 },
      });
      return {
        ...response.data,
        items: response.data.items.filter(
          (w) => w.deliveryStatus === 'NotDelivered' && w.cargoStatus !== 'PendingReceive',
        ),
      };
    },
  });

  return (
    <div>
      <PageHeader title="待自提 / 配送任务" description="已到货但尚未交付的运单。创建交付单请前往交付确认页。" />
      <Card styles={{ body: { padding: 0 } }}>
        <Table<WaybillItem>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          size="middle"
          scroll={{ x: 650 }}
          locale={{ emptyText: <EmptyState description="暂无待交付运单" /> }}
          pagination={{
            current: page,
            pageSize: 20,
            total: listQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
          }}
          columns={[
            { title: '运单号', dataIndex: 'waybillNo', width: 180, render: (v: string) => <span className="ltl-code">{v}</span> },
            {
              title: '货物状态',
              dataIndex: 'cargoStatus',
              width: 130,
              render: (s: string) => <StatusTag status={s} />,
            },
            {
              title: '交付状态',
              dataIndex: 'deliveryStatus',
              width: 130,
              render: (s: string) => <StatusTag status={s} />,
            },
          ]}
        />
      </Card>
    </div>
  );
}
