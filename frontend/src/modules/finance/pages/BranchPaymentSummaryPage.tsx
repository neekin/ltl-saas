import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';
import { EmptyState, PageHeader } from '../../../shared/components';
import { downloadCsv } from '../../../shared/utils/csv';
import { getBranchPaymentSummary, type BranchPaymentSummary } from '../api/finance';

/** 网点收款汇总页（FIN-003 前端）：按运单始发网点聚合收款笔数与金额，支持按月筛选。 */
export function BranchPaymentSummaryPage() {
  const [month, setMonth] = useState<string | undefined>(undefined);

  const summaryQuery = useQuery({
    queryKey: ['branch-payment-summary', month ?? ''],
    queryFn: () => getBranchPaymentSummary(month),
  });

  const columns: ColumnsType<BranchPaymentSummary> = [
    {
      title: '网点编码',
      dataIndex: 'branchCode',
      width: 130,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    { title: '网点名称', dataIndex: 'branchName' },
    {
      title: '累计收款',
      dataIndex: 'totalCollected',
      width: 150,
      align: 'right',
      sorter: (a, b) => a.totalCollected - b.totalCollected,
      render: (v: number) => <span className="ltl-amount ltl-amount--strong">¥ {v.toFixed(2)}</span>,
    },
    {
      title: '累计笔数',
      dataIndex: 'paymentCount',
      width: 110,
      align: 'right',
    },
    {
      title: '当月收款',
      dataIndex: 'monthCollected',
      width: 150,
      align: 'right',
      sorter: (a, b) => a.monthCollected - b.monthCollected,
      render: (v: number) => <span className="ltl-amount">¥ {v.toFixed(2)}</span>,
    },
    {
      title: '当月笔数',
      dataIndex: 'monthPaymentCount',
      width: 110,
      align: 'right',
    },
  ];

  return (
    <div>
      <PageHeader
        title="网点收款汇总"
        description="按运单始发网点聚合收款笔数与金额；收款归属运单的始发网点。"
      />

      <Card>
        <div
          className="ltl-filter-bar"
          style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}
        >
          <div>
            <div style={{ marginBottom: 4, color: '#6b7688', fontSize: 12 }}>当月口径</div>
            <DatePicker
              picker="month"
              format="YYYY年MM月"
              value={month ? dayjs(month, 'YYYYMM') : null}
              onChange={(date) => setMonth(date ? date.format('YYYYMM') : undefined)}
              allowClear
            />
          </div>
          <Typography.Text type="secondary">
            {month
              ? `当月口径：${dayjs(month, 'YYYYMM').format('YYYY 年 M 月')}`
              : `当月口径：未选择（默认当前月 ${dayjs().format('YYYY 年 M 月')}）`}
          </Typography.Text>
          <Button
            loading={summaryQuery.isFetching && !summaryQuery.isError}
            onClick={() => void summaryQuery.refetch()}
          >
            重新统计
          </Button>
          <Button
            disabled={(summaryQuery.data ?? []).length === 0}
            onClick={() =>
              downloadCsv(
                `branch-payment-summary${month ? `-${month}` : ''}.csv`,
                ['网点编码', '网点名称', '累计收款', '累计笔数', '当月收款', '当月笔数'],
                (summaryQuery.data ?? []).map((r) => [
                  r.branchCode, r.branchName,
                  r.totalCollected.toFixed(2), r.paymentCount,
                  r.monthCollected.toFixed(2), r.monthPaymentCount,
                ]),
              )
            }
          >
            导出 Excel(CSV)
          </Button>
        </div>

        {summaryQuery.isError ? (
          <Alert
            type="error"
            showIcon
            message="汇总加载失败"
            description={summaryQuery.error?.message ?? '请稍后重试。'}
            action={
              <Button size="small" onClick={() => void summaryQuery.refetch()}>
                重试
              </Button>
            }
            style={{ marginTop: 16 }}
          />
        ) : (
          <Table<BranchPaymentSummary>
            rowKey="branchId"
            size="middle"
            loading={summaryQuery.isFetching}
            dataSource={summaryQuery.data ?? []}
            columns={columns}
            scroll={{ x: 800 }}
            locale={{ emptyText: <EmptyState description="暂无网点收款数据" /> }}
            pagination={false}
            style={{ marginTop: 16 }}
          />
        )}
      </Card>
    </div>
  );
}
