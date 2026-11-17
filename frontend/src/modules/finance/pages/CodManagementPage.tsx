import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Input, InputNumber, Popconfirm, Space, Typography } from 'antd';
import { useState } from 'react';
import { approveCodSettlement, createCodSettlement, getCodOrder, payCodSettlement, type CodOrder } from '../api/finance';

/** COD 管理页（P8 前端）：COD 收款 + 结算审批 + 付款。 */
export function CodManagementPage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [codOrder, setCodOrder] = useState<CodOrder | null>(null);
  const [settleAmount, setSettleAmount] = useState(0);

  const search = async () => {
    try {
      setCodOrder(await getCodOrder(waybillId));
    } catch {
      setCodOrder(null);
    }
  };

  const settleMutation = useMutation({
    mutationFn: () => createCodSettlement(waybillId, settleAmount),
    onSuccess: () => {
      message.success('结算单已创建');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const approveMutation = useMutation({
    mutationFn: (id: string) => approveCodSettlement(id),
    onSuccess: () => {
      message.success('已审批');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const payMutation = useMutation({
    mutationFn: (id: string) => payCodSettlement(id),
    onSuccess: () => {
      message.success('已付款给发货人');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>COD 管理</Typography.Title>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="输入运单 ID"
          value={waybillId}
          onChange={(e) => setWaybillId(e.target.value)}
          onSearch={search}
          style={{ width: 350 }}
        />
      </Space>

      {codOrder && (
        <Card title={`COD 订单（${codOrder.status}）`} style={{ marginBottom: 16 }}>
          <Space wrap>
            <Typography.Text>代收金额 <strong>{codOrder.amount.toFixed(2)}</strong></Typography.Text>
            <Typography.Text>已收 <strong>{codOrder.collectedAmount.toFixed(2)}</strong></Typography.Text>
            <Typography.Text>已结算 <strong>{codOrder.settledAmount.toFixed(2)}</strong></Typography.Text>
            <Typography.Text>已付 <strong>{codOrder.paidAmount.toFixed(2)}</strong></Typography.Text>
          </Space>
        </Card>
      )}

      {codOrder && codOrder.collectedAmount > 0 && codOrder.settledAmount < codOrder.collectedAmount && (
        <Card title="创建结算单" style={{ marginBottom: 16 }}>
          <Space>
            <Typography.Text>结算金额（≤ {codOrder.collectedAmount - codOrder.settledAmount}）：</Typography.Text>
            <InputNumber
              min={0.01}
              max={codOrder.collectedAmount - codOrder.settledAmount}
              value={settleAmount}
              onChange={(v) => setSettleAmount(v ?? 0)}
              style={{ width: 150 }}
            />
            <Button type="primary" onClick={() => settleMutation.mutate()} loading={settleMutation.isPending}>
              创建结算
            </Button>
          </Space>
        </Card>
      )}

      {codOrder && codOrder.settledAmount > 0 && codOrder.paidAmount < codOrder.settledAmount && (
        <Card title="审批和付款" style={{ marginBottom: 16 }}>
          <Typography.Paragraph type="secondary">
            结算单已创建并计入已结算金额；审批和付款操作需 COD 管理员执行。
          </Typography.Paragraph>
          <Space>
            <Popconfirm title="确认审批该结算单？" onConfirm={() => approveMutation.mutate(codOrder.id)}>
              <Button type="primary">审批</Button>
            </Popconfirm>
            <Popconfirm title="确认付款给发货人？" onConfirm={() => payMutation.mutate(codOrder.id)}>
              <Button danger loading={payMutation.isPending}>
                付款 {codOrder.settledAmount.toFixed(2)} 元
              </Button>
            </Popconfirm>
          </Space>
        </Card>
      )}
    </div>
  );
}
