import { SearchOutlined } from '@ant-design/icons';
import { Card, Flex, Input, Table, Tag } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader } from '../../../shared/components';
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
      <PageHeader
        title="到货差异处理"
        description="按运输批次查看到货记录，核对装车与实收件数差异。"
      />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="输入运输批次 ID"
            value={tripId}
            onChange={(e) => setTripId(e.target.value)}
            onPressEnter={search}
            style={{ width: 360 }}
          />
        </Flex>
      </div>

      {searched && !arrival && (
        <Card>
          <EmptyState description="该批次无到货记录" />
        </Card>
      )}
      {arrival && (
        <Card title={`到货记录（到达网点 ${arrival.branchId}）`}>
          <Table
            rowKey={(r) => r.waybillId}
            size="small"
            dataSource={arrival.items}
            pagination={false}
            scroll={{ x: 800 }}
            locale={{ emptyText: <EmptyState simple description="暂无到货明细" /> }}
            columns={[
              {
                title: '运单 ID',
                dataIndex: 'waybillId',
                ellipsis: true,
                render: (v: string) => <span className="ltl-code">{v}</span>,
              },
              { title: '装车件数', dataIndex: 'loadedPieces', width: 110, align: 'right' },
              { title: '实收件数', dataIndex: 'receivedPieces', width: 110, align: 'right' },
              { title: '破损', dataIndex: 'brokenPieces', width: 90, align: 'right' },
              {
                title: '差异',
                key: 'diff',
                width: 120,
                render: (
                  _: unknown,
                  record: { waybillId: string; receivedPieces: number; loadedPieces: number },
                ) => {
                  const diff = record.receivedPieces - record.loadedPieces;
                  return diff === 0 ? (
                    <Tag color="success">无差异</Tag>
                  ) : (
                    <Tag color="error">短少 {-diff} 件</Tag>
                  );
                },
              },
              { title: '备注', dataIndex: 'remark', render: (v: string | null) => v ?? '—' },
            ]}
          />
        </Card>
      )}
      {!searched && (
        <Card>
          <EmptyState description="请输入运输批次 ID 查询到货差异" />
        </Card>
      )}
    </div>
  );
}
