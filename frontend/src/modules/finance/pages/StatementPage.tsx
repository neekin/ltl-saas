import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, Popconfirm, Space, Typography } from 'antd';
import { useState } from 'react';
import { http } from '../../../shared/api/http';

interface Statement {
  id: string;
  customerId: string;
  period: string;
  totalAmount: number;
  status: string;
}

/** 月结账单页（P7 前端）：生成/确认/关闭。 */
export function StatementPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [statement, setStatement] = useState<Statement | null>(null);

  const generateMutation = useMutation({
    mutationFn: (values: { customerId: string; period: string }) =>
      http.post<Statement>('/finance/statements', values),
    onSuccess: (response) => {
      message.success('账单已生成');
      setStatement(response.data);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const confirmMutation = useMutation({
    mutationFn: (id: string) => http.post(`/finance/statements/${id}/confirm`),
    onSuccess: () => {
      message.success('已确认（明细冻结）');
      if (statement) setStatement({ ...statement, status: 'Confirmed' });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const closeMutation = useMutation({
    mutationFn: (id: string) => http.post(`/finance/statements/${id}/close`),
    onSuccess: () => {
      message.success('已关闭');
      if (statement) setStatement({ ...statement, status: 'Closed' });
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>月结账单</Typography.Title>
      <Card title="生成账单" style={{ marginBottom: 16 }}>
        <Form form={form} layout="inline" onFinish={(values) => generateMutation.mutate(values)}>
          <Form.Item name="customerId" label="客户 ID" rules={[{ required: true }]}>
            <Input style={{ width: 300 }} />
          </Form.Item>
          <Form.Item name="period" label="账期" rules={[{ required: true, message: '格式 yyyyMM' }]}>
            <Input placeholder="yyyyMM" style={{ width: 120 }} />
          </Form.Item>
          <Button htmlType="submit" loading={generateMutation.isPending}>
            生成
          </Button>
        </Form>
      </Card>

      {statement && (
        <Card title={`账单 ${statement.period}（${statement.status}）`}>
          <Typography.Paragraph>
            总金额 <Typography.Text strong>{statement.totalAmount.toFixed(2)}</Typography.Text> 元
          </Typography.Paragraph>
          <Space>
            {statement.status === 'Draft' && (
              <Popconfirm title="确认后明细冻结不可修改" onConfirm={() => confirmMutation.mutate(statement.id)}>
                <Button type="primary">确认账单</Button>
              </Popconfirm>
            )}
            {statement.status === 'Confirmed' && (
              <Popconfirm title="关闭为终态，确认？" onConfirm={() => closeMutation.mutate(statement.id)}>
                <Button danger>关闭账单</Button>
              </Popconfirm>
            )}
          </Space>
        </Card>
      )}
    </div>
  );
}

