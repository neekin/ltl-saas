import { SearchOutlined, TruckOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { Button, Card, Col, Descriptions, Input, Row, Table, Typography } from 'antd';
import { useState } from 'react';
import { EmptyState, StatusTag } from '../../../shared/components';
import { queryWaybillAsCustomer, type CustomerWaybillView } from '../api/customerQuery';

/** 客户运单查询门户（P10）：匿名凭租户编码 + 运单号 + 查询码查单票。 */
export function CustomerQueryPage() {
  const [tenantCode, setTenantCode] = useState('demo');
  const [waybillNo, setWaybillNo] = useState('');
  const [queryCode, setQueryCode] = useState('');

  const queryMutation = useMutation({
    mutationFn: () => queryWaybillAsCustomer(tenantCode.trim(), waybillNo.trim(), queryCode.trim()),
    onError: (e: Error) => {
      setResult(null);
      setError(e.message);
    },
    onSuccess: (data) => {
      setResult(data);
      setError(null);
    },
  });
  const [result, setResult] = useState<CustomerWaybillView | null>(null);
  const [error, setError] = useState<string | null>(null);

  return (
    <div style={{ minHeight: '100vh', background: '#f5f7fa', padding: '32px 16px' }}>
      <div style={{ maxWidth: 900, margin: '0 auto' }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <div style={{ fontSize: 28, color: '#1D4FA0' }}>
            <TruckOutlined />
          </div>
          <Typography.Title level={3} style={{ margin: '8px 0 4px' }}>
            运单查询
          </Typography.Title>
          <Typography.Paragraph type="secondary">
            输入运单号与查询码，了解您的货物运输进度
          </Typography.Paragraph>
        </div>

        <Card title="查询条件" style={{ marginBottom: 16 }}>
          <Row gutter={[12, 12]}>
            <Col xs={24} sm={8}>
              <Input
                prefix={<span style={{ color: '#8c96a5' }}>#</span>}
                placeholder="租户编码"
                value={tenantCode}
                onChange={(e) => setTenantCode(e.target.value)}
              />
            </Col>
            <Col xs={24} sm={8}>
              <Input
                prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
                placeholder="运单号"
                value={waybillNo}
                onChange={(e) => setWaybillNo(e.target.value)}
                onPressEnter={() => queryMutation.mutate()}
              />
            </Col>
            <Col xs={24} sm={5}>
              <Input
                placeholder="查询码"
                value={queryCode}
                onChange={(e) => setQueryCode(e.target.value)}
                onPressEnter={() => queryMutation.mutate()}
                maxLength={16}
              />
            </Col>
            <Col xs={24} sm={3}>
              <Button
                type="primary"
                block
                loading={queryMutation.isPending}
                onClick={() => queryMutation.mutate()}
              >
                查询
              </Button>
            </Col>
          </Row>
          <Typography.Paragraph type="secondary" style={{ margin: '10px 0 0', fontSize: 12 }}>
            查询码由承运方在开单时提供；如遗忘请联系承运方重置。
          </Typography.Paragraph>
        </Card>

        {error && (
          <Card style={{ marginBottom: 16 }}>
            <Typography.Text type="danger">{error}</Typography.Text>
          </Card>
        )}

        {result && (
          <Card title={`运单 ${result.waybillNo}`}>
            <Descriptions
              bordered
              size="small"
              column={{ xs: 1, sm: 2 }}
              style={{ marginBottom: 16 }}
            >
              <Descriptions.Item label="运单状态">
                <StatusTag status={result.status} category="waybill" />
              </Descriptions.Item>
              <Descriptions.Item label="货物状态">
                <StatusTag status={result.cargoStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="运输状态">
                <StatusTag status={result.transportStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="交付状态">
                <StatusTag status={result.deliveryStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="运费财务">
                <StatusTag status={result.freightStatus} />
              </Descriptions.Item>
              <Descriptions.Item label="运费应收">
                {result.totalAmount === null ? (
                  '未固化'
                ) : (
                  <span className="ltl-amount">¥ {result.totalAmount.toFixed(2)}</span>
                )}
              </Descriptions.Item>
              <Descriptions.Item label="发货人">
                {result.senderName} / {result.senderPhoneMasked}
              </Descriptions.Item>
              <Descriptions.Item label="收货人">
                {result.receiverName} / {result.receiverPhoneMasked}
              </Descriptions.Item>
            </Descriptions>
            <Typography.Title level={5}>货物件</Typography.Title>
            <Table
              rowKey="packageNo"
              size="small"
              dataSource={result.packages}
              pagination={false}
              locale={{ emptyText: <EmptyState simple description="暂无货物件" /> }}
              columns={[
                { title: '件号', dataIndex: 'packageNo', width: 70 },
                { title: '货名', dataIndex: 'cargoName' },
                { title: '件数', dataIndex: 'quantity', width: 80 },
                {
                  title: '重量(kg)',
                  dataIndex: 'weightKg',
                  width: 110,
                  align: 'right',
                  render: (v: number) => v.toFixed(3),
                },
                {
                  title: '体积(m³)',
                  dataIndex: 'volumeM3',
                  width: 110,
                  align: 'right',
                  render: (v: number) => v.toFixed(4),
                },
              ]}
            />
          </Card>
        )}
      </div>
    </div>
  );
}
