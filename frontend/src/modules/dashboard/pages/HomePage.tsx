import { ArrowUpOutlined, CarOutlined, DollarOutlined, FileTextOutlined } from '@ant-design/icons';
import { Card, Col, Row, Statistic, Typography } from 'antd';
import { PageHeader } from '../../../shared/components';

/**
 * 概览页。经营看板数据随后续报表任务接入；当前展示指标卡占位。
 * 数据一律来自服务端（AGENTS.md §9.2）。
 */
export function HomePage() {
  const metrics = [
    { title: '今日运单', value: 0, suffix: '单', icon: <FileTextOutlined />, color: '#1D4FA0' },
    { title: '今日货量', value: 0, suffix: '件', icon: <CarOutlined />, color: '#2E8B57' },
    { title: '应收金额', value: 0, prefix: '¥', icon: <DollarOutlined />, color: '#C77700' },
    { title: '未收金额', value: 0, prefix: '¥', icon: <ArrowUpOutlined />, color: '#C0392B' },
  ];

  return (
    <div>
      <PageHeader title="工作台" description="物流运营概览。经营看板数据将在报表阶段接入。" />
      <Row gutter={[16, 16]}>
        {metrics.map((m) => (
          <Col key={m.title} xs={24} sm={12} lg={6}>
            <Card>
              <Statistic
                title={m.title}
                value={m.value}
                prefix={m.prefix}
                suffix={m.suffix}
                precision={m.prefix ? 2 : undefined}
              />
              <div style={{ marginTop: 12, color: m.color, fontSize: 18 }}>
                {m.icon}
                <Typography.Text type="secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                  待接入
                </Typography.Text>
              </div>
            </Card>
          </Col>
        ))}
      </Row>

      <Card title="快捷入口" style={{ marginTop: 16 }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          可使用左侧菜单进入运单查询、开单、运输批次与财务模块。业务数据均由服务端实时返回。
        </Typography.Paragraph>
      </Card>
    </div>
  );
}
