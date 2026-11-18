import { SearchOutlined, TruckOutlined } from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Form, Input, Table, Typography } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { EmptyState, StatusTag, formatStatus } from '../../../shared/components';
import { queryWaybillAsCustomer, type CustomerWaybillView } from '../api/customerQuery';

interface QueryForm {
  tenantCode: string;
  waybillNo: string;
  queryCode: string;
}

/** 客户运单查询门户（P10）：承运方编码 + 运单号 + 查询码，结果突出运输进度。 */
export function CustomerQueryPage() {
  const [searchParams] = useSearchParams();
  const [form] = Form.useForm<QueryForm>();
  const [result, setResult] = useState<CustomerWaybillView | null>(null);
  const [error, setError] = useState<string | null>(null);

  const initialValues: QueryForm = {
    tenantCode: searchParams.get('tenantCode') ?? '',
    waybillNo: searchParams.get('waybillNo') ?? '',
    queryCode: '',
  };

  const queryMutation = useMutation({
    mutationFn: (values: QueryForm) =>
      queryWaybillAsCustomer(
        values.tenantCode.trim(),
        values.waybillNo.trim(),
        values.queryCode.trim(),
      ),
    onError: (mutationError: Error) => {
      setResult(null);
      setError(mutationError.message);
    },
    onSuccess: (data) => {
      setResult(data);
      setError(null);
    },
  });

  return (
    <div className="ltl-track-page">
      <div className="ltl-track-shell">
        <div className="ltl-track-brand">
          <div className="ltl-track-brand__icon">
            <TruckOutlined />
          </div>
          <Typography.Title level={3} style={{ margin: '8px 0 4px' }}>
            运单查询
          </Typography.Title>
          <Typography.Paragraph type="secondary">
            输入承运方提供的信息，了解货物的当前运输进度
          </Typography.Paragraph>
        </div>

        <Card title="查询条件" className="ltl-track-query" style={{ marginBottom: 16 }}>
          <Form<QueryForm>
            form={form}
            layout="vertical"
            initialValues={initialValues}
            requiredMark={false}
            onValuesChange={() => {
              setResult(null);
              setError(null);
            }}
            onFinish={(values) => queryMutation.mutate(values)}
          >
            <Form.Item
              name="tenantCode"
              label="承运方编码"
              extra="由承运方提供，通常印在托运单或面单上"
              normalize={(value: string) => value?.trim()}
              rules={[{ required: true, whitespace: true, message: '请输入承运方编码' }]}
            >
              <Input prefix={<span style={{ color: '#8c96a5' }}>#</span>} placeholder="例如 DEMO" />
            </Form.Item>
            <Form.Item
              name="waybillNo"
              label="运单号"
              normalize={(value: string) => value?.trim()}
              rules={[{ required: true, whitespace: true, message: '请输入运单号' }]}
            >
              <Input
                prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
                placeholder="输入完整运单号"
              />
            </Form.Item>
            <Form.Item
              name="queryCode"
              label="查询码"
              extra="查询码由承运方在开单时提供；如遗忘请联系承运方重置"
              normalize={(value: string) => value?.trim()}
              rules={[{ required: true, whitespace: true, message: '请输入查询码' }]}
            >
              <Input placeholder="输入查询码" maxLength={16} autoComplete="off" />
            </Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              block
              size="large"
              loading={queryMutation.isPending}
              className="ltl-track-query__submit"
            >
              查询运输进度
            </Button>
          </Form>
        </Card>

        {error && (
          <Alert
            style={{ marginBottom: 16 }}
            type="error"
            showIcon
            message="查询未成功"
            description={error}
          />
        )}

        {result && (
          <Card title={`运单 ${result.waybillNo}`} className="ltl-track-result">
            <div className="ltl-track-hero">
              <Typography.Text type="secondary">当前运输进度</Typography.Text>
              <strong>{formatStatus(result.transportStatus)}</strong>
              <div className="ltl-track-meta">
                <span>运单状态：{formatStatus(result.status, 'waybill')}</span>
                <span>交付状态：{formatStatus(result.deliveryStatus)}</span>
                <span>开单时间：{new Date(result.createdAt).toLocaleString('zh-CN')}</span>
              </div>
            </div>

            <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} style={{ marginTop: 16 }}>
              <Descriptions.Item label="货物状态">
                <StatusTag status={result.cargoStatus} />
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

            <Typography.Title level={5} className="ltl-track-packages">
              货物件
            </Typography.Title>
            <Table
              className="ltl-track-packages"
              rowKey="packageNo"
              size="small"
              dataSource={result.packages}
              pagination={false}
              scroll={{ x: 560 }}
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
                  render: (value: number) => value.toFixed(3),
                },
                {
                  title: '体积(m³)',
                  dataIndex: 'volumeM3',
                  width: 110,
                  align: 'right',
                  render: (value: number) => value.toFixed(4),
                },
              ]}
            />
          </Card>
        )}
      </div>
    </div>
  );
}
