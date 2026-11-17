import { useMutation } from '@tanstack/react-query';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { App, Button, Card, Form, Input, InputNumber, Space, Typography } from 'antd';
import { useState } from 'react';
import { recordArrival } from '../api/arrivals';

interface ArrivalItemRow {
  waybillId: string;
  receivedPieces: number;
  brokenPieces: number;
  remark: string;
}

/** 到货登记页（P6 前端）。 */
export function ArrivalPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [items, setItems] = useState<ArrivalItemRow[]>([
    { waybillId: '', receivedPieces: 1, brokenPieces: 0, remark: '' },
  ]);

  const mutation = useMutation({
    mutationFn: (payload: Parameters<typeof recordArrival>[0]) => recordArrival(payload),
    onSuccess: () => message.success('到货登记成功'),
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>到货登记</Typography.Title>
      <Card title="批次信息" style={{ marginBottom: 16 }}>
        <Form form={form} layout="inline">
          <Form.Item name="tripId" label="运输批次 ID" rules={[{ required: true }]}>
            <Input style={{ width: 300 }} />
          </Form.Item>
          <Form.Item name="branchId" label="到达网点 ID" rules={[{ required: true }]}>
            <Input style={{ width: 300 }} />
          </Form.Item>
          <Form.Item name="note" label="备注">
            <Input style={{ width: 200 }} />
          </Form.Item>
        </Form>
      </Card>

      <Card title="到货清单">
        {items.map((item, index) => (
          <Space key={index} style={{ display: 'flex', marginBottom: 8 }} wrap>
            <Input
              placeholder="运单 ID"
              style={{ width: 280 }}
              value={item.waybillId}
              onChange={(e) => {
                const next = [...items];
                next[index] = { ...item, waybillId: e.target.value };
                setItems(next);
              }}
            />
            <InputNumber
              min={0}
              precision={0}
              value={item.receivedPieces}
              onChange={(v) => {
                const next = [...items];
                next[index] = { ...item, receivedPieces: v ?? 0 };
                setItems(next);
              }}
              addonBefore="实收件数"
              style={{ width: 160 }}
            />
            <InputNumber
              min={0}
              precision={0}
              value={item.brokenPieces}
              onChange={(v) => {
                const next = [...items];
                next[index] = { ...item, brokenPieces: v ?? 0 };
                setItems(next);
              }}
              addonBefore="破损"
              style={{ width: 130 }}
            />
            <Input
              placeholder="备注"
              style={{ width: 160 }}
              value={item.remark}
              onChange={(e) => {
                const next = [...items];
                next[index] = { ...item, remark: e.target.value };
                setItems(next);
              }}
            />
            <Button
              icon={<DeleteOutlined />}
              disabled={items.length <= 1}
              onClick={() => setItems(items.filter((_, i) => i !== index))}
            />
          </Space>
        ))}
        <Button
          icon={<PlusOutlined />}
          onClick={() => setItems([...items, { waybillId: '', receivedPieces: 1, brokenPieces: 0, remark: '' }])}
        >
          添加运单
        </Button>
      </Card>

      <Button
        type="primary"
        size="large"
        loading={mutation.isPending}
        onClick={async () => {
          const values = await form.validateFields();
          mutation.mutate({
            tripId: values.tripId,
            branchId: values.branchId,
            note: values.note,
            items: items.filter((i) => i.waybillId),
          });
        }}
      >
        登记到货
      </Button>
    </div>
  );
}

