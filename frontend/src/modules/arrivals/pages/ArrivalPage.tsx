import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, InputNumber, Row } from 'antd';
import { useState } from 'react';
import { PageHeader } from '../../../shared/components';
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
      <PageHeader title="到货登记" description="登记运输批次的到货物件、实收与破损情况。" />

      <Card title="批次信息" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical">
          <Row gutter={[16, 0]}>
            <Col xs={24} md={8}>
              <Form.Item name="tripId" label="运输批次 ID" rules={[{ required: true }]}>
                <Input placeholder="输入批次 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="branchId" label="到达网点 ID" rules={[{ required: true }]}>
                <Input placeholder="输入网点 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={8}>
              <Form.Item name="note" label="备注">
                <Input placeholder="可选" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      <Card title="到货清单" style={{ marginBottom: 16 }}>
        {items.map((item, index) => (
          <Row key={index} gutter={[12, 12]} align="middle" style={{ marginBottom: 8 }}>
            <Col xs={24} md={7}>
              <Input
                placeholder="运单 ID"
                className="ltl-code"
                value={item.waybillId}
                onChange={(e) => {
                  const next = [...items];
                  next[index] = { ...item, waybillId: e.target.value };
                  setItems(next);
                }}
              />
            </Col>
            <Col xs={12} md={5}>
              <InputNumber
                min={0}
                precision={0}
                value={item.receivedPieces}
                onChange={(v) => {
                  const next = [...items];
                  next[index] = { ...item, receivedPieces: v ?? 0 };
                  setItems(next);
                }}
                addonBefore="实收"
                style={{ width: '100%' }}
              />
            </Col>
            <Col xs={12} md={4}>
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
                style={{ width: '100%' }}
              />
            </Col>
            <Col xs={20} md={6}>
              <Input
                placeholder="备注"
                value={item.remark}
                onChange={(e) => {
                  const next = [...items];
                  next[index] = { ...item, remark: e.target.value };
                  setItems(next);
                }}
              />
            </Col>
            <Col xs={4} md={2}>
              <Button
                type="text"
                icon={<DeleteOutlined />}
                danger
                disabled={items.length <= 1}
                onClick={() => setItems(items.filter((_, i) => i !== index))}
              />
            </Col>
          </Row>
        ))}
        <Button
          icon={<PlusOutlined />}
          onClick={() =>
            setItems([...items, { waybillId: '', receivedPieces: 1, brokenPieces: 0, remark: '' }])
          }
        >
          添加运单
        </Button>
      </Card>

      <div className="ltl-no-print" style={{ display: 'flex', justifyContent: 'flex-end' }}>
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
    </div>
  );
}
