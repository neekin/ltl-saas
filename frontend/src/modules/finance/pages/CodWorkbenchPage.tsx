import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Input, InputNumber, Space, Typography } from 'antd';
import { useState } from 'react';
import { getCodOrder, recordCodCollection, type CodOrder } from '../api/finance';

/** COD 工作台（P8 前端）：按运单查询 COD 订单并登记收款。 */
export function CodWorkbenchPage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [codOrder, setCodOrder] = useState<CodOrder | null>(null);
  const [collectAmount, setCollectAmount] = useState(0);

  const search = async () => {
    try {
      setCodOrder(await getCodOrder(waybillId));
    } catch {
      setCodOrder(null);
      message.info('未找到 COD 订单');
    }
  };

  const collectMutation = useMutation({
    mutationFn: () => recordCodCollection(waybillId, collectAmount),
    onSuccess: () => {
      message.success('COD 收款成功');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>COD 工作台</Typography.Title>
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
        <Card title={`COD 订单（${codOrder.status}）`}>
          <Space wrap>
            <Typography.Text>代收金额 <strong>{codOrder.amount.toFixed(2)}</strong> 元</Typography.Text>
            <Typography.Text>已收 <strong>{codOrder.collectedAmount.toFixed(2)}</strong> 元</Typography.Text>
            <Typography.Text>未收 <strong>{(codOrder.amount - codOrder.collectedAmount).toFixed(2)}</strong> 元</Typography.Text>
          </Space>
          {codOrder.collectedAmount < codOrder.amount && (
            <Space style={{ marginTop: 12 }}>
              <InputNumber min={0.01} value={collectAmount} onChange={(v) => setCollectAmount(v ?? 0)} addonBefore="收款" style={{ width: 160 }} />
              <Button type="primary" onClick={() => collectMutation.mutate()} loading={collectMutation.isPending}>
                登记收款
              </Button>
            </Space>
          )}
        </Card>
      )}
    </div>
  );
}
