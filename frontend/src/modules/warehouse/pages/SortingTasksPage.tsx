import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Input, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
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

  return (
    <div>
      <Typography.Title level={4}>分拣作业</Typography.Title>
      <Space style={{ marginBottom: 16 }}>
        <Input.Search
          placeholder="输入运单 ID 查询分拣任务"
          value={waybillId}
          onChange={(e) => setWaybillId(e.target.value)}
          onSearch={() => queryClient.invalidateQueries({ queryKey: ['sorting-tasks'] })}
          style={{ width: 400 }}
        />
      </Space>

      {waybillId && (
        <Table
          rowKey="id"
          loading={tasksQuery.isLoading}
          dataSource={tasksQuery.data}
          columns={[
            {
              title: '状态',
              dataIndex: 'status',
              render: (status: string) => (
                <Tag color={status === 'Pending' ? 'orange' : 'green'}>{status === 'Pending' ? '未完成' : '已完成'}</Tag>
              ),
            },
            {
              title: '创建时间',
              dataIndex: 'createdAt',
              render: (v: string) => new Date(v).toLocaleString('zh-CN'),
            },
            {
              title: '完成时间',
              dataIndex: 'completedAt',
              render: (v: string | null) => (v ? new Date(v).toLocaleString('zh-CN') : '—'),
            },
            {
              title: '操作',
              key: 'actions',
              render: (_: unknown, record: SortingTask) =>
                record.status === 'Pending' ? (
                  <Button
                    type="primary"
                    size="small"
                    loading={completeMutation.isPending}
                    onClick={() => completeMutation.mutate(record.id)}
                  >
                    完成分拣
                  </Button>
                ) : (
                  <Typography.Text type="secondary">已完成</Typography.Text>
                ),
            },
          ]}
        />
      )}
    </div>
  );
}
