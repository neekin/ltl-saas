import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, InputNumber, Select, Space, Table, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { http } from '../../../shared/api/http';
import { trial, type TrialResult } from '../../pricing/api/pricing';
import { createWaybill } from '../api/waybills';

interface BranchOption {
  id: string;
  code: string;
  name: string;
}

interface CustomerOption {
  id: string;
  code: string;
  name: string;
}

interface PackageRow {
  key: number;
  cargoName: string;
  quantity: number;
  weightKg: number;
  volumeM3: number;
}

/** 开单页（P4 前端）：客户/线路/收发货人/货物件 + 实时计价试算（P4 验收）。 */
export function WaybillCreatePage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [packages, setPackages] = useState<PackageRow[]>([]);
  const [trialResult, setTrialResult] = useState<TrialResult | null>(null);
  const [trialing, setTrialing] = useState(false);

  const branchQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
  });

  const customerQuery = useQuery({
    queryKey: ['customer-options'],
    queryFn: async () => {
      const response = await http.get<{ items: CustomerOption[] }>('/customers', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
  });

  const ruleSetQuery = useQuery({
    queryKey: ['pricing-rule-sets'],
    queryFn: async () => {
      const response = await http.get<{ id: string; code: string; status: string }[]>('/pricing/rule-sets');
      return response.data.filter((s) => s.status === 'Active');
    },
  });

  const branchOptions = branchQuery.data ?? [];
  const customerOptions = customerQuery.data ?? [];
  const declaredWeight = packages.reduce((sum, p) => sum + p.weightKg * p.quantity, 0);
  const declaredVolume = packages.reduce((sum, p) => sum + p.volumeM3 * p.quantity, 0);
  const declaredPieces = packages.reduce((sum, p) => sum + p.quantity, 0);

  const onTrial = async () => {
    const ruleSetId = form.getFieldValue('ruleSetId') as string | undefined;
    const originBranchId = form.getFieldValue('originBranchId') as string | undefined;
    const destBranchId = form.getFieldValue('destBranchId') as string | undefined;
    if (!ruleSetId) {
      message.warning('请先选择价卡');
      return;
    }
    setTrialing(true);
    try {
      const result = await trial(ruleSetId, {
        weightKg: declaredWeight,
        volumeM3: declaredVolume,
        pieceCount: declaredPieces,
        originBranchId: originBranchId ?? null,
        destBranchId: destBranchId ?? null,
      });
      setTrialResult(result);
    } catch (e) {
      message.error(e instanceof Error ? e.message : '试算失败');
    } finally {
      setTrialing(false);
    }
  };

  const onSubmit = async (values: Record<string, unknown>) => {
    if (packages.length === 0) {
      message.error('至少添加一个货物件');
      return;
    }
    try {
      const result = await createWaybill({
        originBranchId: values.originBranchId as string,
        destBranchId: values.destBranchId as string,
        customerId: values.customerId as string,
        ruleSetId: values.ruleSetId as string,
        paymentType: values.paymentType as string,
        senderName: values.senderName as string,
        senderPhone: values.senderPhone as string,
        senderAddress: values.senderAddress as string,
        receiverName: values.receiverName as string,
        receiverPhone: values.receiverPhone as string,
        receiverAddress: values.receiverAddress as string,
        remark: (values.remark as string) || undefined,
        packages: packages.map((p) => ({
          cargoName: p.cargoName,
          quantity: p.quantity,
          weightKg: p.weightKg,
          volumeM3: p.volumeM3,
        })),
      });
      message.success(`开单成功：${result.waybillNo}`);
      navigate(`/waybills/${result.waybillId}`);
    } catch (e) {
      message.error(e instanceof Error ? e.message : '开单失败');
    }
  };

  const createMutation = useMutation({
    mutationFn: onSubmit,
  });

  return (
    <div>
      <Typography.Title level={4}>开单</Typography.Title>
      <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
        <Card title="业务信息" style={{ marginBottom: 16 }}>
          <Space wrap size={16}>
            <Form.Item name="originBranchId" label="始发网点" rules={[{ required: true, message: '必选' }]}>
              <Select
                showSearch
                optionFilterProp="label"
                style={{ width: 200 }}
                placeholder="选择网点"
                options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
              />
            </Form.Item>
            <Form.Item name="destBranchId" label="目的网点" rules={[{ required: true, message: '必选' }]}>
              <Select
                showSearch
                optionFilterProp="label"
                style={{ width: 200 }}
                placeholder="选择网点"
                options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
              />
            </Form.Item>
            <Form.Item name="customerId" label="客户" rules={[{ required: true, message: '必选' }]}>
              <Select
                showSearch
                optionFilterProp="label"
                style={{ width: 200 }}
                placeholder="选择客户"
                options={customerOptions.map((c) => ({ value: c.id, label: `${c.code} ${c.name}` }))}
              />
            </Form.Item>
            <Form.Item name="ruleSetId" label="价卡" rules={[{ required: true, message: '必选' }]}>
              <Select
                showSearch
                style={{ width: 200 }}
                placeholder="选择价卡"
                options={(ruleSetQuery.data ?? []).map((s) => ({ value: s.id, label: `${s.code}` }))}
              />
            </Form.Item>
            <Form.Item name="paymentType" label="支付方式" rules={[{ required: true, message: '必选' }]} initialValue="Cash">
              <Select
                style={{ width: 140 }}
                options={[
                  { value: 'Cash', label: '现付' },
                  { value: 'OnDelivery', label: '到付' },
                  { value: 'Monthly', label: '月结' },
                ]}
              />
            </Form.Item>
          </Space>
        </Card>

        <Card title="收发货人" style={{ marginBottom: 16 }}>
          <Space wrap size={16}>
            <Form.Item name="senderName" label="发货人" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 140 }} />
            </Form.Item>
            <Form.Item name="senderPhone" label="电话" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 150 }} />
            </Form.Item>
            <Form.Item name="senderAddress" label="发货地址" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 260 }} />
            </Form.Item>
            <Form.Item name="receiverName" label="收货人" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 140 }} />
            </Form.Item>
            <Form.Item name="receiverPhone" label="电话" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 150 }} />
            </Form.Item>
            <Form.Item name="receiverAddress" label="收货地址" rules={[{ required: true, message: '必填' }]}>
              <Input style={{ width: 260 }} />
            </Form.Item>
            <Form.Item name="remark" label="备注">
              <Input style={{ width: 160 }} />
            </Form.Item>
          </Space>
        </Card>

        <Card
          title={`货物件（Σ ${declaredPieces} 件 / ${declaredWeight.toFixed(2)}kg / ${declaredVolume.toFixed(3)}m³）`}
          style={{ marginBottom: 16 }}
          extra={
            <Space>
              <Button onClick={onTrial} loading={trialing} disabled={packages.length === 0}>
                实时试算
              </Button>
              <Button
                icon={<PlusOutlined />}
                onClick={() =>
                  setPackages([
                    ...packages,
                    { key: Date.now(), cargoName: '', quantity: 1, weightKg: 0, volumeM3: 0 },
                  ])
                }
              >
                添加货物件
              </Button>
            </Space>
          }
        >
          {packages.length === 0 && <Typography.Text type="secondary">尚未添加货物件</Typography.Text>}
          {packages.map((pkg, index) => (
            <Space key={pkg.key} style={{ display: 'flex', marginBottom: 8 }} wrap>
              <Input
                placeholder="货名"
                style={{ width: 180 }}
                value={pkg.cargoName}
                onChange={(e) => {
                  const next = [...packages];
                  next[index] = { ...pkg, cargoName: e.target.value };
                  setPackages(next);
                }}
              />
              <InputNumber
                min={1}
                precision={0}
                value={pkg.quantity}
                onChange={(v) => {
                  const next = [...packages];
                  next[index] = { ...pkg, quantity: v ?? 1 };
                  setPackages(next);
                }}
                addonBefore="件数"
                style={{ width: 140 }}
              />
              <InputNumber
                min={0}
                value={pkg.weightKg}
                onChange={(v) => {
                  const next = [...packages];
                  next[index] = { ...pkg, weightKg: v ?? 0 };
                  setPackages(next);
                }}
                addonBefore="重量kg"
                style={{ width: 160 }}
              />
              <InputNumber
                min={0}
                value={pkg.volumeM3}
                onChange={(v) => {
                  const next = [...packages];
                  next[index] = { ...pkg, volumeM3: v ?? 0 };
                  setPackages(next);
                }}
                addonBefore="体积m³"
                style={{ width: 170 }}
              />
              <Button
                icon={<DeleteOutlined />}
                onClick={() => setPackages(packages.filter((_, i) => i !== index))}
              />
            </Space>
          ))}
        </Card>

        {trialResult && (
          <Card title={`试算：命中 ${trialResult.quote.matchedRuleName}（${trialResult.ruleSetCode} v${trialResult.versionNumber}）`} style={{ marginBottom: 16 }}>
            <Table
              size="small"
              rowKey={(r) => `${r.kind}-${r.name}`}
              dataSource={trialResult.quote.items}
              pagination={false}
              columns={[
                { title: '费用项', dataIndex: 'name', width: 140 },
                {
                  title: '金额',
                  dataIndex: 'amount',
                  width: 110,
                  render: (amount: number) => `${amount.toFixed(2)} 元`,
                },
                { title: '计算过程', dataIndex: 'detail' },
              ]}
            />
            <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
              应收 <Typography.Text strong style={{ fontSize: 18 }}>{trialResult.quote.total.toFixed(2)}</Typography.Text> 元
              （开单后按此金额固化）
            </Typography.Paragraph>
          </Card>
        )}

        <Button type="primary" size="large" htmlType="submit" loading={createMutation.isPending}>
          开单（固化费用）
        </Button>
      </Form>
    </div>
  );
}
