import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Descriptions, Input, InputNumber, Popconfirm, Space, Table, Tag, Timeline, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  cancelWaybill,
  confirmWaybill,
  createSortingTask,
  getWaybill,
  listWaybillEvents,
  recordReceipt,
} from '../api/waybills';

const eventTypeText: Record<string, string> = {
  Created: '开单',
  Confirmed: '确认',
  Cancelled: '取消',
  Received: '收货登记',
  Sorted: '分拣完成',
  Departed: '发车',
  Arrived: '到达',
};

/** 运单详情页（P4 前端）：状态/货物件/收货登记/分拣/轨迹时间线/打印。 */
export function WaybillDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [receiptOpen, setReceiptOpen] = useState(false);
  const [receiptForm, setReceiptForm] = useState({ pieces: 1, weight: 0, volume: 0, note: '' });

  const detailQuery = useQuery({
    queryKey: ['waybill', id],
    queryFn: () => getWaybill(id!),
  });

  const eventsQuery = useQuery({
    queryKey: ['waybill-events', id],
    queryFn: () => listWaybillEvents(id!),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['waybill', id] });
    queryClient.invalidateQueries({ queryKey: ['waybill-events', id] });
    queryClient.invalidateQueries({ queryKey: ['waybills'] });
  };

  const confirmMutation = useMutation({
    mutationFn: () => confirmWaybill(id!),
    onSuccess: () => {
      message.success('已确认');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const cancelMutation = useMutation({
    mutationFn: (reason: string) => cancelWaybill(id!, reason),
    onSuccess: () => {
      message.success('已取消');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const receiptMutation = useMutation({
    mutationFn: () =>
      recordReceipt(id!, detailQuery.data!.originBranchId, {
        receivedPieces: receiptForm.pieces,
        receivedWeightKg: receiptForm.weight,
        receivedVolumeM3: receiptForm.volume,
        note: receiptForm.note || undefined,
      }),
    onSuccess: (result) => {
      message.success(
        `收货登记成功（差异：件数 ${result.pieceDifference} / 重量 ${result.weightDifferenceKg}kg / 体积 ${result.volumeDifferenceM3}m³）`,
      );
      setReceiptOpen(false);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const sortMutation = useMutation({
    mutationFn: () => createSortingTask(id!, detailQuery.data!.originBranchId, '页面分拣'),
    onSuccess: () => {
      message.success('分拣任务已创建，请在分拣作业页完成');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  if (detailQuery.isLoading || !detailQuery.data) {
    return <Typography.Text type="secondary">加载中…</Typography.Text>;
  }

  const waybill = detailQuery.data;
  const canConfirm = waybill.status === 'Draft';
  const canCancel = waybill.status === 'Draft' || (waybill.status === 'Confirmed' && waybill.transportStatus === 'NotShipped');
  const canReceive = waybill.status === 'Confirmed' && waybill.cargoStatus === 'PendingReceive';
  const canSort = waybill.status === 'Confirmed' && waybill.cargoStatus === 'Received';

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button onClick={() => navigate('/waybills')}>返回列表</Button>
        {canConfirm && (
          <Button type="primary" onClick={() => confirmMutation.mutate()} loading={confirmMutation.isPending}>
            确认运单
          </Button>
        )}
        {canReceive && (
          <Button type="primary" onClick={() => setReceiptOpen(true)}>
            收货登记
          </Button>
        )}
        {canSort && (
          <Button onClick={() => sortMutation.mutate()} loading={sortMutation.isPending}>
            创建分拣任务
          </Button>
        )}
        {canCancel && (
          <Popconfirm title="确认取消该运单？" onConfirm={() => cancelMutation.mutate('页面取消')}>
            <Button danger>取消运单</Button>
          </Popconfirm>
        )}
        <Button onClick={() => window.print()}>打印</Button>
      </Space>

      <Card title={`运单 ${waybill.waybillNo}`} style={{ marginBottom: 16 }}>
        <Descriptions bordered size="small" column={3}>
          <Descriptions.Item label="运单状态">
            <Tag color={waybill.status === 'Cancelled' ? 'default' : 'green'}>{waybill.status}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="货物/仓储">
            <Tag>{waybill.cargoStatus}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="运输状态">
            <Tag>{waybill.transportStatus}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="支付方式">{waybill.paymentType}</Descriptions.Item>
          <Descriptions.Item label="运费应收">
            {waybill.totalAmount === null ? '未固化' : `${waybill.totalAmount.toFixed(2)} 元`}
          </Descriptions.Item>
          <Descriptions.Item label="COD">
            {waybill.codAmount === null ? '—' : `${waybill.codAmount.toFixed(2)} 元（独立资金）`}
          </Descriptions.Item>
          <Descriptions.Item label="发货人" span={1}>
            {waybill.senderName} / {waybill.senderPhone}
          </Descriptions.Item>
          <Descriptions.Item label="发货地址" span={2}>
            {waybill.senderAddress}
          </Descriptions.Item>
          <Descriptions.Item label="收货人" span={1}>
            {waybill.receiverName} / {waybill.receiverPhone}
          </Descriptions.Item>
          <Descriptions.Item label="收货地址" span={2}>
            {waybill.receiverAddress}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title="货物件" style={{ marginBottom: 16 }}>
        <Table
          rowKey="id"
          size="small"
          dataSource={waybill.packages}
          pagination={false}
          columns={[
            { title: '件号', dataIndex: 'packageNo', width: 70 },
            { title: '货名', dataIndex: 'cargoName' },
            { title: '件数', dataIndex: 'quantity', width: 80 },
            {
              title: '重量(kg)',
              dataIndex: 'weightKg',
              width: 110,
              render: (v: number) => v.toFixed(3),
            },
            {
              title: '体积(m³)',
              dataIndex: 'volumeM3',
              width: 110,
              render: (v: number) => v.toFixed(4),
            },
            { title: '备注', dataIndex: 'remark' },
          ]}
        />
      </Card>

      <Card title="运单轨迹时间线" style={{ marginBottom: 16 }}>
        <Timeline
          items={(eventsQuery.data ?? []).map((event) => ({
            children: (
              <>
                <Typography.Text strong>{eventTypeText[event.eventType] ?? event.eventType}</Typography.Text>
                {event.detail && <Typography.Text type="secondary"> — {event.detail}</Typography.Text>}
                <br />
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {new Date(event.occurredAt).toLocaleString('zh-CN')}
                </Typography.Text>
              </>
            ),
          }))}
        />
        {(eventsQuery.data ?? []).length === 0 && <Typography.Text type="secondary">暂无事件</Typography.Text>}
      </Card>

      {receiptOpen && (
        <Card title="收货登记（称重/体积复核）" style={{ marginBottom: 16 }}>
          <Space wrap>
            <InputNumber
              min={1}
              precision={0}
              value={receiptForm.pieces}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, pieces: v ?? 1 })}
              addonBefore="实收件数"
              style={{ width: 160 }}
            />
            <InputNumber
              min={0}
              value={receiptForm.weight}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, weight: v ?? 0 })}
              addonBefore="实收kg"
              style={{ width: 170 }}
            />
            <InputNumber
              min={0}
              value={receiptForm.volume}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, volume: v ?? 0 })}
              addonBefore="实收m³"
              style={{ width: 180 }}
            />
            <Input
              placeholder="备注"
              value={receiptForm.note}
              onChange={(e) => setReceiptForm({ ...receiptForm, note: e.target.value })}
              style={{ width: 160 }}
            />
            <Button
              type="primary"
              loading={receiptMutation.isPending}
              onClick={() => receiptMutation.mutate()}
            >
              登记收货
            </Button>
            <Button onClick={() => setReceiptOpen(false)}>取消</Button>
          </Space>
        </Card>
      )}
    </div>
  );
}
