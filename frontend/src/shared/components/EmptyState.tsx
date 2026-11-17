import { Empty, Typography } from 'antd';
import type { ReactNode } from 'react';

/**
 * 统一空状态（AGENTS.md §9.3）。
 * 替代各页面零散的「暂无数据」文本，给出一致的视觉与可选引导操作。
 */
export interface EmptyStateProps {
  description?: ReactNode;
  /** 表格/列表紧凑场景使用简单文案，卡片级空态用默认插图。 */
  simple?: boolean;
  action?: ReactNode;
}

export function EmptyState({ description = '暂无数据', simple = false, action }: EmptyStateProps) {
  if (simple) {
    return (
      <div style={{ textAlign: 'center', padding: '32px 16px' }}>
        <Typography.Text type="secondary">{description}</Typography.Text>
        {action && <div style={{ marginTop: 12 }}>{action}</div>}
      </div>
    );
  }
  return (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={<Typography.Text type="secondary">{description}</Typography.Text>}
    >
      {action}
    </Empty>
  );
}
