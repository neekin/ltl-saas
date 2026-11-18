import { SearchOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import {
  App,
  Button,
  Card,
  Col,
  Flex,
  Input,
  InputNumber,
  Popconfirm,
  Row,
  Space,
  Typography,
} from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import {
  approveCodSettlement,
  createCodSettlement,
  getCodOrder,
  payCodSettlement,
  type CodOrder,
} from '../api/finance';

/** COD 管理页（P8 前端）：COD 收款 + 结算审批 + 付款。 */
export function CodManagementPage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [codOrder, setCodOrder] = useState<CodOrder | null>(null);
  const [settleAmount, setSettleAmount] = useState(0);

  const search = async () => {
    try {
      setCodOrder(await getCodOrder(waybillId));
    } catch {
      setCodOrder(null);
    }
  };

  const settleMutation = useMutation({
    mutationFn: () => createCodSettlement(waybillId, settleAmount),
    onSuccess: () => {
      message.success('结算单已创建');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const approveMutation = useMutation({
    mutationFn: (id: string) => approveCodSettlement(id),
    onSuccess: () => {
      message.success('已审批');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const payMutation = useMutation({
    mutationFn: (id: string) => payCodSettlement(id),
    onSuccess: () => {
      message.success('已付款给发货人');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <PageHeader title="COD 管理" description="创建 COD 结算单，执行审批与付款登记。" />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="输入运单 ID 查询 COD 订单"
            value={waybillId}
            onChange={(e) => setWaybillId(e.target.value)}
            onPressEnter={search}
            style={{ width: 360 }}
          />
          <Button type="primary" onClick={search}>
            查询
          </Button>
        </Flex>
      </div>

      {!codOrder && (
        <Card>
          <EmptyState description="输入运单 ID 查询 COD 订单" />
        </Card>
      )}

      {codOrder && (
        <>
          <Card title="COD 订单" style={{ marginBottom: 16 }}>
            <Row gutter={[16, 16]}>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>代收金额</div>
                <div className="ltl-amount ltl-amount--strong" style={{ fontSize: 22 }}>
                  ¥ {codOrder.amount.toFixed(2)}
                </div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>已收</div>
                <div className="ltl-amount" style={{ fontSize: 22 }}>
                  ¥ {codOrder.collectedAmount.toFixed(2)}
                </div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>已结算</div>
                <div className="ltl-amount" style={{ fontSize: 22 }}>
                  ¥ {codOrder.settledAmount.toFixed(2)}
                </div>
              </Col>
              <Col xs={12} md={6}>
                <div style={{ color: '#8c96a5', fontSize: 12 }}>已付</div>
                <div className="ltl-amount" style={{ fontSize: 22 }}>
                  ¥ {codOrder.paidAmount.toFixed(2)}
                </div>
              </Col>
              <Col xs={24}>
                <StatusTag status={codOrder.status} />
              </Col>
            </Row>
          </Card>

          {codOrder.collectedAmount > 0 && codOrder.settledAmount < codOrder.collectedAmount && (
            <Card title="创建结算单" style={{ marginBottom: 16 }}>
              <Space wrap>
                <Typography.Text type="secondary">
                  可结算上限 ¥ {(codOrder.collectedAmount - codOrder.settledAmount).toFixed(2)}
                </Typography.Text>
                <InputNumber
                  min={0.01}
                  max={codOrder.collectedAmount - codOrder.settledAmount}
                  precision={2}
                  value={settleAmount}
                  onChange={(v) => setSettleAmount(v ?? 0)}
                  addonBefore="结算金额"
                  style={{ width: 190 }}
                />
                <Button
                  type="primary"
                  onClick={() => settleMutation.mutate()}
                  loading={settleMutation.isPending}
                >
                  创建结算
                </Button>
              </Space>
            </Card>
          )}

          {codOrder.settledAmount > 0 && codOrder.paidAmount < codOrder.settledAmount && (
            <Card title="审批和付款">
              <Typography.Paragraph type="secondary">
                结算单已创建并计入已结算金额；审批和付款操作由 COD 管理员执行。
              </Typography.Paragraph>
              <Space wrap>
                <Popconfirm
                  title="确认审批该结算单？"
                  onConfirm={() => approveMutation.mutate(codOrder.id)}
                >
                  <Button type="primary">审批</Button>
                </Popconfirm>
                <Popconfirm
                  title="确认付款给发货人？"
                  onConfirm={() => payMutation.mutate(codOrder.id)}
                >
                  <Button danger loading={payMutation.isPending}>
                    付款 ¥ {codOrder.settledAmount.toFixed(2)}
                  </Button>
                </Popconfirm>
              </Space>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
