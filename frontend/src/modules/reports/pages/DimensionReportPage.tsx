import { useQuery } from '@tanstack/react-query';
import { Card, DatePicker, Flex, Table, Tabs, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';
import { EmptyState, PageHeader } from '../../../shared/components';
import { getDimensionReport, type DimensionMetric, type ReportDimension } from '../api/reports';

const DIMENSION_LABELS: Record<ReportDimension, { tab: string; code: string; name: string }> = {
  branches: { tab: '网点运营', code: '网点编码', name: '网点名称' },
  routes: { tab: '线路运营', code: '线路编码', name: '线路名称' },
  customers: { tab: '客户月结', code: '客户编码', name: '客户名称' },
};

/** 运营维度报表（REPORT-002 前端）：网点/线路/客户三维度运营聚合。 */
export function DimensionReportPage() {
  const [dimension, setDimension] = useState<ReportDimension>('branches');
  const [month, setMonth] = useState<string | undefined>(undefined);

  const reportQuery = useQuery({
    queryKey: ['dimension-report', dimension, month],
    queryFn: () => getDimensionReport(dimension, month),
  });

  const labels = DIMENSION_LABELS[dimension];

  const columns: ColumnsType<DimensionMetric> = [
    {
      title: labels.code,
      dataIndex: 'code',
      width: 150,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    { title: labels.name, dataIndex: 'name' },
    {
      title: '运单数',
      dataIndex: 'waybillCount',
      width: 110,
      align: 'right',
      sorter: (a, b) => a.waybillCount - b.waybillCount,
      defaultSortOrder: 'descend',
    },
    {
      title: '运费收入',
      dataIndex: 'freightRevenue',
      width: 150,
      align: 'right',
      sorter: (a, b) => a.freightRevenue - b.freightRevenue,
      render: (v: number) => (
        <span className="ltl-amount ltl-amount--strong">¥ {v.toFixed(2)}</span>
      ),
    },
    {
      title: '货量 (kg)',
      dataIndex: 'totalWeightKg',
      width: 130,
      align: 'right',
      render: (v: number) => <span className="ltl-amount">{v.toFixed(2)}</span>,
    },
    {
      title: '体积 (m³)',
      dataIndex: 'totalVolumeM3',
      width: 130,
      align: 'right',
      render: (v: number) => <span className="ltl-amount">{v.toFixed(3)}</span>,
    },
  ];

  return (
    <div>
      <PageHeader
        title="运营报表"
        description="按网点、线路、客户维度查看运单量与运费收入；数据来自服务端实时聚合。"
      />

      <Card>
        <Tabs
          activeKey={dimension}
          onChange={(key) => setDimension(key as ReportDimension)}
          items={Object.entries(DIMENSION_LABELS).map(([key, value]) => ({
            key,
            label: value.tab,
          }))}
        />
        <Flex gap={12} wrap align="center" style={{ marginBottom: 16 }}>
          <Typography.Text type="secondary">统计月份：</Typography.Text>
          <DatePicker
            picker="month"
            value={month ? dayjs(month, 'YYYYMM') : null}
            onChange={(date) => setMonth(date ? date.format('YYYYMM') : undefined)}
            allowClear
            placeholder="全部按当月"
          />
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            留空按当前月统计
          </Typography.Text>
        </Flex>
        <Table<DimensionMetric>
          rowKey="id"
          size="middle"
          loading={reportQuery.isLoading}
          dataSource={reportQuery.data}
          columns={columns}
          scroll={{ x: 850 }}
          locale={{ emptyText: <EmptyState description="该维度暂无数据" /> }}
          pagination={{
            pageSize: 20,
            showTotal: (total) => `共 ${total} 条`,
            showSizeChanger: false,
          }}
        />
      </Card>
    </div>
  );
}
