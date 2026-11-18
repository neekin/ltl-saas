import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  App,
  Button,
  Card,
  Flex,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Typography,
} from 'antd';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { EmptyState, OnBoardTag, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';
import {
  arriveTrip,
  departTrip,
  getTrip,
  listTripFees,
  loadWaybill,
  recordTripFee,
  removeWaybill,
} from '../api/trips';

interface WaybillOption {
  id: string;
  waybillNo: string;
  status: string;
  cargoStatus: string;
}

/** 配载工作台/批次详情页（P5 前端）：装车/撤销/发车/到达/费用。 */
export function TripDetailPage() {
  const { id } = useParams();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [loadWaybillNo, setLoadWaybillNo] = useState('');
  const [feeOpen, setFeeOpen] = useState(false);
  const [feeForm, setFeeForm] = useState({ feeType: 'fuel', amount: 0, note: '' });

  const tripQuery = useQuery({ queryKey: ['trip', id], queryFn: () => getTrip(id!) });
  const feesQuery = useQuery({ queryKey: ['trip-fees', id], queryFn: () => listTripFees(id!) });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['trip', id] });
    queryClient.invalidateQueries({ queryKey: ['trip-fees', id] });
    queryClient.invalidateQueries({ queryKey: ['trips'] });
  };

  const trip = tripQuery.data;
  const isPlanned = trip?.status === 'Planned';

  const loadMutation = useMutation({
    mutationFn: async () => {
      const response = await http.get<{ items: WaybillOption[] }>('/waybills', {
        params: { waybillNo: loadWaybillNo.trim(), pageSize: 1 },
      });
      const waybill = response.data.items[0];
      if (!waybill) {
        throw new Error('未找到该运单号');
      }
      await loadWaybill(id!, waybill.id);
    },
    onSuccess: () => {
      message.success('装车确认成功');
      setLoadWaybillNo('');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const removeMutation = useMutation({
    mutationFn: (assignmentId: string) => removeWaybill(id!, assignmentId),
    onSuccess: () => {
      message.success('已撤销配载');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const departMutation = useMutation({
    mutationFn: () => departTrip(id!),
    onSuccess: () => {
      message.success('已发车');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const arriveMutation = useMutation({
    mutationFn: () => arriveTrip(id!),
    onSuccess: () => {
      message.success('已到达');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const feeMutation = useMutation({
    mutationFn: () =>
      recordTripFee(id!, feeForm.feeType, feeForm.amount, feeForm.note || undefined),
    onSuccess: () => {
      message.success('费用已登记');
      setFeeForm({ feeType: 'fuel', amount: 0, note: '' });
      setFeeOpen(false);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const feesTotal = (feesQuery.data ?? []).reduce((sum, f) => sum + f.amount, 0);

  return (
    <div>
      <PageHeader
        title={
          <Space size={12} wrap>
            <span>配载工作台</span>
            {trip && (
              <span
                className="ltl-code"
                style={{ fontSize: 16, color: '#5b6573', fontWeight: 400 }}
              >
                {trip.tripNo}
              </span>
            )}
            {trip && <StatusTag status={trip.status} category="trip" />}
          </Space>
        }
        description="按运单号装车、撤销配载，并登记发车、到达与运输费用。"
        onBack={() => window.history.back()}
        actions={
          <Space wrap>
            {trip?.status === 'Planned' && (
              <Button
                type="primary"
                onClick={() => departMutation.mutate()}
                loading={departMutation.isPending}
              >
                发车
              </Button>
            )}
            {trip?.status === 'Departed' && (
              <Button
                type="primary"
                onClick={() => arriveMutation.mutate()}
                loading={arriveMutation.isPending}
              >
                到达
              </Button>
            )}
          </Space>
        }
      />

      <Card
        title="配载运单"
        extra={
          isPlanned && (
            <Flex gap={8} wrap>
              <Input
                placeholder="输入运单号装车"
                value={loadWaybillNo}
                onChange={(e) => setLoadWaybillNo(e.target.value)}
                onPressEnter={() => loadWaybillNo.trim() && loadMutation.mutate()}
                style={{ width: 240 }}
              />
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => loadMutation.mutate()}
                loading={loadMutation.isPending}
              >
                装车确认
              </Button>
            </Flex>
          )
        }
      >
        <Table
          rowKey="id"
          size="small"
          loading={tripQuery.isLoading}
          dataSource={trip?.waybills}
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState simple description="尚未配载运单" /> }}
          columns={[
            {
              title: '运单 ID',
              dataIndex: 'waybillId',
              ellipsis: true,
              render: (v: string) => <span className="ltl-code">{v}</span>,
            },
            {
              title: '在车',
              dataIndex: 'active',
              width: 100,
              render: (active: boolean) => <OnBoardTag active={active} />,
            },
            {
              title: '装车时间',
              dataIndex: 'loadedAt',
              width: 180,
              render: (v: string) => (
                <Typography.Text type="secondary">
                  {new Date(v).toLocaleString('zh-CN')}
                </Typography.Text>
              ),
            },
            {
              title: '操作',
              key: 'actions',
              width: 90,
              render: (_: unknown, record: { id: string; active: boolean }) =>
                isPlanned && record.active ? (
                  <Popconfirm title="撤销装车？" onConfirm={() => removeMutation.mutate(record.id)}>
                    <Button icon={<DeleteOutlined />} size="small" danger>
                      撤销
                    </Button>
                  </Popconfirm>
                ) : null,
            },
          ]}
        />
      </Card>

      <Card
        title={
          <Space size={10}>
            <span>运输费用</span>
            <Typography.Text type="secondary">合计</Typography.Text>
            <span className="ltl-amount ltl-amount--strong">¥ {feesTotal.toFixed(2)}</span>
          </Space>
        }
        style={{ marginTop: 16 }}
        extra={
          <Button icon={<PlusOutlined />} onClick={() => setFeeOpen(true)}>
            登记费用
          </Button>
        }
      >
        <Table
          rowKey="id"
          size="small"
          loading={feesQuery.isLoading}
          dataSource={feesQuery.data}
          pagination={false}
          scroll={{ x: 600 }}
          locale={{ emptyText: <EmptyState simple description="暂无费用记录" /> }}
          columns={[
            { title: '类型', dataIndex: 'feeType', width: 120 },
            {
              title: '金额',
              dataIndex: 'amount',
              width: 140,
              align: 'right',
              render: (v: number) => <span className="ltl-amount">¥ {v.toFixed(2)}</span>,
            },
            { title: '备注', dataIndex: 'note', render: (v: string | null) => v ?? '—' },
            {
              title: '时间',
              dataIndex: 'occurredAt',
              width: 180,
              render: (v: string) => (
                <Typography.Text type="secondary">
                  {new Date(v).toLocaleString('zh-CN')}
                </Typography.Text>
              ),
            },
          ]}
        />
      </Card>

      <Modal
        title="登记运输费用"
        open={feeOpen}
        onCancel={() => setFeeOpen(false)}
        onOk={() => feeMutation.mutate()}
        okText="登记"
        confirmLoading={feeMutation.isPending}
        destroyOnClose
      >
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Select
            value={feeForm.feeType}
            style={{ width: '100%' }}
            onChange={(feeType) => setFeeForm({ ...feeForm, feeType })}
            options={[
              { value: 'fuel', label: '燃油' },
              { value: 'toll', label: '过路费' },
              { value: 'driver', label: '司机费用' },
              { value: 'other', label: '其他' },
            ]}
          />
          <InputNumber
            min={0}
            precision={2}
            value={feeForm.amount}
            onChange={(v) => setFeeForm({ ...feeForm, amount: v ?? 0 })}
            addonBefore="金额（元）"
            style={{ width: '100%' }}
          />
          <Input
            placeholder="备注（可选）"
            value={feeForm.note}
            onChange={(e) => setFeeForm({ ...feeForm, note: e.target.value })}
          />
        </Space>
      </Modal>
    </div>
  );
}
