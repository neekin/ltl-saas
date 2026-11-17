import { SearchOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Flex, Input, InputNumber, Popconfirm, Row, Select, Space, Table, Typography } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { getReceivable, listPayments, recordPayment, refundPayment, type PaymentRecord, type Receivable } from '../api/finance';

/** 收款核销界面（P7 前端）：应收 + 收款记录 + 部分收款 + 退款。 */
export function PaymentAllocationPage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [receivable, setReceivable] = useState<Receivable | null>(null);
  const [payments, setPayments] = useState<PaymentRecord[]>([]);
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState('cash');

  const search = async () => {
    try {
      setReceivable(await getReceivable(waybillId));
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

  const refundMutation = useMutation({
    mutationFn: (paymentId: string) => refundPayment(paymentId, payAmount, '退款'),
    onSuccess: () => {
      message.success('退款成功');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <PageHeader title="收款核销" description="查询应收、登记收款核销，并对收款记录执行退款冲正。" />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="输入运单 ID 查询应收"
            value={waybillId}
            onChange={(e) => setWaybillId(e.target.value)}
            onPressEnter={search}
            style={{ width: 360 }}
          />
          <Button type="primary" onClick={search}>查询</Button>
        </Flex>
      </div>

      {!receivable && <Card><EmptyState description="输入运单 ID 查询应收账款" /></Card>}

      {receivable && (
        <>
          <Card title="应收账款" style={{ marginBottom: 16 }}>
            <Row gutter={[16, 16]}>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>应收金额</div>
                <div className="ltl-amount ltl-amount--strong" style={{ fontSize: 22 }}>¥ {receivable.amount.toFixed(2)}</div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>已收金额</div>
                <div className="ltl-amount" style={{ fontSize: 22 }}>¥ {receivable.settledAmount.toFixed(2)}</div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>未收金额</div>
                <div className="ltl-amount" style={{ fontSize: 22, color: '#C0392B' }}>¥ {receivable.outstanding.toFixed(2)}</div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12, marginBottom: 4 }}>状态</div>
                <StatusTag status={receivable.status} />
              </Col>
            </Row>
          </Card>

          {receivable.status !== 'Settled' && (
            <Card title="收款登记" style={{ marginBottom: 16 }}>
              <Space wrap>
                <InputNumber min={0.01} precision={2} value={payAmount} onChange={(v) => setPayAmount(v ?? 0)} addonBefore="金额（元）" style={{ width: 180 }} />
                <Select value={payMethod} onChange={setPayMethod} style={{ width: 130 }} options={[
                  { value: 'cash', label: '现付' },
                  { value: 'ondelivery', label: '到付' },
                  { value: 'monthly', label: '月结' },
                ]} />
                <Button type="primary" onClick={() => payMutation.mutate()} loading={payMutation.isPending}>收款</Button>
              </Space>
            </Card>
          )}

          <Card title="收款记录（追加式，退款以反向流水冲正）">
            <Table
              rowKey="id"
              size="small"
              dataSource={payments}
              pagination={false}
              scroll={{ x: 700 }}
              locale={{ emptyText: <EmptyState simple description="暂无收款记录" /> }}
              columns={[
                { title: '金额', dataIndex: 'amount', width: 140, align: 'right', render: (v: number) => <span className="ltl-amount">¥ {v.toFixed(2)}</span> },
                { title: '方式', dataIndex: 'method', width: 120 },
                { title: '时间', dataIndex: 'occurredAt', width: 180, render: (v: string) => <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text> },
                {
                  title: '退款',
                  key: 'refund',
                  width: 90,
                  render: (_: unknown, record: PaymentRecord) => (
                    <Popconfirm title="确认退款？" onConfirm={() => refundMutation.mutate(record.id)}>
                      <Button size="small" danger>退款</Button>
                    </Popconfirm>
                  ),
                },
              ]}
            />
          </Card>
        </>
      )}
    </div>
  );
}
