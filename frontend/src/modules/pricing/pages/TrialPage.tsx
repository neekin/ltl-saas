import { useQuery } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, InputNumber, Row, Select, Table, Typography } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader } from '../../../shared/components';
import { listRuleSets, trial, type TrialResult } from '../api/pricing';

/** 计价试算页（P3 前端）：输入 → 命中规则 + 逐项计算过程展示。 */
export function TrialPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [result, setResult] = useState<TrialResult | null>(null);

  const ruleSetsQuery = useQuery({ queryKey: ['pricing-rule-sets'], queryFn: listRuleSets });
  const activeRuleSets = (ruleSetsQuery.data ?? []).filter((s) => s.status === 'Active');

  const onTrial = async (values: {
    ruleSetId: string;
    weightKg: number;
    volumeM3: number;
    pieceCount: number;
  }) => {
    try {
      const trialResult = await trial(values.ruleSetId, {
        weightKg: values.weightKg,
        volumeM3: values.volumeM3,
        pieceCount: values.pieceCount,
      });
      setResult(trialResult);
    } catch (e) {
      message.error(e instanceof Error ? e.message : '试算失败');
    }
  };

  return (
    <div>
      <PageHeader title="计价试算" description="输入货物参数，查看命中的计费规则与逐项计算过程。" />

      <Card title="试算输入" style={{ marginBottom: 16 }}>
        <Form layout="vertical" form={form} onFinish={onTrial}>
          <Row gutter={[16, 0]} align="bottom">
            <Col xs={24} md={8}>
              <Form.Item
                name="ruleSetId"
                label="价卡"
                rules={[{ required: true, message: '请选择价卡' }]}
              >
                <Select
                  placeholder="选择生效价卡"
                  options={activeRuleSets.map((s) => ({
                    value: s.id,
                    label: `${s.code} ${s.name}`,
                  }))}
                />
              </Form.Item>
            </Col>
            <Col xs={12} md={4}>
              <Form.Item
                name="weightKg"
                label="重量 (kg)"
                rules={[{ required: true, message: '必填' }]}
              >
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col xs={12} md={4}>
              <Form.Item
                name="volumeM3"
                label="体积 (m³)"
                rules={[{ required: true, message: '必填' }]}
              >
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col xs={12} md={3}>
              <Form.Item
                name="pieceCount"
                label="件数"
                rules={[{ required: true, message: '必填' }]}
                initialValue={1}
              >
                <InputNumber min={1} precision={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col xs={12} md={5}>
              <Form.Item label=" ">
                <Button type="primary" htmlType="submit" block>
                  试算
                </Button>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {!result && (
        <Card>
          <EmptyState description="填写左侧参数并点击「试算」查看费用明细" />
        </Card>
      )}

      {result && (
        <Card
          title={
            <span>
              试算结果 · {result.ruleSetCode} v{result.versionNumber}
              <Typography.Text
                type="secondary"
                style={{ marginLeft: 8, fontWeight: 400, fontSize: 13 }}
              >
                命中规则：{result.quote.matchedRuleName}
              </Typography.Text>
            </span>
          }
        >
          <Typography.Paragraph type="secondary">
            {result.quote.billingBasis ?? '固定计费'}
          </Typography.Paragraph>
          <Table
            rowKey={(r) => `${r.kind}-${r.name}-${r.amount}`}
            size="small"
            dataSource={result.quote.items}
            scroll={{ x: 600 }}
            pagination={false}
            locale={{ emptyText: <EmptyState simple description="无费用项" /> }}
            columns={[
              { title: '类别', dataIndex: 'kind', width: 100 },
              { title: '费用项', dataIndex: 'name', width: 150 },
              {
                title: '金额',
                dataIndex: 'amount',
                width: 120,
                align: 'right',
                render: (amount: number) => (
                  <span className="ltl-amount">¥ {amount.toFixed(2)}</span>
                ),
              },
              { title: '计算过程', dataIndex: 'detail' },
            ]}
          />
          {result.quote.discount && (
            <Typography.Paragraph style={{ marginTop: 12, marginBottom: 0 }}>
              折扣：{result.quote.discount.detail}（-¥ {result.quote.discount.amount.toFixed(2)}）
            </Typography.Paragraph>
          )}
          <div style={{ marginTop: 12, textAlign: 'right' }}>
            <Typography.Text type="secondary">
              小计 ¥ {result.quote.subtotal.toFixed(2)}　应收合计{' '}
            </Typography.Text>
            <Typography.Text
              strong
              className="ltl-amount"
              style={{ fontSize: 20, color: '#1D4FA0' }}
            >
              ¥ {result.quote.total.toFixed(2)}
            </Typography.Text>
          </div>
        </Card>
      )}
    </div>
  );
}
