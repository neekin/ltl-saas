import { Tag } from 'antd';
import { getStatusMeta, type StatusCategory } from '../utils/status';

/**
 * 全站统一状态标签（AGENTS.md §9.3）。
 *
 * 将各页面分散、重复的 status 映射集中到共享工具层：同一状态值在全站显示一致的
 * 中文文案与语义色。未收录的状态值降级为灰色原值展示，绝不抛错或静默吞掉。
 */

export interface StatusTagProps {
  status?: string | null;
  /** 领域分类，用于消解同名状态（如 Received）。可省略，省略时取通用映射。 */
  category?: StatusCategory;
}

export function StatusTag({ status, category }: StatusTagProps) {
  if (!status) {
    return <Tag>—</Tag>;
  }
  const meta = getStatusMeta(status, category);
  if (!meta) {
    return <Tag>{status}</Tag>;
  }
  return <Tag color={meta.tone}>{meta.text}</Tag>;
}

/** 在车/已卸等布尔型标签独立提供，语义固定。 */
export function OnBoardTag({ active }: { active: boolean }) {
  return <Tag color={active ? 'success' : 'default'}>{active ? '在车' : '已卸'}</Tag>;
}
