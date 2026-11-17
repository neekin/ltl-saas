import { SearchOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Flex, Input, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { completeSortingTask, listSortingTasks, type SortingTask } from '../../waybills/api/waybills';

/** 分拣作业页（P4 前端）：按运单号查询任务并完成分拣。 */
export function SortingTasksPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [waybillId, setWaybillId] = useState('');

  const tasksQuery = useQuery({
    queryKey: ['sorting-tasks', waybillId],
    queryFn: () => listSortingTasks(waybillId),
    enabled: waybillId !== '',
  });

  const completeMutation = useMutation({
    mutationFn: (taskId: string) => completeSortingTask(taskId),
    onSuccess: () => {
      message.success('分拣已完成');
      queryClient.invalidateQueries({ queryKey: ['sorting-tasks', waybillId] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const columns: ColumnsType<SortingTask> = [
    {
      title: '状态',
      dataIndex: 'status',
      width: 120,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      width: 180,
      render: (v: string) => <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text>,
    },
    {
      title: '完成时间',
      dataIndex: 'completedAt',
      width: 180,
      render: (v: string | null) =>
        v ? <Typography.Text type="secondary">{new Date(v).toLocaleString('zh-CN')}</Typography.Text> : '—',
    },
    { title: '备注', dataIndex: 'note', render: (v: string | null) => v ?? '—' },
    {
      title: '操作',
      key: 'actions',
      width: 120,
      render: (_: unknown, record: SortingTask) =>
        record.status === 'Pending' ? (
          <Button type="primary" size="small" loading={completeMutation.isPending} onClick={() => completeMutation.mutate(record.id)}>
            完成分拣
          </Button>
        ) : (
          <Typography.Text type="secondary">已完成</Typography.Text>
        ),
    },
  ];

  return (
    <div>
      <PageHeader title="分拣作业" description="按运单 ID 查询分拣任务，完成货物分拣复核。" />

      <div className="ltl-filter-bar">
        <Flex gap={12} wrap>
          <Input
            prefix={<SearchOutlined style={{ color: '#8c96a5' }} />}
            placeholder="输入运单 ID 查询分拣任务"
            value={waybillId}
            onChange={(e) => setWaybillId(e.target.value)}
            onPressEnter={() => queryClient.invalidateQueries({ queryKey: ['sorting-tasks'] })}
            style={{ width: 400 }}
            allowClear
          />
        </Flex>
      </div>

      <Card styles={{ body: { padding: 0 } }}>
        {waybillId ? (
          <Table<SortingTask>
            rowKey="id"
            loading={tasksQuery.isLoading}
            dataSource={tasksQuery.data}
            columns={columns}
            size="middle"
            scroll={{ x: 750 }}
            locale={{ emptyText: <EmptyState description="该运单暂无分拣任务" /> }}
          />
        ) : (
          <EmptyState description="请输入运单 ID 查询分拣任务" />
        )}
      </Card>
    </div>
  );
}
