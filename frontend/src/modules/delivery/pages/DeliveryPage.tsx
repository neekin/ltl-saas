import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, Row, Select, Space, Typography } from 'antd';
import { useState } from 'react';
import { PageHeader } from '../../../shared/components';
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
  const [receiverName, setReceiverName] = useState('');

  const createMutation = useMutation({
    mutationFn: (values: { waybillId: string; branchId: string; deliveryType: string; driverId?: string }) =>
      createDelivery(values),
    onSuccess: (result) => {
      message.success('交付单已创建');
      setOrder(result);
      setReceiverName('');
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
      <PageHeader title="交付确认" description="创建交付单，登记签收或拒收结果。" />

      <Card title="创建交付单" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={8}>
              <Form.Item name="waybillId" label="运单 ID" rules={[{ required: true }]}>
                <Input placeholder="输入运单 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="branchId" label="交付网点 ID" rules={[{ required: true }]}>
                <Input placeholder="输入网点 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={4}>
              <Form.Item name="deliveryType" label="类型" rules={[{ required: true }]} initialValue="SelfPickup">
                <Select
                  options={[
                    { value: 'SelfPickup', label: '自提' },
                    { value: 'Delivery', label: '配送' },
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={4}>
              <Form.Item label=" ">
                <Button type="primary" htmlType="submit" block loading={createMutation.isPending}>
                  创建
                </Button>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {order && (
        <Card
          title={`交付单（${order.deliveryType === 'SelfPickup' ? '自提' : '配送'}）`}
          extra={<Typography.Text type="secondary">状态：{order.status}</Typography.Text>}
        >
          <Space wrap>
            <Typography.Text>签收人：</Typography.Text>
            <Input
              placeholder="签收人姓名"
              style={{ width: 200 }}
              value={receiverName}
              onChange={(e) => setReceiverName(e.target.value)}
            />
            <Button type="primary" onClick={() => completeMutation.mutate(receiverName)}>
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
