import { useQuery } from '@tanstack/react-query';
import { App, Button, Card, Form, InputNumber, Select, Table, Typography } from 'antd';
import { useState } from 'react';
import { listRuleSets, trial, type TrialResult } from '../api/pricing';

/** 计价试算页（P3 前端）：输入 → 命中规则 + 逐项计算过程展示。 */
export function TrialPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [result, setResult] = useState<TrialResult | null>(null);

  const ruleSetsQuery = useQuery({ queryKey: ['pricing-rule-sets'], queryFn: listRuleSets });
  const activeRuleSets = (ruleSetsQuery.data ?? []).filter((s) => s.status === 'Active');

  const onTrial = async (values: { ruleSetId: string; weightKg: number; volumeM3: number; pieceCount: number }) => {
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
      <Typography.Title level={4}>计价试算</Typography.Title>
      <Card title="输入" style={{ marginBottom: 16 }}>
        <Form layout="inline" form={form} onFinish={onTrial}>
          <Form.Item name="ruleSetId" label="价卡" rules={[{ required: true, message: '请选择价卡' }]}>
            <Select
              placeholder="选择生效价卡"
              style={{ width: 220 }}
              options={activeRuleSets.map((s) => ({ value: s.id, label: `${s.code} ${s.name}` }))}
            />
          </Form.Item>
          <Form.Item name="weightKg" label="重量(kg)" rules={[{ required: true, message: '必填' }]}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="volumeM3" label="体积(m³)" rules={[{ required: true, message: '必填' }]}>
            <InputNumber min={0} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="pieceCount" label="件数" rules={[{ required: true, message: '必填' }]} initialValue={1}>
            <InputNumber min={1} precision={0} style={{ width: 90 }} />
          </Form.Item>
          <Button type="primary" htmlType="submit">
            试算
          </Button>
        </Form>
      </Card>

      {result && (
        <Card title={`试算结果（${result.ruleSetCode} v${result.versionNumber}，命中规则：${result.quote.matchedRuleName}）`}>
          <Typography.Paragraph>
            {result.quote.billingBasis ?? '固定计费'}
          </Typography.Paragraph>
          <Table
            rowKey={(r) => `${r.kind}-${r.name}-${r.amount}`}
            size="small"
            dataSource={result.quote.items}
            columns={[
              { title: '类别', dataIndex: 'kind', width: 90 },
              { title: '费用项', dataIndex: 'name', width: 140 },
              {
                title: '金额',
                dataIndex: 'amount',
                width: 100,
                render: (amount: number) => `${amount.toFixed(2)} 元`,
              },
              { title: '计算过程', dataIndex: 'detail' },
            ]}
            pagination={false}
          />
          {result.quote.discount && (
            <Typography.Paragraph style={{ marginTop: 12 }}>
              折扣：{result.quote.discount.detail}（-{result.quote.discount.amount.toFixed(2)} 元）
            </Typography.Paragraph>
          )}
          <Typography.Paragraph style={{ marginTop: 8 }}>
            小计 <Typography.Text strong>{result.quote.subtotal.toFixed(2)}</Typography.Text> 元；应收{' '}
            <Typography.Text strong style={{ fontSize: 18 }}>
              {result.quote.total.toFixed(2)}
            </Typography.Text>{' '}
            元
          </Typography.Paragraph>
        </Card>
      )}
    </div>
  );
}
