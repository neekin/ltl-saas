import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { Checkbox, InputNumber } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  App,
  Button,
  Card,
  Drawer,
  Flex,
  Form,
  Input,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import {
  addAddress,
  addContact,
  createCustomer,
  disableCustomer,
  enableCustomer,
  getCustomer,
  grantBranch,
  listCustomers,
  removeAddress,
  removeContact,
  revokeBranch,
  setCreditPolicy,
  updateCustomer,
  type Customer,
  type CustomerDetail,
} from '../api/customers';
import { http } from '../../../shared/api/http';

interface BranchOption {
  id: string;
  code: string;
  name: string;
  status: string;
}

/** 客户管理页（P2 前端）：列表/搜索/创建/编辑/启停 + 详情抽屉（联系人/地址/网点授权）。 */
export function CustomerListPage() {
  const navigate = useNavigate();
  const { id: routeIdParam } = useParams();
  const { message } = App.useApp();
  const queryClient = useQueryClient();

  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState('');
  const [searchText, setSearchText] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [detailId, setDetailId] = useState<string | null>(routeIdParam ?? null);

  const listQuery = useQuery({
    queryKey: ['customers', page, keyword],
    queryFn: () => listCustomers(page, 10, keyword || undefined),
  });

  const detailQuery = useQuery({
    queryKey: ['customer', detailId],
    queryFn: () => getCustomer(detailId!),
    enabled: detailId !== null,
  });

  const branchQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
    enabled: detailId !== null,
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['customers'] });
    queryClient.invalidateQueries({ queryKey: ['customer', detailId] });
  };

  const createMutation = useMutation({
    mutationFn: (values: { code: string; name: string; remark?: string }) =>
      createCustomer(values.code, values.name, values.remark),
    onSuccess: (customer) => {
      message.success('客户创建成功');
      setCreateOpen(false);
      invalidate();
      setDetailId(customer.id);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, name, remark }: { id: string; name: string; remark?: string }) =>
      updateCustomer(id, name, remark),
    onSuccess: () => {
      message.success('已更新');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) =>
      active ? disableCustomer(id) : enableCustomer(id),
    onSuccess: () => {
      message.success('状态已更新');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const detailMutationWrapper = async (action: () => Promise<unknown>, successText: string) => {
    try {
      await action();
      message.success(successText);
      invalidate();
    } catch (e) {
      message.error(e instanceof Error ? e.message : '操作失败');
    }
  };

  const detail = detailQuery.data;

  const columns = [
    {
      title: '编码',
      dataIndex: 'code',
      key: 'code',
      width: 130,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    { title: '名称', dataIndex: 'name', key: 'name' },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <StatusTag status={status} />,
    },
    { title: '备注', dataIndex: 'remark', key: 'remark', ellipsis: true },
    {
      title: '月结额度',
      key: 'credit',
      width: 150,
      render: (_: unknown, record: Customer) =>
        record.creditLimit !== null ? (
          <span>
            ¥ {record.creditLimit.toFixed(2)}
            {record.paymentTermsDays !== null && (
              <Typography.Text type="secondary">
                （账期 {record.paymentTermsDays} 天）
              </Typography.Text>
            )}
          </span>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: '操作',
      key: 'actions',
      render: (_: unknown, record: Customer) => (
        <Space>
          <Button
            type="link"
            size="small"
            onClick={() => {
              setDetailId(record.id);
              navigate(`/customers/${record.id}`);
            }}
          >
            详情
          </Button>
          <Popconfirm
            title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
            onConfirm={() =>
              toggleMutation.mutate({ id: record.id, active: record.status === 'Active' })
            }
          >
            <Button type="link" size="small" danger={record.status === 'Active'}>
              {record.status === 'Active' ? '停用' : '启用'}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="客户管理"
        description="维护客户档案、联系人、地址与可服务网点授权。"
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            新建客户
          </Button>
        }
      />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input.Search
            placeholder="搜索编码/名称"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            onSearch={(value) => {
              setKeyword(value);
              setPage(1);
            }}
            style={{ width: 280 }}
            allowClear
          />
          <Button icon={<ReloadOutlined />} onClick={() => invalidate()} />
        </Flex>
      </div>

      <Card styles={{ body: { padding: 0 } }}>
        <Table
          rowKey="id"
          columns={columns}
          dataSource={listQuery.data?.items}
          loading={listQuery.isLoading}
          size="middle"
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState description="暂无客户，点击右上角「新建客户」" /> }}
          pagination={{
            current: page,
            pageSize: 10,
            total: listQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
            showSizeChanger: false,
          }}
        />
      </Card>

      <CreateCustomerDrawer
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
        submitting={createMutation.isPending}
      />

      <Drawer
        title="客户详情"
        width={720}
        open={detailId !== null}
        onClose={() => {
          setDetailId(null);
          navigate('/customers');
        }}
        destroyOnHidden
      >
        {detailQuery.isLoading || !detail ? (
          <Typography.Text type="secondary">加载中…</Typography.Text>
        ) : (
          <CustomerDetailBody
            detail={detail}
            branchOptions={branchQuery.data ?? []}
            onUpdate={(values) => updateMutation.mutate({ id: detail.id, ...values })}
            onCreditPolicySave={(values) =>
              detailMutationWrapper(() => setCreditPolicy(detail.id, values), '信用政策已更新')
            }
            onContactAdd={(values) =>
              detailMutationWrapper(() => addContact(detail.id, values), '联系人已添加')
            }
            onContactRemove={(contactId) =>
              detailMutationWrapper(() => removeContact(detail.id, contactId), '联系人已移除')
            }
            onAddressAdd={(values) =>
              detailMutationWrapper(() => addAddress(detail.id, values), '地址已添加')
            }
            onAddressRemove={(addressId) =>
              detailMutationWrapper(() => removeAddress(detail.id, addressId), '地址已移除')
            }
            onBranchGrant={(branchId) =>
              detailMutationWrapper(() => grantBranch(detail.id, branchId), '已授权')
            }
            onBranchRevoke={(branchId) =>
              detailMutationWrapper(() => revokeBranch(detail.id, branchId), '已收回')
            }
          />
        )}
      </Drawer>
    </div>
  );
}

function CreateCustomerDrawer({
  open,
  onClose,
  onSubmit,
  submitting,
}: {
  open: boolean;
  onClose: () => void;
  onSubmit: (values: { code: string; name: string; remark?: string }) => void;
  submitting: boolean;
}) {
  const [form] = Form.useForm();

  return (
    <Drawer
      title="新建客户"
      width={480}
      open={open}
      onClose={onClose}
      destroyOnHidden
      extra={
        <Button type="primary" loading={submitting} onClick={() => form.submit()}>
          保存
        </Button>
      }
    >
      <Form form={form} layout="vertical" onFinish={onSubmit}>
        <Form.Item name="code" label="客户编码" rules={[{ required: true, message: '请输入编码' }]}>
          <Input placeholder="如 KH001" />
        </Form.Item>
        <Form.Item name="name" label="客户名称" rules={[{ required: true, message: '请输入名称' }]}>
          <Input />
        </Form.Item>
        <Form.Item name="remark" label="备注">
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Drawer>
  );
}

function CustomerDetailBody({
  detail,
  branchOptions,
  onUpdate,
  onCreditPolicySave,
  onContactAdd,
  onContactRemove,
  onAddressAdd,
  onAddressRemove,
  onBranchGrant,
  onBranchRevoke,
}: {
  detail: CustomerDetail;
  branchOptions: BranchOption[];
  onUpdate: (values: { name: string; remark?: string }) => void;
  onCreditPolicySave: (values: {
    creditLimit: number | null;
    paymentTermsDays: number | null;
  }) => void;
  onContactAdd: (values: {
    name: string;
    phone: string;
    isPrimary: boolean;
    remark?: string;
  }) => void;
  onContactRemove: (contactId: string) => void;
  onAddressAdd: (values: {
    address: string;
    label?: string;
    isDefault: boolean;
    remark?: string;
  }) => void;
  onAddressRemove: (addressId: string) => void;
  onBranchGrant: (branchId: string) => void;
  onBranchRevoke: (branchId: string) => void;
}) {
  const { message } = App.useApp();
  const [updateForm] = Form.useForm();
  const [contactForm] = Form.useForm();
  const [addressForm] = Form.useForm();
  const [creditForm] = Form.useForm();
  const availableBranches = branchOptions.filter((b) => !detail.servicedBranchIds.includes(b.id));

  return (
    <div>
      <Typography.Paragraph>
        编码 <Typography.Text code>{detail.code}</Typography.Text>{' '}
        <StatusTag status={detail.status} />
      </Typography.Paragraph>

      <Typography.Title level={5}>基本信息</Typography.Title>
      <Form
        form={updateForm}
        layout="inline"
        initialValues={{ name: detail.name, remark: detail.remark ?? undefined }}
        onFinish={onUpdate}
        style={{ marginBottom: 16, flexWrap: 'wrap', gap: 8 }}
      >
        <Form.Item name="name" rules={[{ required: true, message: '名称必填' }]}>
          <Input style={{ width: 220 }} />
        </Form.Item>
        <Form.Item name="remark">
          <Input placeholder="备注" style={{ width: 200 }} />
        </Form.Item>
        <Button htmlType="submit">保存</Button>
      </Form>

      <Typography.Title level={5}>月结信用政策</Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        额度为开单前置校验依据（未收余额超过额度将拒绝月结开单）；留空表示不启用额度检查。
      </Typography.Paragraph>
      <Form
        form={creditForm}
        layout="inline"
        initialValues={{
          creditLimit: detail.creditLimit ?? undefined,
          paymentTermsDays: detail.paymentTermsDays ?? undefined,
        }}
        onFinish={(values) => {
          onCreditPolicySave({
            creditLimit: values.creditLimit ?? null,
            paymentTermsDays: values.paymentTermsDays ?? null,
          });
        }}
        style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}
      >
        <Form.Item
          name="creditLimit"
          label="信用额度"
          rules={[
            () => ({
              validator: (_, value) =>
                value === undefined || value === null || value >= 0
                  ? Promise.resolve()
                  : Promise.reject(new Error('信用额度不能为负数')),
            }),
          ]}
        >
          <InputNumber min={0} precision={2} placeholder="元" style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="paymentTermsDays" label="账期">
          <InputNumber min={0} precision={0} suffix="天" style={{ width: 140 }} />
        </Form.Item>
        <Button htmlType="submit">保存信用政策</Button>
      </Form>

      <Typography.Title level={5}>联系人</Typography.Title>
      <ul style={{ paddingLeft: 16 }}>
        {detail.contacts.map((c) => (
          <li key={c.id} style={{ marginBottom: 4 }}>
            {c.isPrimary && <Tag color="blue">主联系人</Tag>}
            {c.name} · {c.phone}
            {c.remark && <Typography.Text type="secondary">（{c.remark}）</Typography.Text>}
            <Button type="link" size="small" danger onClick={() => onContactRemove(c.id)}>
              移除
            </Button>
          </li>
        ))}
        {detail.contacts.length === 0 && (
          <Typography.Text type="secondary">暂无联系人</Typography.Text>
        )}
      </ul>
      <Form
        form={contactForm}
        layout="inline"
        onFinish={(values) => {
          onContactAdd(values);
          contactForm.resetFields();
        }}
        style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}
      >
        <Form.Item name="name" rules={[{ required: true, message: '姓名必填' }]}>
          <Input placeholder="姓名" style={{ width: 120 }} />
        </Form.Item>
        <Form.Item name="phone" rules={[{ required: true, message: '电话必填' }]}>
          <Input placeholder="电话" style={{ width: 140 }} />
        </Form.Item>
        <Form.Item name="isPrimary" valuePropName="checked">
          <Checkbox>主联系人</Checkbox>
        </Form.Item>
        <Button htmlType="submit">添加</Button>
      </Form>

      <Typography.Title level={5}>地址</Typography.Title>
      <ul style={{ paddingLeft: 16 }}>
        {detail.addresses.map((a) => (
          <li key={a.id} style={{ marginBottom: 4 }}>
            {a.isDefault && <Tag color="blue">默认</Tag>}
            {a.address}
            {a.label && <Tag>{a.label}</Tag>}
            <Button type="link" size="small" danger onClick={() => onAddressRemove(a.id)}>
              移除
            </Button>
          </li>
        ))}
        {detail.addresses.length === 0 && (
          <Typography.Text type="secondary">暂无地址</Typography.Text>
        )}
      </ul>
      <Form
        form={addressForm}
        layout="inline"
        onFinish={(values) => {
          onAddressAdd(values);
          addressForm.resetFields();
        }}
        style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}
      >
        <Form.Item name="address" rules={[{ required: true, message: '地址必填' }]}>
          <Input placeholder="详细地址" style={{ width: 280 }} />
        </Form.Item>
        <Form.Item name="label">
          <Input placeholder="标签" style={{ width: 100 }} />
        </Form.Item>
        <Form.Item name="isDefault" valuePropName="checked">
          <Checkbox>默认</Checkbox>
        </Form.Item>
        <Button htmlType="submit">添加</Button>
      </Form>

      <Typography.Title level={5}>可服务网点</Typography.Title>
      <Space wrap style={{ marginBottom: 8 }}>
        {detail.servicedBranchIds.map((branchId) => {
          const option = branchOptions.find((b) => b.id === branchId);
          return (
            <Tag key={branchId} closable onClose={() => onBranchRevoke(branchId)}>
              {option?.name ?? branchId}
            </Tag>
          );
        })}
        {detail.servicedBranchIds.length === 0 && (
          <Typography.Text type="secondary">未授权网点</Typography.Text>
        )}
      </Space>
      <Select
        placeholder="添加可服务网点"
        style={{ width: 220 }}
        showSearch
        optionFilterProp="label"
        options={availableBranches.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
        onChange={(branchId) => {
          if (branchId) {
            onBranchGrant(branchId);
            message.success('已授权');
          }
        }}
        value={null}
      />
    </div>
  );
}
