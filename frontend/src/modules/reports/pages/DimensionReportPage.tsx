import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, Table, Tabs, Typography } from 'antd';
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

/** 运营维度报表（REPORT-002 前端）：按当前选定月份查看三个经营维度。 */
export function DimensionReportPage() {
  const [dimension, setDimension] = useState<ReportDimension>('branches');
  const [month, setMonth] = useState(dayjs().format('YYYYMM'));

  const reportQuery = useQuery({
    queryKey: ['dimension-report', dimension, month],
    queryFn: () => getDimensionReport(dimension, month),
  });

  const labels = DIMENSION_LABELS[dimension];
  const monthText = dayjs(month, 'YYYYMM').format('YYYY 年 M 月');

  const columns: ColumnsType<DimensionMetric> = [
    {
      title: labels.code,
      dataIndex: 'code',
      width: 150,
      render: (value: string) => <span className="ltl-code">{value}</span>,
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
      render: (value: number) => (
        <span className="ltl-amount ltl-amount--strong">¥ {value.toFixed(2)}</span>
      ),
    },
    {
      title: '货量 (kg)',
      dataIndex: 'totalWeightKg',
      width: 130,
      align: 'right',
      render: (value: number) => <span className="ltl-amount">{value.toFixed(2)}</span>,
    },
    {
      title: '体积 (m³)',
      dataIndex: 'totalVolumeM3',
      width: 130,
      align: 'right',
      render: (value: number) => <span className="ltl-amount">{value.toFixed(3)}</span>,
    },
  ];

  return (
    <div>
      <PageHeader
        title="运营报表"
        description="按网点、线路、客户维度查看指定月份的运单量与运费收入；数据由服务端实时聚合。"
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
        <div
          className="ltl-filter-bar"
          style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}
        >
          <div>
            <div style={{ marginBottom: 4, color: '#6b7688', fontSize: 12 }}>统计月份</div>
            <DatePicker
              picker="month"
              format="YYYY年MM月"
              value={dayjs(month, 'YYYYMM')}
              onChange={(date) => date && setMonth(date.format('YYYYMM'))}
              allowClear={false}
            />
          </div>
          <Typography.Text type="secondary">当前展示：{monthText}</Typography.Text>
          <Button
            loading={reportQuery.isFetching && !reportQuery.isError}
            onClick={() => void reportQuery.refetch()}
          >
            重新统计
          </Button>
        </div>

        {reportQuery.isError ? (
          <Alert
            type="error"
            showIcon
            message="报表加载失败"
            description={reportQuery.error?.message ?? '请稍后重试，未用空数据代替失败结果。'}
            action={
              <Button size="small" onClick={() => void reportQuery.refetch()}>
                重试
              </Button>
            }
          />
        ) : (
          <Table<DimensionMetric>
            rowKey="id"
            size="middle"
            loading={reportQuery.isFetching}
            dataSource={reportQuery.data ?? []}
            columns={columns}
            scroll={{ x: 850 }}
            locale={{ emptyText: <EmptyState description={`${monthText}暂无数据`} /> }}
            pagination={{
              pageSize: 20,
              showTotal: (total) => `共 ${total} 条`,
              showSizeChanger: false,
            }}
          />
        )}
      </Card>
    </div>
  );
}
