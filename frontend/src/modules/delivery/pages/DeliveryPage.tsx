import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, Select, Space, Typography } from 'antd';
import { useState } from 'react';
import { completeDelivery, createDelivery, rejectDelivery } from '../api/deliveries';

interface DeliveryOrder {
  id: string;
  deliveryType: string;
  status: string;
  receiverName: string | null;
}

/** 交付确认页（P6 前端）：创建交付单/签收/拒收。 */
export function DeliveryPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [order, setOrder] = useState<DeliveryOrder | null>(null);

  const createMutation = useMutation({
    mutationFn: (values: { waybillId: string; branchId: string; deliveryType: string; driverId?: string }) =>
      createDelivery(values),
    onSuccess: (result) => {
      message.success('交付单已创建');
      setOrder(result);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const completeMutation = useMutation({
    mutationFn: (receiverName: string) => completeDelivery(order!.id, receiverName),
    onSuccess: () => {
      message.success('签收完成');
      setOrder(null);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const rejectMutation = useMutation({
    mutationFn: () => rejectDelivery(order!.id),
    onSuccess: () => {
      message.success('已拒收');
      setOrder(null);
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>交付确认</Typography.Title>
      <Card title="创建交付单" style={{ marginBottom: 16 }}>
        <Form form={form} layout="inline" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="waybillId" label="运单 ID" rules={[{ required: true }]}>
            <Input style={{ width: 300 }} />
          </Form.Item>
          <Form.Item name="branchId" label="交付网点 ID" rules={[{ required: true }]}>
            <Input style={{ width: 300 }} />
          </Form.Item>
          <Form.Item name="deliveryType" label="类型" rules={[{ required: true }]} initialValue="SelfPickup">
            <Select style={{ width: 120 }} options={[
              { value: 'SelfPickup', label: '自提' },
              { value: 'Delivery', label: '配送' },
            ]} />
          </Form.Item>
          <Button htmlType="submit" loading={createMutation.isPending}>创建</Button>
        </Form>
      </Card>

      {order && (
        <Card title={`交付单（${order.deliveryType === 'SelfPickup' ? '自提' : '配送'} / ${order.status}）`}>
          <Space>
            <Typography.Text>签收人：</Typography.Text>
            <Input
              placeholder="签收人姓名"
              style={{ width: 180 }}
              onChange={(e) => { order.receiverName = e.target.value; }}
            />
            <Button type="primary" onClick={() => completeMutation.mutate(order.receiverName ?? '')}>
              签收
            </Button>
            <Button danger onClick={() => rejectMutation.mutate()}>
              拒收
            </Button>
          </Space>
        </Card>
      )}
    </div>
  );
}

