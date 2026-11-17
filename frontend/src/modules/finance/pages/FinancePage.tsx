import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Input, InputNumber, Select, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { getReceivable, listPayments, recordPayment, type PaymentRecord, type Receivable } from '../api/finance';

/** 财务管理页（P7 前端）：应收查询 + 收款登记 + 退款。 */
export function FinancePage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [receivable, setReceivable] = useState<Receivable | null>(null);
  const [payments, setPayments] = useState<PaymentRecord[]>([]);
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState('cash');

  const search = async () => {
    try {
      const rv = await getReceivable(waybillId);
      setReceivable(rv);
      setPayments(await listPayments(waybillId));
    } catch {
      setReceivable(null);
      setPayments([]);
    }
  };

  const payMutation = useMutation({
    mutationFn: () => recordPayment({ waybillId, amount: payAmount, method: payMethod }),
    onSuccess: () => {
      message.success('收款成功');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>财务管理</Typography.Title>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="输入运单 ID"
          value={waybillId}
          onChange={(e) => setWaybillId(e.target.value)}
          onSearch={search}
          style={{ width: 350 }}
        />
      </Space>

      {receivable && (
        <Card title="应收账款" style={{ marginBottom: 16 }}>
          <Space wrap>
            <Typography.Text>应收 <strong>{receivable.amount.toFixed(2)}</strong> 元</Typography.Text>
            <Typography.Text>已收 <strong>{receivable.settledAmount.toFixed(2)}</strong> 元</Typography.Text>
            <Typography.Text>未收 <strong>{receivable.outstanding.toFixed(2)}</strong> 元</Typography.Text>
            <Tag color={receivable.status === 'Settled' ? 'green' : receivable.status === 'PartiallyPaid' ? 'orange' : 'red'}>
              {receivable.status}
            </Tag>
          </Space>
        </Card>
      )}

      {receivable && receivable.status !== 'Settled' && (
        <Card title="收款登记" style={{ marginBottom: 16 }}>
          <Space wrap>
            <InputNumber min={0.01} value={payAmount} onChange={(v) => setPayAmount(v ?? 0)} addonBefore="金额" style={{ width: 160 }} />
            <Select value={payMethod} onChange={setPayMethod} style={{ width: 130 }} options={[
              { value: 'cash', label: '现付' },
              { value: 'ondelivery', label: '到付' },
              { value: 'monthly', label: '月结' },
            ]} />
            <Button type="primary" onClick={() => payMutation.mutate()} loading={payMutation.isPending}>
              收款
            </Button>
          </Space>
        </Card>
      )}

      {payments.length > 0 && (
        <Card title="收款记录">
          <Table
            rowKey="id"
            size="small"
            dataSource={payments}
            pagination={false}
            columns={[
              { title: '金额', dataIndex: 'amount', render: (v: number) => `${v.toFixed(2)} 元` },
              { title: '方式', dataIndex: 'method' },
              { title: '时间', dataIndex: 'occurredAt', render: (v: string) => new Date(v).toLocaleString('zh-CN') },
            ]}
          />
        </Card>
      )}
    </div>
  );
}

