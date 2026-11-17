import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, InputNumber, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  cancelTransfer,
  dispatchTransfer,
  getTransfer,
  receiveTransfer,
  submitTransfer,
  type BranchTransferItem,
} from '../api/transfers';

/** 调拨单详情页（P5 前端）：提交/发出/验收（逐项数量核验 + 差异留痕）。 */
export function TransferDetailPage() {
  const { id } = useParams();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [receivedQuantities, setReceivedQuantities] = useState<Record<string, number>>({});
  const [brokenQuantities, setBrokenQuantities] = useState<Record<string, number>>({});
  const [remarks, setRemarks] = useState<Record<string, string>>({});

  const detailQuery = useQuery({ queryKey: ['transfer', id], queryFn: () => getTransfer(id!) });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['transfer', id] });
    queryClient.invalidateQueries({ queryKey: ['transfers'] });
  };

  const submitMutation = useMutation({
    mutationFn: () => submitTransfer(id!),
    onSuccess: () => {
      message.success('已提交');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const dispatchMutation = useMutation({
    mutationFn: () => dispatchTransfer(id!),
    onSuccess: () => {
      message.success('已发出确认');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const receiveMutation = useMutation({
    mutationFn: () =>
      receiveTransfer(
        id!,
        (detailQuery.data?.items ?? []).map((item) => ({
          itemId: item.id,
          receivedQuantity: receivedQuantities[item.id] ?? 0,
          brokenQuantity: brokenQuantities[item.id] ?? 0,
          remark: remarks[item.id] || undefined,
        })),
      ),
    onSuccess: () => {
      message.success('验收完成（差异已留痕）');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelTransfer(id!),
    onSuccess: () => {
      message.success('已取消');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const status = detailQuery.data?.status;
  const canSubmit = status === 'Draft';
  const canDispatch = status === 'Submitted';
  const canReceive = status === 'Dispatched';
  const canCancel = status === 'Draft' || status === 'Submitted';

  const columns: ColumnsType<BranchTransferItem> = [
    { title: '序号', dataIndex: 'seq', width: 60 },
    { title: '货物', dataIndex: 'cargoName' },
    { title: '申报数量', dataIndex: 'declaredQuantity', width: 100 },
    {
      title: '实收数量',
      key: 'received',
      width: 140,
      render: (_, record) =>
        record.receivedQuantity === null ? (
          <InputNumber
            min={0}
            max={record.declaredQuantity}
            precision={0}
            value={receivedQuantities[record.id] ?? 0}
            onChange={(v) => setReceivedQuantities({ ...receivedQuantities, [record.id]: v ?? 0 })}
            style={{ width: 120 }}
          />
        ) : (
          record.receivedQuantity
        ),
    },
    {
      title: '破损',
      key: 'broken',
      width: 120,
      render: (_, record) =>
        record.receivedQuantity === null ? (
          <InputNumber
            min={0}
            precision={0}
            value={brokenQuantities[record.id] ?? 0}
            onChange={(v) => setBrokenQuantities({ ...brokenQuantities, [record.id]: v ?? 0 })}
            style={{ width: 100 }}
          />
        ) : (
          record.brokenQuantity
        ),
    },
    {
      title: '差异',
      key: 'diff',
      width: 100,
      render: (_, record) =>
        record.receivedQuantity === null ? '—' : `${record.receivedQuantity - record.declaredQuantity}`,
    },
    {
      title: '验收备注',
      key: 'remark',
      render: (_, record) =>
        record.receivedQuantity === null ? (
          <Input
            value={remarks[record.id] ?? ''}
            onChange={(e) => setRemarks({ ...remarks, [record.id]: e.target.value })}
            placeholder="短少/破损说明"
          />
        ) : (
          record.remark
        ),
    },
  ];

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button onClick={() => window.history.back()}>返回</Button>
        {detailQuery.data && (
          <Typography.Title level={4} style={{ margin: 0 }}>
            调拨单 {detailQuery.data.transferNo}（{status}）
          </Typography.Title>
        )}
      </Space>

      <Card
        title="货物清单与验收"
        style={{ marginBottom: 16 }}
        extra={
          <Space>
            {canSubmit && <Button onClick={() => submitMutation.mutate()}>提交</Button>}
            {canDispatch && <Button type="primary" onClick={() => dispatchMutation.mutate()}>发出确认</Button>}
            {canReceive && (
              <Button type="primary" onClick={() => receiveMutation.mutate()}>
                验收入库
              </Button>
            )}
            {canCancel && (
              <Button danger onClick={() => cancelMutation.mutate()}>
                取消
              </Button>
            )}
          </Space>
        }
      >
        <Table
          rowKey="id"
          size="small"
          loading={detailQuery.isLoading}
          dataSource={detailQuery.data?.items}
          pagination={false}
          columns={columns}
        />
        <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
          验收一次性写入、不可覆盖（AGENTS §6.3：原始交接记录不可覆盖）；破损计入实收；调拨不产生对外销售收入。
        </Typography.Paragraph>
      </Card>
    </div>
  );
}
