import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, Modal, Popconfirm, Space, Table } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
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
      form.resetFields();
      queryClient.invalidateQueries({ queryKey: ['pricing-rule-sets'] });
      navigate(`/pricing/rule-sets/${ruleSet.id}/versions`);
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <PageHeader
        title="计价规则集"
        description="管理价卡规则集；每个规则集可发布多个版本，停用后全部版本停止匹配。"
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            新建规则集
          </Button>
        }
      />

      <Card styles={{ body: { padding: 0 } }}>
        <Table<PricingRuleSet>
          rowKey="id"
          dataSource={listQuery.data}
          loading={listQuery.isLoading}
          size="middle"
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState description="暂无规则集，点击右上角新建" /> }}
          columns={[
            { title: '编码', dataIndex: 'code', key: 'code', width: 160, render: (v: string) => <span className="ltl-code">{v}</span> },
            { title: '名称', dataIndex: 'name', key: 'name' },
            {
              title: '状态',
              dataIndex: 'status',
              key: 'status',
              width: 100,
              render: (status: string) => <StatusTag status={status} />,
            },
            {
              title: '操作',
              key: 'actions',
              width: 180,
              fixed: 'right',
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
      </Card>

      <Modal
        title="新建规则集"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => form.submit()}
        okText="创建"
        confirmLoading={createMutation.isPending}
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="code" label="编码" rules={[{ required: true, message: '编码必填' }]}>
            <Input placeholder="规则集编码" />
          </Form.Item>
          <Form.Item name="name" label="名称" rules={[{ required: true, message: '名称必填' }]}>
            <Input placeholder="规则集名称" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
