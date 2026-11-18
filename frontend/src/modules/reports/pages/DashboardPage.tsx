import { useQuery } from '@tanstack/react-query';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  FileTextOutlined,
  DollarOutlined,
  ShopOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { getDashboard } from '../api/reports';

/** 经营看板（P9 前端）：核心运营指标。 */
export function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => getDashboard(),
  });

  if (isLoading || !data) {
    return <Typography.Text type="secondary">加载中…</Typography.Text>;
  }

  return (
    <div>
      <Typography.Title level={4} style={{ marginBottom: 16 }}>
        经营看板
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="运单总量"
              value={data.totalWaybills}
              prefix={<FileTextOutlined />}
              suffix={`（本月 ${data.monthlyWaybills}）`}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="货物总重量"
              value={data.totalCargoWeightKg}
              precision={2}
              suffix="kg"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="货物总体积"
              value={data.totalCargoVolumeM3}
              precision={2}
              suffix="m³"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="运费收入"
              value={data.totalFreightRevenue}
              precision={2}
              prefix={<DollarOutlined />}
              suffix="元"
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="应收总额"
              value={data.totalReceivableAmount}
              precision={2}
              suffix="元"
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="已收款"
              value={data.totalSettledAmount}
              precision={2}
              prefix={<ArrowUpOutlined />}
              suffix="元"
              valueStyle={{ color: '#3f8600' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="未收余额"
              value={data.totalOutstandingAmount}
              precision={2}
              prefix={<ArrowDownOutlined />}
              suffix="元"
              valueStyle={{ color: '#cf1322' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="COD 已付" value={data.totalCodPaidOut} precision={2} suffix="元" />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={12} lg={6}>
          <Card>
            <Statistic title="启用网点" value={data.activeBranchCount} prefix={<ShopOutlined />} />
          </Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card>
            <Statistic
              title="启用客户"
              value={data.activeCustomerCount}
              prefix={<UserOutlined />}
            />
          </Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card>
            <Statistic title="COD 已收" value={data.totalCodCollected} precision={2} suffix="元" />
          </Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card>
            <Statistic title="COD 已付" value={data.totalCodPaidOut} precision={2} suffix="元" />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
