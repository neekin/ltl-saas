import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Row,
  Space,
  Spin,
  Table,
  Timeline,
  Typography,
} from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag, formatStatus } from '../../../shared/components';
import {
  cancelWaybill,
  confirmWaybill,
  createSortingTask,
  getWaybill,
  listWaybillEvents,
  recordReceipt,
  resetQueryCode,
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

const paymentTypeText: Record<string, string> = {
  Cash: '现付',
  OnDelivery: '到付',
  Monthly: '月结',
};

/** 运单详情/货物跟踪页（P4 前端）：状态、货物件、收货登记、分拣、轨迹时间线、打印。 */
export function WaybillDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { message, modal } = App.useApp();
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

  const queryCodeMutation = useMutation({
    mutationFn: () => resetQueryCode(id!),
    onSuccess: (code) => {
      modal.info({
        title: '查询码已重新生成',
        content: (
          <div>
            <Typography.Paragraph>请将新查询码转告客户，旧码立即失效：</Typography.Paragraph>
            <Typography.Text
              code
              copyable
              style={{ fontSize: 18, fontWeight: 600, letterSpacing: 2 }}
            >
              {code}
            </Typography.Text>
          </div>
        ),
      });
    },
    onError: (e: Error) => message.error(e.message),
  });

  if (detailQuery.isLoading) {
    return <Spin tip="加载运单详情…" />;
  }

  if (detailQuery.isError || !detailQuery.data) {
    return (
      <Alert
        type="error"
        showIcon
        message="运单详情加载失败"
        description={detailQuery.error?.message ?? '请稍后重试'}
        action={
          <Button size="small" onClick={() => void detailQuery.refetch()}>
            重新加载
          </Button>
        }
      />
    );
  }

  const waybill = detailQuery.data;
  const canConfirm = waybill.status === 'Draft';
  const canCancel =
    waybill.status === 'Draft' ||
    (waybill.status === 'Confirmed' && waybill.transportStatus === 'NotShipped');
  const canReceive = waybill.status === 'Confirmed' && waybill.cargoStatus === 'PendingReceive';
  const canSort = waybill.status === 'Confirmed' && waybill.cargoStatus === 'Received';

  return (
    <div>
      <PageHeader
        title={
          <Space size={12} wrap>
            <span>运单详情</span>
            <span className="ltl-code" style={{ fontSize: 16, color: '#5b6573', fontWeight: 400 }}>
              {waybill.waybillNo}
            </span>
            <StatusTag status={waybill.status} category="waybill" />
          </Space>
        }
        description="查看运单状态、货物明细与全程轨迹，并执行收货、分拣等操作。"
        onBack={() => navigate('/waybills')}
        actions={
          <Space wrap>
            {canConfirm && (
              <Button
                type="primary"
                onClick={() => confirmMutation.mutate()}
                loading={confirmMutation.isPending}
              >
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
              <Popconfirm
                title="确认取消该运单？"
                onConfirm={() => cancelMutation.mutate('页面取消')}
              >
                <Button danger>取消运单</Button>
              </Popconfirm>
            )}
            <Button onClick={() => navigate(`/waybills/${id}/label`)}>打印面单</Button>
            <Popconfirm
              title="重新生成客户查询码？"
              description="旧查询码会立即失效，需要把新码重新告知客户。"
              okText="确认重新生成"
              cancelText="取消"
              onConfirm={() => queryCodeMutation.mutate()}
            >
              <Button loading={queryCodeMutation.isPending}>重新生成查询码</Button>
            </Popconfirm>
          </Space>
        }
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title="基本信息" style={{ height: '100%' }}>
            <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="运单状态">
                <StatusTag status={waybill.status} category="waybill" />
              </Descriptions.Item>
              <Descriptions.Item label="货物/仓储">
                <StatusTag status={waybill.cargoStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="运输状态">
                <StatusTag status={waybill.transportStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="交付状态">
                <StatusTag status={waybill.deliveryStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="支付方式">
                {paymentTypeText[waybill.paymentType] ?? waybill.paymentType}
              </Descriptions.Item>
              <Descriptions.Item label="运费应收">
                {waybill.totalAmount === null ? (
                  <Typography.Text type="secondary">未固化</Typography.Text>
                ) : (
                  <span className="ltl-amount ltl-amount--strong">
                    ¥ {waybill.totalAmount.toFixed(2)}
                  </span>
                )}
              </Descriptions.Item>
              <Descriptions.Item label="COD 代收" span={2}>
                {waybill.codAmount === null ? (
                  <Typography.Text type="secondary">—</Typography.Text>
                ) : (
                  <span className="ltl-amount">
                    ¥ {waybill.codAmount.toFixed(2)}（独立资金，不计入运费）
                  </span>
                )}
              </Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
        <Col xs={24} xl={10}>
          <Card title="收发货信息" style={{ height: '100%' }}>
            <Descriptions size="small" column={1}>
              <Descriptions.Item label="发货人">
                {waybill.senderName} / {waybill.senderPhone}
              </Descriptions.Item>
              <Descriptions.Item label="发货地址">{waybill.senderAddress}</Descriptions.Item>
              <Descriptions.Item label="收货人">
                {waybill.receiverName} / {waybill.receiverPhone}
              </Descriptions.Item>
              <Descriptions.Item label="收货地址">{waybill.receiverAddress}</Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
      </Row>

      <Card title="货物件" style={{ marginTop: 16 }}>
        <Table
          rowKey="id"
          size="small"
          dataSource={waybill.packages}
          pagination={false}
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState simple description="暂无货物件" /> }}
          columns={[
            { title: '件号', dataIndex: 'packageNo', width: 80 },
            { title: '货名', dataIndex: 'cargoName' },
            { title: '件数', dataIndex: 'quantity', width: 90 },
            {
              title: '重量(kg)',
              dataIndex: 'weightKg',
              width: 120,
              align: 'right',
              render: (v: number) => <span className="ltl-amount">{v.toFixed(3)}</span>,
            },
            {
              title: '体积(m³)',
              dataIndex: 'volumeM3',
              width: 120,
              align: 'right',
              render: (v: number) => <span className="ltl-amount">{v.toFixed(4)}</span>,
            },
            { title: '备注', dataIndex: 'remark', render: (v: string | null) => v ?? '—' },
          ]}
        />
      </Card>

      <Card title="运单轨迹" style={{ marginTop: 16 }}>
        {eventsQuery.isLoading ? (
          <Spin tip="加载轨迹…" />
        ) : eventsQuery.isError ? (
          <Alert
            type="error"
            showIcon
            message="轨迹加载失败"
            description={eventsQuery.error?.message ?? '请稍后重试'}
            action={
              <Button size="small" onClick={() => void eventsQuery.refetch()}>
                重新加载
              </Button>
            }
          />
        ) : (eventsQuery.data ?? []).length === 0 ? (
          <EmptyState simple description="暂无轨迹事件" />
        ) : (
          <Timeline
            items={(eventsQuery.data ?? []).map((event) => ({
              color: event.eventType === 'Cancelled' ? 'red' : 'blue',
              children: (
                <>
                  <Typography.Text strong>
                    {eventTypeText[event.eventType] ?? formatStatus(event.eventType)}
                  </Typography.Text>
                  {event.detail && (
                    <Typography.Text type="secondary"> — {event.detail}</Typography.Text>
                  )}
                  <br />
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {new Date(event.occurredAt).toLocaleString('zh-CN')}
                  </Typography.Text>
                </>
              ),
            }))}
          />
        )}
      </Card>

      <Modal
        title="收货登记（称重/体积复核）"
        open={receiptOpen}
        onCancel={() => setReceiptOpen(false)}
        onOk={() => receiptMutation.mutate()}
        okText="登记收货"
        confirmLoading={receiptMutation.isPending}
        destroyOnClose
      >
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Space wrap>
            <InputNumber
              min={1}
              precision={0}
              value={receiptForm.pieces}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, pieces: v ?? 1 })}
              addonBefore="实收件数"
              style={{ width: 180 }}
            />
            <InputNumber
              min={0}
              value={receiptForm.weight}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, weight: v ?? 0 })}
              addonBefore="实收 kg"
              style={{ width: 180 }}
            />
            <InputNumber
              min={0}
              value={receiptForm.volume}
              onChange={(v: number | null) => setReceiptForm({ ...receiptForm, volume: v ?? 0 })}
              addonBefore="实收 m³"
              style={{ width: 180 }}
            />
          </Space>
          <Input
            placeholder="备注（可选）"
            value={receiptForm.note}
            onChange={(e) => setReceiptForm({ ...receiptForm, note: e.target.value })}
          />
        </Space>
      </Modal>
    </div>
  );
}
