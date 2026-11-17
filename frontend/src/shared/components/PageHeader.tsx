import { ArrowLeftOutlined } from '@ant-design/icons';
import { Button, Flex, Space, Typography } from 'antd';
import type { ReactNode } from 'react';

/**
 * 统一页面头部（AGENTS.md §9.3）。
 * fixed 结构：标题 + 副标题/描述在左，主操作按钮在右，下方留白与内容区分隔。
 * 不承载业务逻辑，仅统一页面入口的信息层级与操作节奏。
 */
export interface PageHeaderProps {
  title: ReactNode;
  /** 标题下方的辅助说明，保持简短。 */
  description?: ReactNode;
  /** 右侧主操作区，通常放 1 个主按钮 + 若干次按钮。 */
  actions?: ReactNode;
  /** 传入则显示返回按钮，点击时回调。 */
  onBack?: () => void;
}

export function PageHeader({ title, description, actions, onBack }: PageHeaderProps) {
  return (
    <div className="ltl-no-print" style={{ marginBottom: 20 }}>
      <Flex align="flex-start" justify="space-between" gap={16} wrap="wrap">
        <div style={{ minWidth: 0 }}>
          <Space align="center" size={12}>
            {onBack && <Button type="text" icon={<ArrowLeftOutlined />} onClick={onBack} aria-label="返回" />}
            <Typography.Title level={4} style={{ margin: 0 }}>
              {title}
            </Typography.Title>
          </Space>
          {description && (
            <Typography.Paragraph type="secondary" style={{ margin: '6px 0 0' }}>
              {description}
            </Typography.Paragraph>
          )}
        </div>
        {actions && <Space wrap>{actions}</Space>}
      </Flex>
    </div>
  );
}
