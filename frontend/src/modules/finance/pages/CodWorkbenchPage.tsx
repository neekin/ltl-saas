import { SearchOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { App, Button, Card, Col, Flex, Input, InputNumber, Row, Space, Typography } from 'antd';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { ensureCodOrder, getCodOrder, recordCodCollection, type CodOrder } from '../api/finance';

/** COD 工作台（P8 前端）：按运单查询 COD 订单并登记收款。 */
export function CodWorkbenchPage() {
  const { message } = App.useApp();
  const [waybillId, setWaybillId] = useState('');
  const [codOrder, setCodOrder] = useState<CodOrder | null>(null);
  const [collectAmount, setCollectAmount] = useState(0);

  const search = async () => {
    try {
      setCodOrder(await getCodOrder(waybillId));
    } catch {
      setCodOrder(null);
      message.info('未找到 COD 订单');
    }
  };

  const collectMutation = useMutation({
    mutationFn: () => recordCodCollection(waybillId, collectAmount),
    onSuccess: () => {
      message.success('COD 收款成功');
      search();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <PageHeader title="COD 工作台" description="按运单查询代收货款订单，登记 COD 收款。" />

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
          <EmptyState description="输入运单 ID 查询 COD 订单；如运单已设置代收金额，可先初始化 COD 订单。" />
          {waybillId.trim() && (
            <div style={{ marginTop: 12 }}>
              <Button
                onClick={async () => {
                  try {
                    setCodOrder(await ensureCodOrder(waybillId));
                    message.success('COD 订单已创建');
                  } catch (e) {
                    message.error(e instanceof Error ? e.message : '创建 COD 订单失败');
                  }
                }}
              >
                初始化 COD 订单
              </Button>
            </div>
          )}
        </Card>
      )}

      {codOrder && (
        <Card title="COD 订单">
          <Row gutter={[16, 16]} style={{ marginBottom: 8 }}>
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
              <div style={{ color: '#8c96a5', fontSize: 12 }}>未收</div>
              <div className="ltl-amount" style={{ fontSize: 22, color: '#C0392B' }}>
                ¥ {(codOrder.amount - codOrder.collectedAmount).toFixed(2)}
              </div>
            </Col>
            <Col xs={12} md={6}>
              <div style={{ color: '#8c96a5', fontSize: 12, marginBottom: 4 }}>状态</div>
              <StatusTag status={codOrder.status} />
            </Col>
          </Row>
          <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
            COD 代收款为独立资金业务，不计入物流运费收入。
          </Typography.Paragraph>
          {codOrder.collectedAmount < codOrder.amount && (
            <Space style={{ marginTop: 4 }}>
              <InputNumber
                min={0.01}
                precision={2}
                value={collectAmount}
                onChange={(v) => setCollectAmount(v ?? 0)}
                addonBefore="收款（元）"
                style={{ width: 190 }}
              />
              <Button
                type="primary"
                onClick={() => collectMutation.mutate()}
                loading={collectMutation.isPending}
              >
                登记收款
              </Button>
            </Space>
          )}
        </Card>
      )}
    </div>
  );
}
