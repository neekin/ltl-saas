import { DeleteOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, InputNumber, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  arriveTrip,
  departTrip,
  getTrip,
  listTripFees,
  loadWaybill,
  recordTripFee,
  removeWaybill,
} from '../api/trips';
import { http } from '../../../shared/api/http';

interface WaybillOption {
  id: string;
  waybillNo: string;
  status: string;
  cargoStatus: string;
}

function statusTag(status: string) {
  const map: Record<string, { color: string; text: string }> = {
    Planned: { color: 'orange', text: '已计划' },
    Departed: { color: 'blue', text: '已发车' },
    Arrived: { color: 'green', text: '已到达' },
  };
  const item = map[status];
  return item ? <Tag color={item.color}>{item.text}</Tag> : <Tag>{status}</Tag>;
}

/** 配载工作台/批次详情页（P5 前端）：装车/撤销/发车/到达/费用。 */
export function TripDetailPage() {
  const { id } = useParams();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [loadWaybillNo, setLoadWaybillNo] = useState('');
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
      // 按运单号查找运单（列表搜索），取第一个匹配
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
    mutationFn: () => recordTripFee(id!, feeForm.feeType, feeForm.amount, feeForm.note || undefined),
    onSuccess: () => {
      message.success('费用已登记');
      setFeeForm({ feeType: 'fuel', amount: 0, note: '' });
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const feesTotal = (feesQuery.data ?? []).reduce((sum, f) => sum + f.amount, 0);

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button onClick={() => window.history.back()}>返回</Button>
        {trip && <Typography.Title level={4} style={{ margin: 0 }}>批次 {trip.tripNo} {statusTag(trip.status)}</Typography.Title>}
      </Space>

      <Card title="配载运单" style={{ marginBottom: 16 }} extra={
        isPlanned && (
          <Space>
            <Input
              placeholder="运单号"
              value={loadWaybillNo}
              onChange={(e) => setLoadWaybillNo(e.target.value)}
              style={{ width: 220 }}
            />
            <Button onClick={() => loadMutation.mutate()} loading={loadMutation.isPending}>
              装车确认
            </Button>
          </Space>
        )
      }>
        <Table
          rowKey="id"
          size="small"
          loading={tripQuery.isLoading}
          dataSource={trip?.waybills}
          columns={[
            { title: '运单 ID', dataIndex: 'waybillId', ellipsis: true },
            {
              title: '在车',
              dataIndex: 'active',
              width: 80,
              render: (active: boolean) => (active ? <Tag color="green">在车</Tag> : <Tag>已卸</Tag>),
            },
            {
              title: '装车时间',
              dataIndex: 'loadedAt',
              render: (v: string) => new Date(v).toLocaleString('zh-CN'),
            },
            {
              title: '操作',
              key: 'actions',
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
        <Space style={{ marginTop: 12 }}>
          {trip?.status === 'Planned' && (
            <Button type="primary" onClick={() => departMutation.mutate()} loading={departMutation.isPending}>
              发车
            </Button>
          )}
          {trip?.status === 'Departed' && (
            <Button type="primary" onClick={() => arriveMutation.mutate()} loading={arriveMutation.isPending}>
              到达
            </Button>
          )}
        </Space>
      </Card>

      <Card title={`运输费用（合计 ${feesTotal.toFixed(2)} 元）`} extra={
        <Space>
          <Select
            value={feeForm.feeType}
            style={{ width: 120 }}
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
            value={feeForm.amount}
            onChange={(v) => setFeeForm({ ...feeForm, amount: v ?? 0 })}
            addonBefore="金额"
            style={{ width: 160 }}
          />
          <Input
            placeholder="备注"
            value={feeForm.note}
            onChange={(e) => setFeeForm({ ...feeForm, note: e.target.value })}
            style={{ width: 160 }}
          />
          <Button onClick={() => feeMutation.mutate()} loading={feeMutation.isPending}>
            登记
          </Button>
        </Space>
      }>
        <Table
          rowKey="id"
          size="small"
          loading={feesQuery.isLoading}
          dataSource={feesQuery.data}
          pagination={false}
          columns={[
            { title: '类型', dataIndex: 'feeType', width: 120 },
            { title: '金额', dataIndex: 'amount', width: 120, render: (v: number) => `${v.toFixed(2)} 元` },
            { title: '备注', dataIndex: 'note' },
            {
              title: '时间',
              dataIndex: 'occurredAt',
              render: (v: string) => new Date(v).toLocaleString('zh-CN'),
            },
          ]}
        />
      </Card>
    </div>
  );
}
