import { useQuery } from '@tanstack/react-query';
import { Card, List } from 'antd-mobile';
import { StatusTag } from '../../../shared/components';
import { listMyDeliveryTasks, type DeliveryTask } from '../../finance/api/finance';

/**
 * 我的配送任务页（DELIVERY-002，P11 移动派单）：
 * 配送员查看分配给自己的待办配送任务；完成后在「到货」页签收（沿用既有签收流程）。
 */
export function MobileDispatchPage() {
  const tasksQuery = useQuery({
    queryKey: ['mobile-my-deliveries'],
    queryFn: listMyDeliveryTasks,
    retry: false,
  });

  const tasks = tasksQuery.data ?? [];

  return (
    <div style={{ padding: 12 }}>
      <Card
        title="我的配送任务"
        extra={<span style={{ fontSize: 12, color: '#8c96a5' }}>DELIVERY-002</span>}
      >
        <p style={{ color: '#8c96a5', fontSize: 13 }}>
          派单后任务出现在这里；到货核验页完成签收后任务自动闭环。
        </p>
        {tasks.length === 0 ? (
          <p style={{ color: '#8c96a5' }}>暂无待办配送任务。</p>
        ) : (
          <List>
            {tasks.map((task: DeliveryTask) => (
              <List.Item
                key={task.id}
                extra={<StatusTag status={task.status} />}
                description={`配送类型：${task.deliveryType}`}
              >
                运单 {task.waybillId.slice(0, 8)}…
              </List.Item>
            ))}
          </List>
        )}
      </Card>
    </div>
  );
}
