import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, Popconfirm, Space, Spin, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import {
  createDraftVersion,
  listVersions,
  publishVersion,
  updateDraftVersion,
  validateVersion,
  type PricingDefinition,
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

/** 规则集版本管理页（P3 前端）：版本列表 + 定义 JSON 编辑 + 校验/发布。 */
export function RuleSetVersionsPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftText, setDraftText] = useState('');

  const versionsQuery = useQuery({
    queryKey: ['pricing-versions', id],
    queryFn: () => listVersions(id!),
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
    setDraftText(JSON.stringify(version.definition, null, 2));
  };

  const saveMutation = useMutation({
    mutationFn: () => {
      const definition = JSON.parse(draftText) as PricingDefinition;
      return updateDraftVersion(editingId!, definition);
    },
    onSuccess: () => {
      message.success('草稿已保存');
      setEditingId(null);
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
        message.error(`校验未通过（${errors.length} 项），见页面提示`);
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
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

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
        description="编辑规则的声明式定义 JSON，校验通过后发布；已发布版本不可修改。"
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
          {editingId === version.id ? (
            <Space direction="vertical" style={{ width: '100%' }}>
              <Input.TextArea
                rows={16}
                value={draftText}
                onChange={(e) => setDraftText(e.target.value)}
                style={{ fontFamily: 'monospace' }}
              />
              <Space>
                <Button onClick={() => setEditingId(null)}>取消</Button>
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
            <Input.TextArea
              rows={8}
              readOnly
              value={JSON.stringify(version.definition, null, 2)}
              style={{ fontFamily: 'monospace', background: '#fafafa' }}
            />
          )}
        </Card>
      ))}

      <Typography.Paragraph type="secondary">
        规则定义结构见 PRICE-001 设计文档；发布前校验零错误方可发布；已发布版本不可修改。
      </Typography.Paragraph>
    </div>
  );
}
