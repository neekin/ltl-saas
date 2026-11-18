import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Col, Form, Input, Row, Select, Space, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';
import {
  createPlan,
  createRoute,
  disableRoute,
  enableRoute,
  getRoute,
  listPlans,
  renameRoute,
  replaceStops,
  type TransportPlan,
} from '../api/routes';

interface BranchOption {
  id: string;
  code: string;
  name: string;
}

interface StopRow {
  branchId: string;
  sequence: number;
  remark?: string;
}

interface SegmentRow {
  fromBranchId: string;
  toBranchId: string;
  sequence: number;
}

/** 线路编辑页（P2 前端）：基本信息 + 站点排序编辑 + 运输方案配置。 */
export function RouteEditPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const isNew = !id;

  const branchQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
  });

  const routeQuery = useQuery({
    queryKey: ['route', id],
    queryFn: () => getRoute(id!),
    enabled: !isNew,
  });

  const plansQuery = useQuery({
    queryKey: ['route-plans', id],
    queryFn: () => listPlans(id!),
    enabled: !isNew,
  });

  const branchOptions = branchQuery.data ?? [];
  const [stops, setStops] = useState<StopRow[]>([]);
  const [stopsTouched, setStopsTouched] = useState(false);
  const [stopsInitializedFor, setStopsInitializedFor] = useState<string | null>(null);
  if (!isNew && routeQuery.data && stopsInitializedFor !== routeQuery.data.id) {
    setStopsInitializedFor(routeQuery.data.id);
    setStops(routeQuery.data.stops.map((s) => ({ branchId: s.branchId, sequence: s.sequence })));
  }

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['routes'] });
    queryClient.invalidateQueries({ queryKey: ['route', id] });
    queryClient.invalidateQueries({ queryKey: ['route-plans', id] });
  };

  const [baseForm] = Form.useForm<{ code: string; name: string }>();

  const createMutation = useMutation({
    mutationFn: (values: { code: string; name: string }) =>
      createRoute({
        code: values.code,
        name: values.name,
        originBranchId: stops[0]?.branchId ?? '',
        destBranchId: stops[stops.length - 1]?.branchId ?? '',
      }),
    onSuccess: (route) => {
      message.success('线路已创建，请继续编辑站点');
      invalidate();
      navigate(`/routes/${route.id}`, { replace: true });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const renameMutation = useMutation({
    mutationFn: (name: string) => renameRoute(id!, name),
    onSuccess: () => {
      message.success('已保存');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const stopsMutation = useMutation({
    mutationFn: () => replaceStops(id!, stops),
    onSuccess: () => {
      message.success('站点已更新');
      setStopsTouched(false);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const statusMutation = useMutation({
    mutationFn: async (active: boolean) => (active ? disableRoute(id!) : enableRoute(id!)),
    onSuccess: () => {
      message.success('状态已更新');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const planMutation = useMutation({
    mutationFn: (payload: { name: string; segments: SegmentRow[] }) => createPlan(id!, payload),
    onSuccess: () => {
      message.success('运输方案已创建');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const route = routeQuery.data;
  const sortedStops = [...stops].sort((a, b) => a.sequence - b.sequence);

  return (
    <div>
      <PageHeader
        title={isNew ? '新建线路' : '线路编辑'}
        description={
          isNew ? '创建线路后继续配置站点与运输方案。' : '维护线路基本信息、站点序列与运输方案。'
        }
        onBack={() => navigate('/routes')}
        actions={
          !isNew && route ? (
            <Space>
              <StatusTag status={route.status} />
              <Button
                danger={route.status === 'Active'}
                onClick={() => statusMutation.mutate(route.status === 'Active')}
              >
                {route.status === 'Active' ? '停用' : '启用'}
              </Button>
            </Space>
          ) : undefined
        }
      />

      <Card title="基本信息" style={{ marginBottom: 16 }}>
        <Form
          layout="vertical"
          form={baseForm}
          initialValues={{ code: route?.code, name: route?.name }}
        >
          <Row gutter={[16, 0]} align="bottom">
            <Col xs={24} md={5}>
              <Form.Item
                name="code"
                label="编码"
                rules={[{ required: isNew, message: '编码必填' }]}
              >
                <Input disabled={!isNew} placeholder="线路编码" />
              </Form.Item>
            </Col>
            <Col xs={24} md={7}>
              <Form.Item name="name" label="名称" rules={[{ required: true, message: '名称必填' }]}>
                <Input placeholder="线路名称" />
              </Form.Item>
            </Col>
            <Col xs={24} md={6}>
              <Form.Item label=" ">
                {isNew ? (
                  <Button
                    type="primary"
                    onClick={async () => {
                      const values = await baseForm.validateFields();
                      createMutation.mutate(values);
                    }}
                    loading={createMutation.isPending}
                  >
                    创建
                  </Button>
                ) : (
                  <Button
                    onClick={async () => {
                      const values = await baseForm.validateFields();
                      renameMutation.mutate(values.name);
                    }}
                    loading={renameMutation.isPending}
                  >
                    保存名称
                  </Button>
                )}
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      {!isNew && (
        <>
          <Card
            title="站点序列（sequence 从 0 起，首站=起点、末站=终点）"
            style={{ marginBottom: 16 }}
            extra={
              <Space>
                <Button
                  icon={<PlusOutlined />}
                  onClick={() => setStops([...stops, { branchId: '', sequence: stops.length }])}
                >
                  添加站点
                </Button>
                <Button
                  type="primary"
                  disabled={!stopsTouched || stops.length < 2}
                  loading={stopsMutation.isPending}
                  onClick={() => stopsMutation.mutate()}
                >
                  保存站点
                </Button>
              </Space>
            }
          >
            {sortedStops.length === 0 && (
              <EmptyState simple description="暂无站点，点击「添加站点」开始配置" />
            )}
            {sortedStops.map((stop, index) => (
              <Space key={index} style={{ display: 'flex', marginBottom: 8 }}>
                <Typography.Text type="secondary">{index}</Typography.Text>
                <Select
                  value={stop.branchId || undefined}
                  placeholder="选择网点"
                  showSearch
                  optionFilterProp="label"
                  style={{ width: 220 }}
                  options={branchOptions.map((b) => ({
                    value: b.id,
                    label: `${b.code} ${b.name}`,
                  }))}
                  onChange={(branchId) => {
                    const next = [...stops];
                    next[stops.indexOf(stop)] = { ...stop, branchId };
                    setStops(next);
                    setStopsTouched(true);
                  }}
                />
                <Button
                  icon={<DeleteOutlined />}
                  disabled={stops.length <= 2}
                  onClick={() => {
                    setStops(
                      stops.filter((s) => s !== stop).map((s, i) => ({ ...s, sequence: i })),
                    );
                    setStopsTouched(true);
                  }}
                />
              </Space>
            ))}
          </Card>

          <PlanSection
            routeId={id!}
            plans={plansQuery.data ?? []}
            branchOptions={branchQuery.data ?? []}
            onCreate={planMutation.mutate}
            creating={planMutation.isPending}
          />
        </>
      )}
    </div>
  );
}

function PlanSection({
  plans,
  branchOptions,
  onCreate,
  creating,
}: {
  routeId: string;
  plans: TransportPlan[];
  branchOptions: BranchOption[];
  onCreate: (payload: { name: string; segments: SegmentRow[] }) => void;
  creating: boolean;
}) {
  const [planName, setPlanName] = useState('');
  const [segments, setSegments] = useState<SegmentRow[]>([
    { fromBranchId: '', toBranchId: '', sequence: 1 },
  ]);

  return (
    <Card title="运输方案（1 区段=直达，多区段=中转；区段须首尾相连且落在站点内）">
      {plans.map((plan) => (
        <div key={plan.id} style={{ marginBottom: 12 }}>
          <Space size={8}>
            <Typography.Text strong>{plan.name}</Typography.Text>
            <StatusTag status={plan.status} />
          </Space>{' '}
          <div style={{ paddingLeft: 16 }}>
            {plan.segments.map((segment) => (
              <Typography.Text key={segment.id} type="secondary" style={{ marginRight: 12 }}>
                {segment.sequence}.{' '}
                {branchOptions.find((b) => b.id === segment.fromBranchId)?.name ?? '?'} →{' '}
                {branchOptions.find((b) => b.id === segment.toBranchId)?.name ?? '?'}
              </Typography.Text>
            ))}
          </div>
        </div>
      ))}
      {plans.length === 0 && <EmptyState simple description="暂无运输方案" />}

      <Typography.Title level={5}>新建方案</Typography.Title>
      <Input
        placeholder="方案名称"
        value={planName}
        onChange={(e) => setPlanName(e.target.value)}
        style={{ width: 240, marginBottom: 8 }}
      />
      {segments.map((segment, index) => (
        <Space key={index} style={{ display: 'flex', marginBottom: 8 }}>
          <Typography.Text type="secondary">{index + 1}.</Typography.Text>
          <Select
            placeholder="起点"
            showSearch
            optionFilterProp="label"
            style={{ width: 180 }}
            options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
            value={segment.fromBranchId || undefined}
            onChange={(fromBranchId) => {
              const next = [...segments];
              next[index] = { ...segment, fromBranchId };
              setSegments(next);
            }}
          />
          <Select
            placeholder="终点"
            showSearch
            optionFilterProp="label"
            style={{ width: 180 }}
            options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
            value={segment.toBranchId || undefined}
            onChange={(toBranchId) => {
              const next = [...segments];
              next[index] = { ...segment, toBranchId };
              setSegments(next);
            }}
          />
          <Button
            icon={<DeleteOutlined />}
            disabled={segments.length <= 1}
            onClick={() =>
              setSegments(
                segments.filter((_, i) => i !== index).map((s, i) => ({ ...s, sequence: i + 1 })),
              )
            }
          />
        </Space>
      ))}
      <Space style={{ marginBottom: 8 }}>
        <Button
          icon={<PlusOutlined />}
          onClick={() => {
            const last = segments[segments.length - 1];
            setSegments([
              ...segments,
              {
                fromBranchId: last?.toBranchId ?? '',
                toBranchId: '',
                sequence: segments.length + 1,
              },
            ]);
          }}
        >
          添加区段
        </Button>
        <Button
          type="primary"
          loading={creating}
          disabled={!planName.trim() || segments.some((s) => !s.fromBranchId || !s.toBranchId)}
          onClick={() =>
            onCreate({
              name: planName.trim(),
              segments: segments.map((s, i) => ({
                fromBranchId: s.fromBranchId,
                toBranchId: s.toBranchId,
                sequence: i + 1,
              })),
            })
          }
        >
          创建方案
        </Button>
      </Space>
    </Card>
  );
}
