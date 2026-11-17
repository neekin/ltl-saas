import { useQuery } from '@tanstack/react-query';
import { Table, Tag, Typography } from 'antd';
import { useState } from 'react';
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
      <Typography.Title level={4}>待自提 / 配送任务</Typography.Title>
      <Typography.Paragraph type="secondary">
        已到货但尚未交付的运单。创建交付单请前往交付确认页。
      </Typography.Paragraph>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        pagination={{ current: page, pageSize: 20, total: listQuery.data?.total, onChange: setPage }}
        columns={[
          { title: '运单号', dataIndex: 'waybillNo' },
          {
            title: '货物状态',
            dataIndex: 'cargoStatus',
            render: (s: string) => {
              const map: Record<string, string> = { Received: '已收货', Sorted: '已分拣' };
              return <Tag>{map[s] ?? s}</Tag>;
            },
          },
          {
            title: '交付状态',
            dataIndex: 'deliveryStatus',
            render: (s: string) => <Tag>{s}</Tag>,
          },
        ]}
      />
    </div>
  );
}
