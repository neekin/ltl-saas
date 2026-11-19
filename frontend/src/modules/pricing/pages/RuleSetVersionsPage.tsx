import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  App,
  Button,
  Card,
  Checkbox,
  Collapse,
  Empty,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Spin,
  Typography,
} from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';
import {
  createDraftVersion,
  listVersions,
  publishVersion,
  updateDraftVersion,
  validateVersion,
  type PricingDefinition,
  type PricingRule,
  type PricingVersion,
} from '../api/pricing';

const EMPTY_DEFINITION: PricingDefinition = {
  rounding: 'HalfUpTwoDecimals',
  rules: [
    {
      priority: 1,
      name: '标准运价',
      enabled: true,
      billingWeightStrategy: 'WeightOnly',
      conditions: [],
      freight: { name: '运费', type: 'ByWeight', unitPrice: 1.5 },
      surcharges: [],
    },
  ],
};

// 条件/费用字段白名单（与后端 PricingConditionField/Operator、PricingFreightType 对齐，AGENTS §6.4 声明式规则）
const CONDITION_FIELDS = [
  { value: 'WeightKg', label: '货物总重量 (kg)' },
  { value: 'VolumeM3', label: '货物总体积 (m³)' },
  { value: 'PieceCount', label: '货物件数' },
  { value: 'CustomerId', label: '客户（客户专属规则）' },
  { value: 'RouteId', label: '线路' },
  { value: 'OriginBranchId', label: '始发网点' },
  { value: 'DestBranchId', label: '目的网点' },
];
const UUID_FIELDS = new Set(['CustomerId', 'RouteId', 'OriginBranchId', 'DestBranchId']);
const CONDITION_OPERATORS = [
  { value: 'Equals', label: '等于' },
  { value: 'In', label: '包含之一（逗号分隔）' },
  { value: 'GreaterThan', label: '大于' },
  { value: 'GreaterOrEqual', label: '大于等于' },
  { value: 'LessThan', label: '小于' },
  { value: 'LessOrEqual', label: '小于等于' },
  { value: 'Between', label: '区间 [Value, Value2]' },
];
const BILLING_STRATEGIES = [
  { value: 'WeightVolumeMax', label: '重量与体积重取大' },
  { value: 'WeightVolumeMin', label: '重量与体积重取小' },
  { value: 'WeightOnly', label: '仅按重量' },
  { value: 'VolumeOnly', label: '仅按体积' },
];
const FREIGHT_TYPES = [
  { value: 'Fixed', label: '固定运费（一口价）' },
  { value: 'ByWeight', label: '按计费重量单价' },
  { value: 'ByVolume', label: '按体积单价' },
  { value: 'ByPieces', label: '按件数单价' },
  { value: 'Tiered', label: '阶梯计费' },
];
const SURCHARGE_BASES = [
  { value: 'PerWaybill', label: '整票固定' },
  { value: 'PerPiece', label: '每件' },
  { value: 'PerDeclaredValue', label: '保价费（按声明价值费率）' },
];

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
interface RouteOption {
  id: string;
  code: string;
  name: string;
}

