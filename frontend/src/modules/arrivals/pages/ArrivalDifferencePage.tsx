import { Card, Input, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { getArrivalByTrip, type ArrivalDto } from '../api/arrivals';

/** 到货差异处理页（P6 前端）：按批次查看到货记录 + 差异明细。 */
export function ArrivalDifferencePage() {
  const [tripId, setTripId] = useState('');
  const [arrival, setArrival] = useState<ArrivalDto | null>(null);
  const [searched, setSearched] = useState(false);

  const search = async () => {
    try {
      setArrival(await getArrivalByTrip(tripId));
    } catch {
      setArrival(null);
    }
    setSearched(true);
  };

  return (
    <div>
      <Typography.Title level={4}>到货差异处理</Typography.Title>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="输入运输批次 ID"
          value={tripId}
          onChange={(e) => setTripId(e.target.value)}
          onSearch={search}
          style={{ width: 400 }}
        />
      </Space>
      {searched && !arrival && (
        <Typography.Text type="secondary">该批次无到货记录</Typography.Text>
      )}
      {arrival && (
        <Card title={`到货记录（到达网点 ${arrival.branchId}）`}>
          <Table
            rowKey={(r) => r.waybillId}
            size="small"
            dataSource={arrival.items}
            pagination={false}
            columns={[
              { title: '运单 ID', dataIndex: 'waybillId', ellipsis: true },
              { title: '装车件数', dataIndex: 'loadedPieces', width: 100 },
              { title: '实收件数', dataIndex: 'receivedPieces', width: 100 },
              { title: '破损', dataIndex: 'brokenPieces', width: 80 },
              {
                title: '差异',
                key: 'diff',
                render: (_: unknown, record: { waybillId: string; receivedPieces: number; loadedPieces: number }) => {
                  const diff = record.receivedPieces - record.loadedPieces;
                  return diff === 0 ? (
                    <Tag color="green">无差异</Tag>
                  ) : (
                    <Tag color="red">短少 {-diff} 件</Tag>
                  );
                },
              },
              { title: '备注', dataIndex: 'remark' },
            ]}
          />
        </Card>
      )}
    </div>
  );
}
