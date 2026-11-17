import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Popconfirm, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { createRuleSet, disableRuleSet, enableRuleSet, listRuleSets, type PricingRuleSet } from '../api/pricing';

/** 计价规则集列表页（P3 前端）。 */
export function RuleSetListPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [form] = Form.useForm<{ code: string; name: string }>();

  const listQuery = useQuery({ queryKey: ['pricing-rule-sets'], queryFn: listRuleSets });

  const toggleMutation = useMutation({
    mutationFn: async ({ ruleSet, disable }: { ruleSet: PricingRuleSet; disable: boolean }) =>
      disable ? disableRuleSet(ruleSet.id) : enableRuleSet(ruleSet.id),
    onSuccess: () => {
      message.success('状态已更新');
      queryClient.invalidateQueries({ queryKey: ['pricing-rule-sets'] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const createMutation = useMutation({
    mutationFn: (values: { code: string; name: string }) => createRuleSet(values.code, values.name),
    onSuccess: (ruleSet) => {
      message.success('规则集已创建');
      setCreateOpen(false);
      queryClient.invalidateQueries({ queryKey: ['pricing-rule-sets'] });
      navigate(`/pricing/rule-sets/${ruleSet.id}/versions`);
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
          新建规则集
        </Button>
      </Space>
      <Table
        rowKey="id"
        dataSource={listQuery.data}
        loading={listQuery.isLoading}
        columns={[
          { title: '编码', dataIndex: 'code', key: 'code' },
          { title: '名称', dataIndex: 'name', key: 'name' },
          {
            title: '状态',
            dataIndex: 'status',
            key: 'status',
            render: (status: string) => (
              <Tag color={status === 'Active' ? 'green' : 'default'}>{status === 'Active' ? '启用' : '停用'}</Tag>
            ),
          },
          {
            title: '操作',
            key: 'actions',
            render: (_: unknown, record: PricingRuleSet) => (
              <Space>
                <Button type="link" size="small" onClick={() => navigate(`/pricing/rule-sets/${record.id}/versions`)}>
                  版本管理
                </Button>
                <Popconfirm
                  title={record.status === 'Active' ? '停用后该价卡全部版本停止匹配，确认？' : '确认启用？'}
                  onConfirm={() => toggleMutation.mutate({ ruleSet: record, disable: record.status === 'Active' })}
                >
                  <Button type="link" size="small" danger={record.status === 'Active'}>
                    {record.status === 'Active' ? '停用' : '启用'}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />

      {createOpen && (
        <Form form={form} layout="inline" onFinish={(values) => createMutation.mutate(values)} style={{ marginTop: 16 }}>
          <Form.Item name="code" rules={[{ required: true, message: '编码必填' }]}>
            <Input placeholder="规则集编码" />
          </Form.Item>
          <Form.Item name="name" rules={[{ required: true, message: '名称必填' }]}>
            <Input placeholder="规则集名称" />
          </Form.Item>
          <Button htmlType="submit" loading={createMutation.isPending}>
            创建
          </Button>
        </Form>
      )}
    </div>
  );
}
