import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, InputNumber, Row, Select, Space, Table, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PageHeader } from '../../../shared/components';
import { http } from '../../../shared/api/http';
import { trial, type TrialResult } from '../../pricing/api/pricing';
import { createWaybill } from '../api/waybills';

interface CustomerDetailContacts {
  contacts: { id: string; name: string; phone: string; isPrimary: boolean }[];
  addresses: { id: string; address: string; isDefault: boolean }[];
}

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

  // 监听客户选择变化 → 预填发货人信息（主联系人 + 默认地址）
  const selectedCustomerId = Form.useWatch('customerId', form);
  const customerDetailQuery = useQuery({
    queryKey: ['customer-detail-for-prefill', selectedCustomerId],
    queryFn: async () => {
      const response = await http.get<CustomerDetailContacts>(`/customers/${selectedCustomerId}`);
      return response.data;
    },
    enabled: !!selectedCustomerId,
  });

  useEffect(() => {
    const detail = customerDetailQuery.data;
    if (!detail) return;

    const primary = detail.contacts.find((c) => c.isPrimary) ?? detail.contacts[0];
    const defaultAddr = detail.addresses.find((a) => a.isDefault) ?? detail.addresses[0];

    const patch: Record<string, string | undefined> = {};
    if (primary) {
      patch.senderName = primary.name;
      patch.senderPhone = primary.phone;
    }
    if (defaultAddr) {
      patch.senderAddress = defaultAddr.address;
    }
    if (Object.keys(patch).length > 0) {
      form.setFieldsValue(patch);
    }
  }, [customerDetailQuery.data, form]);

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
        declaredValue: (values.declaredValue as number) || undefined,
        insuranceFee: (values.insuranceFee as number) || undefined,
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
      <PageHeader title="新建运单" description="填写业务信息、收发货人与货物件，运费将按价卡规则实时试算并在开单时固化。" />

      <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
        <Card title="业务信息" style={{ marginBottom: 16 }}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="originBranchId" label="始发网点" rules={[{ required: true, message: '必选' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="选择始发网点"
                  options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="destBranchId" label="目的网点" rules={[{ required: true, message: '必选' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="选择目的网点"
                  options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="customerId" label="客户" rules={[{ required: true, message: '必选' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="选择客户"
                  options={customerOptions.map((c) => ({ value: c.id, label: `${c.code} ${c.name}` }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="ruleSetId" label="价卡" rules={[{ required: true, message: '必选' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="选择价卡"
                  options={(ruleSetQuery.data ?? []).map((s) => ({ value: s.id, label: s.code }))}
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="paymentType" label="支付方式" rules={[{ required: true, message: '必选' }]} initialValue="Cash">
                <Select
                  options={[
                    { value: 'Cash', label: '现付' },
                    { value: 'OnDelivery', label: '到付' },
                    { value: 'Monthly', label: '月结' },
                  ]}
                />
              </Form.Item>
            </Col>
          </Row>
        </Card>

        <Card title="收发货人" style={{ marginBottom: 16 }}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="senderName" label="发货人" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="发货人姓名" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="senderPhone" label="发货人电话" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="联系电话" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="senderAddress" label="发货地址" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="详细发货地址" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="receiverName" label="收货人" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="收货人姓名" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="receiverPhone" label="收货人电话" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="联系电话" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="receiverAddress" label="收货地址" rules={[{ required: true, message: '必填' }]}>
                <Input placeholder="详细收货地址" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} lg={8}>
              <Form.Item name="remark" label="备注">
                <Input placeholder="可选" />
              </Form.Item>
            </Col>
          </Row>
        </Card>

        <Card
          title={`货物件（Σ ${declaredPieces} 件 / ${declaredWeight.toFixed(2)} kg / ${declaredVolume.toFixed(3)} m³）`}
          style={{ marginBottom: 16 }}
          extra={
            <Space>
              <Button onClick={onTrial} loading={trialing} disabled={packages.length === 0}>
                实时试算
              </Button>
              <Button
                type="primary"
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
          <Table
            rowKey="key"
            size="small"
            pagination={false}
            dataSource={packages}
            locale={{ emptyText: <Typography.Text type="secondary">尚未添加货物件，点击右上角「添加货物件」开始录入</Typography.Text> }}
            columns={[
              {
                title: '货名',
                dataIndex: 'cargoName',
                render: (value: string, record: PackageRow) => (
                  <Input
                    placeholder="货物名称"
                    value={value}
                    onChange={(e) => {
                      setPackages(packages.map((p) => (p.key === record.key ? { ...p, cargoName: e.target.value } : p)));
                    }}
                  />
                ),
              },
              {
                title: '件数',
                dataIndex: 'quantity',
                width: 130,
                render: (value: number, record: PackageRow) => (
                  <InputNumber
                    min={1}
                    precision={0}
                    value={value}
                    onChange={(v) => {
                      setPackages(packages.map((p) => (p.key === record.key ? { ...p, quantity: v ?? 1 } : p)));
                    }}
                    style={{ width: '100%' }}
                  />
                ),
              },
              {
                title: '重量 (kg)',
                dataIndex: 'weightKg',
                width: 150,
                align: 'right',
                render: (value: number, record: PackageRow) => (
                  <InputNumber
                    min={0}
                    precision={3}
                    value={value}
                    onChange={(v) => {
                      setPackages(packages.map((p) => (p.key === record.key ? { ...p, weightKg: v ?? 0 } : p)));
                    }}
                    style={{ width: '100%' }}
                  />
                ),
              },
              {
                title: '体积 (m³)',
                dataIndex: 'volumeM3',
                width: 150,
                align: 'right',
                render: (value: number, record: PackageRow) => (
                  <InputNumber
                    min={0}
                    precision={4}
                    value={value}
                    onChange={(v) => {
                      setPackages(packages.map((p) => (p.key === record.key ? { ...p, volumeM3: v ?? 0 } : p)));
                    }}
                    style={{ width: '100%' }}
                  />
                ),
              },
              {
                title: '操作',
                key: 'actions',
                width: 70,
                render: (_: unknown, record: PackageRow) => (
                  <Button
                    type="text"
                    icon={<DeleteOutlined />}
                    danger
                    onClick={() => setPackages(packages.filter((p) => p.key !== record.key))}
                  />
                ),
              },
            ]}
          />
        </Card>

        {trialResult && (
          <Card
            title={`试算结果：命中 ${trialResult.quote.matchedRuleName}（${trialResult.ruleSetCode} v${trialResult.versionNumber}）`}
            style={{ marginBottom: 16 }}
          >
            <Table
              size="small"
              rowKey={(r) => `${r.kind}-${r.name}`}
              dataSource={trialResult.quote.items}
              pagination={false}
              columns={[
                { title: '费用项', dataIndex: 'name', width: 160 },
                {
                  title: '金额',
                  dataIndex: 'amount',
                  width: 140,
                  align: 'right',
                  render: (amount: number) => <span className="ltl-amount">¥ {amount.toFixed(2)}</span>,
                },
                { title: '计算过程', dataIndex: 'detail' },
              ]}
            />
            <div style={{ marginTop: 12, textAlign: 'right' }}>
              <Typography.Text type="secondary">应收合计 </Typography.Text>
              <Typography.Text strong className="ltl-amount" style={{ fontSize: 20, color: '#1D4FA0' }}>
                ¥ {trialResult.quote.total.toFixed(2)}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>（开单后按此金额固化）</Typography.Text>
            </div>
          </Card>
        )}

        <div className="ltl-no-print" style={{ display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
          <Button onClick={() => navigate('/waybills')}>取消</Button>
          <Button type="primary" size="large" htmlType="submit" loading={createMutation.isPending}>
            开单并固化费用
          </Button>
        </div>
      </Form>
    </div>
  );
}
