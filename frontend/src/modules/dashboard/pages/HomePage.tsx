import { Card, Col, Row, Statistic, Typography } from 'antd';

/**
 * 概览页占位。经营看板随 P9 报表任务实现；数据一律来自服务端（AGENTS.md §9.2）。
 */
export function HomePage() {
  return (
    <Card title="概览">
      <Typography.Paragraph type="secondary">
        经营看板将在后续阶段（P9）实现。当前可通过左侧菜单查看系统状态，验证前后端联通。
      </Typography.Paragraph>
      <Row gutter={16}>
        <Col span={6}>
          <Card>
            <Statistic title="今日运单" value={0} />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic title="今日货量" value={0} suffix="件" />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic title="应收金额" value={0} prefix="¥" precision={2} />
          </Card>
        </Col>
        <Col span={6}>
          <Card>
            <Statistic title="未收金额" value={0} prefix="¥" precision={2} />
          </Card>
        </Col>
      </Row>
    </Card>
  );
}
