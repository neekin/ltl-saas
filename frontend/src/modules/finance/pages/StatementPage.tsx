import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, Popconfirm, Row, Space, Typography } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
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
      <PageHeader
        title="月结账单"
        description="按客户与账期生成月结账单，确认后明细冻结，关闭为终态。"
      />

      <Card title="生成账单" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical" onFinish={(values) => generateMutation.mutate(values)}>
          <Row gutter={[16, 0]} align="bottom">
            <Col xs={24} md={8}>
              <Form.Item name="customerId" label="客户 ID" rules={[{ required: true }]}>
                <Input placeholder="输入客户 ID" />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item
                name="period"
                label="账期"
                rules={[{ required: true, message: '格式 yyyyMM' }]}
              >
                <Input placeholder="yyyyMM" />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item label=" ">
                <Button type="primary" htmlType="submit" loading={generateMutation.isPending}>
                  生成
                </Button>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {!statement && (
        <Card>
          <EmptyState description="生成账单后在此查看与确认" />
        </Card>
      )}

      {statement && (
        <Card
          title={
            <Space size={10}>
              <span>账单 {statement.period}</span>
              <StatusTag status={statement.status} />
            </Space>
          }
        >
          <div style={{ marginBottom: 16 }}>
            <div style={{ color: '#8c96a5', fontSize: 12 }}>账单总金额</div>
            <div
              className="ltl-amount ltl-amount--strong"
              style={{ fontSize: 24, color: '#1D4FA0' }}
            >
              ¥ {statement.totalAmount.toFixed(2)}
            </div>
          </div>
          <Space wrap>
            {statement.status === 'Draft' && (
              <Popconfirm
                title="确认后明细冻结不可修改"
                onConfirm={() => confirmMutation.mutate(statement.id)}
              >
                <Button type="primary">确认账单</Button>
              </Popconfirm>
            )}
            {statement.status === 'Confirmed' && (
              <Popconfirm
                title="关闭为终态，确认？"
                onConfirm={() => closeMutation.mutate(statement.id)}
              >
                <Button danger>关闭账单</Button>
              </Popconfirm>
            )}
          </Space>
          <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
            确认后明细冻结不可修改；调整须通过调整单或冲正记录完成。
          </Typography.Paragraph>
        </Card>
      )}
    </div>
  );
}