/** 规则集版本管理页（P3 前端）：结构化规则/条件配置器 + 校验/发布；已发布版本不可修改。 */
export function RuleSetVersionsPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<PricingDefinition | null>(null);

  const versionsQuery = useQuery({
    queryKey: ['pricing-versions', id],
    queryFn: () => listVersions(id!),
  });

  // 条件值需要实体下拉（客户/线路/网点）——仅编辑时加载
  const editing = editingId !== null;
  const customersQuery = useQuery({
    queryKey: ['customer-options'],
    queryFn: async () => {
      const response = await http.get<{ items: CustomerOption[] }>('/customers', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
    enabled: editing,
  });
  const branchesQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
    enabled: editing,
  });
  const routesQuery = useQuery({
    queryKey: ['route-options'],
    queryFn: async () => {
      const response = await http.get<{ items: RouteOption[] }>('/routes', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
    enabled: editing,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['pricing-versions', id] });

  const createMutation = useMutation({
    mutationFn: () => createDraftVersion(id!, EMPTY_DEFINITION),
    onSuccess: () => {
      message.success('草稿已创建');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const startEdit = (version: PricingVersion) => {
    setEditingId(version.id);
    setDraft(JSON.parse(JSON.stringify(version.definition)) as PricingDefinition);
  };

  const saveMutation = useMutation({
    mutationFn: () => updateDraftVersion(editingId!, draft!),
    onSuccess: () => {
      message.success('草稿已保存');
      setEditingId(null);
      setDraft(null);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const validateMutation = useMutation({
    mutationFn: () => validateVersion(editingId ?? versionsQuery.data![0].id),
    onSuccess: (errors) => {
      if (errors.length === 0) {
        message.success('校验通过');
      } else {
        message.error(`校验未通过（${errors.length} 项），发布前需修复`);
      }
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const publishMutation = useMutation({
    mutationFn: () => publishVersion(editingId ?? versionsQuery.data![0].id),
    onSuccess: () => {
      message.success('已发布');
      setEditingId(null);
      setDraft(null);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  // ---------- 草稿编辑辅助（不可变更新） ----------
  const updateRule = (index: number, patch: Partial<PricingRule>) => {
    setDraft((d) => {
      if (!d) return d;
      const rules = d.rules.map((r, i) => (i === index ? { ...r, ...patch } : r));
      return { ...d, rules };
    });
  };

  const uuidOptions = (field: string) => {
    if (field === 'CustomerId') {
      return (customersQuery.data ?? []).map((c) => ({
        value: c.id,
        label: `${c.code} ${c.name}`,
      }));
    }
    if (field === 'RouteId') {
      return (routesQuery.data ?? []).map((r) => ({ value: r.id, label: `${r.code} ${r.name}` }));
    }
    return (branchesQuery.data ?? []).map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }));
  };

  const renderConditionRow = (ruleIndex: number, condIndex: number) => {
    const condition = draft!.rules[ruleIndex].conditions[condIndex];
    const isUuid = UUID_FIELDS.has(condition.field);
    const isBetween = condition.operator === 'Between';
    return (
      <Space key={condIndex} wrap size={8}>
        <Select
          style={{ width: 190 }}
          value={condition.field}
          options={CONDITION_FIELDS}
          onChange={(field) =>
            updateRule(ruleIndex, {
              conditions: draft!.rules[ruleIndex].conditions.map((c, i) =>
                i === condIndex ? { ...c, field, value: '', value2: null } : c,
              ),
            })
          }
        />
        <Select
          style={{ width: 190 }}
          value={condition.operator}
          options={CONDITION_OPERATORS}
          onChange={(operator) =>
            updateRule(ruleIndex, {
              conditions: draft!.rules[ruleIndex].conditions.map((c, i) =>
                i === condIndex
                  ? { ...c, operator, value2: operator === 'Between' ? c.value2 : null }
                  : c,
              ),
            })
          }
        />
        {isUuid ? (
          <Select
            showSearch
            optionFilterProp="label"
            style={{ width: 220 }}
            placeholder="选择实体"
            value={condition.value || undefined}
            options={uuidOptions(condition.field)}
            onChange={(value) =>
              updateRule(ruleIndex, {
                conditions: draft!.rules[ruleIndex].conditions.map((c, i) =>
                  i === condIndex ? { ...c, value } : c,
                ),
              })
            }
          />
        ) : (
          <Input
            style={{ width: 160 }}
            placeholder={isUuid ? '' : '值'}
            value={condition.value}
            onChange={(e) =>
              updateRule(ruleIndex, {
                conditions: draft!.rules[ruleIndex].conditions.map((c, i) =>
                  i === condIndex ? { ...c, value: e.target.value } : c,
                ),
              })
            }
          />
        )}
        {isBetween && (
          <Input
            style={{ width: 160 }}
            placeholder="上限 Value2"
            value={condition.value2 ?? ''}
            onChange={(e) =>
              updateRule(ruleIndex, {
                conditions: draft!.rules[ruleIndex].conditions.map((c, i) =>
                  i === condIndex ? { ...c, value2: e.target.value } : c,
                ),
              })
            }
          />
        )}
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() =>
            updateRule(ruleIndex, {
              conditions: draft!.rules[ruleIndex].conditions.filter((_, i) => i !== condIndex),
            })
          }
        />
      </Space>
    );
  };

  const renderRuleEditor = (rule: PricingRule, ruleIndex: number) => (
    <Card
      key={ruleIndex}
      size="small"
      title={`规则 ${ruleIndex + 1}：优先级 ${rule.priority}`}
      extra={
        draft!.rules.length > 1 && (
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            onClick={() =>
              setDraft((d) => {
                if (!d) return d;
                return { ...d, rules: d.rules.filter((_, i) => i !== ruleIndex) };
              })
            }
          >
            删除规则
          </Button>
        )
      }
    >
      <Space wrap size={8} style={{ marginBottom: 8 }}>
        <InputNumber
          min={1}
          precision={0}
          value={rule.priority}
          onChange={(v) => updateRule(ruleIndex, { priority: v ?? 1 })}
          prefix="优先级"
          style={{ width: 150 }}
        />
        <Input
          style={{ width: 200 }}
          value={rule.name}
          onChange={(e) => updateRule(ruleIndex, { name: e.target.value })}
          placeholder="规则名称"
        />
        <Select
          style={{ width: 200 }}
          value={rule.billingWeightStrategy}
          options={BILLING_STRATEGIES}
          onChange={(v) => updateRule(ruleIndex, { billingWeightStrategy: v })}
        />
        <Checkbox
          checked={rule.enabled}
          onChange={(e) => updateRule(ruleIndex, { enabled: e.target.checked })}
        >
          启用
        </Checkbox>
      </Space>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
        运费（PricingFreightType 白名单；阶梯计费用 JSON 视图编辑 tiers）
      </Typography.Paragraph>
      <Space wrap size={8} style={{ marginBottom: 8 }}>
        <Input
          style={{ width: 140 }}
          value={rule.freight.name}
          onChange={(e) =>
            updateRule(ruleIndex, { freight: { ...rule.freight, name: e.target.value } })
          }
          placeholder="费用名称"
        />
        <Select
          style={{ width: 190 }}
          value={rule.freight.type}
          options={FREIGHT_TYPES}
          onChange={(v) => updateRule(ruleIndex, { freight: { ...rule.freight, type: v } })}
        />
        {rule.freight.type === 'Fixed' && (
          <InputNumber
            min={0}
            precision={2}
            value={rule.freight.amount ?? undefined}
            onChange={(v) =>
              updateRule(ruleIndex, { freight: { ...rule.freight, amount: v ?? null } })
            }
            prefix="一口价 ¥"
            style={{ width: 170 }}
          />
        )}
        {(rule.freight.type === 'ByWeight' ||
          rule.freight.type === 'ByVolume' ||
          rule.freight.type === 'ByPieces') && (
          <InputNumber
            min={0}
            precision={2}
            value={rule.freight.unitPrice ?? undefined}
            onChange={(v) =>
              updateRule(ruleIndex, { freight: { ...rule.freight, unitPrice: v ?? null } })
            }
            prefix="单价 ¥"
            style={{ width: 160 }}
          />
        )}
        <InputNumber
          min={0}
          precision={2}
          value={rule.freight.minCharge ?? undefined}
          onChange={(v) =>
            updateRule(ruleIndex, { freight: { ...rule.freight, minCharge: v ?? null } })
          }
          prefix="起步价 ¥"
          style={{ width: 160 }}
        />
      </Space>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
        条件（全部满足才命中；客户专属规则用「客户 = 某客户」；不设条件 = 默认规则）
      </Typography.Paragraph>
      <Space direction="vertical" size={6} style={{ width: '100%', marginBottom: 8 }}>
        {rule.conditions.map((_, condIndex) => renderConditionRow(ruleIndex, condIndex))}
        <Button
          icon={<PlusOutlined />}
          onClick={() =>
            updateRule(ruleIndex, {
              conditions: [
                ...rule.conditions,
                { field: 'WeightKg', operator: 'GreaterOrEqual', value: '', value2: null },
              ],
            })
          }
        >
          添加条件
        </Button>
      </Space>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
        附加费用
      </Typography.Paragraph>
      <Space direction="vertical" size={6} style={{ width: '100%', marginBottom: 8 }}>
        {rule.surcharges.map((surcharge, sIndex) => (
          <Space key={sIndex} wrap size={8}>
            <Input
              style={{ width: 160 }}
              value={surcharge.name}
              placeholder="费用名称"
              onChange={(e) =>
                updateRule(ruleIndex, {
                  surcharges: rule.surcharges.map((x, i) =>
                    i === sIndex ? { ...x, name: e.target.value } : x,
                  ),
                })
              }
            />
            <Select
              style={{ width: 190 }}
              value={surcharge.basis}
              options={SURCHARGE_BASES}
              onChange={(v) =>
                updateRule(ruleIndex, {
                  surcharges: rule.surcharges.map((x, i) =>
                    i === sIndex
                      ? {
                          ...x,
                          basis: v,
                          rate: v === 'PerDeclaredValue' ? (x.rate ?? 0.5) : null,
                          amount: v === 'PerDeclaredValue' ? 0 : x.amount,
                        }
                      : x,
                  ),
                })
              }
            />
            {surcharge.basis === 'PerDeclaredValue' ? (
              <InputNumber
                min={0}
                precision={2}
                value={surcharge.rate ?? undefined}
                onChange={(v) =>
                  updateRule(ruleIndex, {
                    surcharges: rule.surcharges.map((x, i) =>
                      i === sIndex ? { ...x, rate: v ?? null } : x,
                    ),
                  })
                }
                prefix="费率"
                suffix="%"
                style={{ width: 200 }}
              />
            ) : (
              <InputNumber
                min={0}
                precision={2}
                value={surcharge.amount}
                onChange={(v) =>
                  updateRule(ruleIndex, {
                    surcharges: rule.surcharges.map((x, i) =>
                      i === sIndex ? { ...x, amount: v ?? 0 } : x,
                    ),
                  })
                }
                prefix="¥"
                style={{ width: 150 }}
              />
            )}
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              onClick={() =>
                updateRule(ruleIndex, {
                  surcharges: rule.surcharges.filter((_, i) => i !== sIndex),
                })
              }
            />
          </Space>
        ))}
        <Button
          icon={<PlusOutlined />}
          onClick={() =>
            updateRule(ruleIndex, {
              surcharges: [
                ...rule.surcharges,
                { name: '', basis: 'PerWaybill', amount: 0, rate: null },
              ],
            })
          }
        >
          添加附加费
        </Button>
      </Space>

      <Collapse
        items={[
          {
            key: 'json',
            label: '高级视图（当前规则 JSON，含阶梯/折扣）',
            children: (
              <Input.TextArea
                rows={6}
                style={{ fontFamily: 'monospace', fontSize: 12 }}
                value={JSON.stringify(rule, null, 2)}
                readOnly
              />
            ),
          },
        ]}
      />
    </Card>
  );

  if (versionsQuery.isLoading) {
    return (
      <div>
        <PageHeader title="规则版本" onBack={() => navigate('/pricing/rule-sets')} />
        <Card>
          <Spin tip="加载中…" />
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="规则版本"
        description="结构化配置规则、条件（含客户专属价格）与附加费；校验通过后发布，已发布版本不可修改。"
        onBack={() => navigate('/pricing/rule-sets')}
        actions={
          <Button
            type="primary"
            onClick={() => createMutation.mutate()}
            loading={createMutation.isPending}
          >
            新建草稿版本
          </Button>
        }
      />

      {(versionsQuery.data ?? []).length === 0 && (
        <Card>
          <EmptyState description="暂无版本，点击右上角「新建草稿版本」" />
        </Card>
      )}

      {versionsQuery.data?.map((version) => (
        <Card
          key={version.id}
          title={
            <Space size={10}>
              <span>v{version.versionNumber}</span>
              <StatusTag status={version.status} />
            </Space>
          }
          style={{ marginBottom: 16 }}
          extra={
            version.status === 'Draft' && editingId !== version.id ? (
              <Button type="link" onClick={() => startEdit(version)}>
                编辑
              </Button>
            ) : null
          }
        >
          {version.status === 'Draft' && editingId === version.id && draft ? (
            <Space direction="vertical" style={{ width: '100%' }}>
              {draft.rules.map((rule, ruleIndex) => renderRuleEditor(rule, ruleIndex))}
              <Button
                icon={<PlusOutlined />}
                onClick={() =>
                  setDraft((d) => {
                    if (!d) return d;
                    const nextPriority = Math.max(...d.rules.map((r) => r.priority)) + 1;
                    return {
                      ...d,
                      rules: [
                        ...d.rules,
                        {
                          priority: nextPriority,
                          name: `规则 ${nextPriority}`,
                          enabled: true,
                          billingWeightStrategy: 'WeightOnly',
                          conditions: [],
                          freight: { name: '运费', type: 'ByWeight', unitPrice: 1.5 },
                          surcharges: [],
                        },
                      ],
                    };
                  })
                }
              >
                添加规则
              </Button>
              <Space>
                <Button
                  onClick={() => {
                    setEditingId(null);
                    setDraft(null);
                  }}
                >
                  取消
                </Button>
                <Button loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
                  保存草稿
                </Button>
                <Button
                  loading={validateMutation.isPending}
                  onClick={() => validateMutation.mutate()}
                >
                  校验
                </Button>
                <Popconfirm
                  title="发布后版本不可修改，确认？"
                  onConfirm={() => publishMutation.mutate()}
                >
                  <Button type="primary" loading={publishMutation.isPending}>
                    发布
                  </Button>
                </Popconfirm>
              </Space>
            </Space>
          ) : (
            <Collapse
              items={[
                {
                  key: 'json',
                  label: '规则定义 JSON（只读）',
                  children: (
                    <Input.TextArea
                      rows={10}
                      readOnly
                      value={JSON.stringify(version.definition, null, 2)}
                      style={{ fontFamily: 'monospace', background: '#fafafa' }}
                    />
                  ),
                },
              ]}
            />
          )}
        </Card>
      ))}

      {versionsQuery.data?.length === 0 && <Empty />}
      <Typography.Paragraph type="secondary">
        条件字段与操作符为服务端白名单（AGENTS
        §6.4，禁止任意表达式）；优先级数值小者先匹配；发布前校验零错误方可发布。
      </Typography.Paragraph>
    </div>
  );
}
