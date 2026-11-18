import { Tag } from 'antd';

/**
 * 全站统一状态标签（AGENTS.md §9.3）。
 *
 * 将各页面分散、重复的 status 映射集中到一处：同一状态值在全站显示一致的
 * 中文文案与语义色。未收录的状态值降级为灰色原值展示，绝不抛错或静默吞掉，
 * 便于新增枚举时仍能安全渲染。
 */

type Tone = 'success' | 'processing' | 'error' | 'warning' | 'default';

interface StatusMeta {
  text: string;
  tone: Tone;
}

/**
 * 状态映射表。按领域前缀区分同名状态（如 Received 在运单=已收货、在调拨=已验收）。
 * key 格式：`${category}:${rawStatus}`，无分类的通用状态直接用 rawStatus。
 */
const STATUS_MAP: Record<string, StatusMeta> = {
  // ── 通用启用/停用 ─────────────────────────────────────────
  Active: { text: '启用', tone: 'success' },
  Inactive: { text: '停用', tone: 'default' },

  // ── 运单状态 (waybill) ────────────────────────────────────
  'waybill:Draft': { text: '草稿', tone: 'warning' },
  'waybill:Confirmed': { text: '已确认', tone: 'success' },
  'waybill:Cancelled': { text: '已取消', tone: 'default' },
  Draft: { text: '草稿', tone: 'warning' },
  Confirmed: { text: '已确认', tone: 'success' },
  Cancelled: { text: '已取消', tone: 'default' },

  // ── 货物/仓储状态 (cargo) ─────────────────────────────────
  PendingReceive: { text: '待收货', tone: 'warning' },
  Received: { text: '已收货', tone: 'processing' },
  Sorted: { text: '已分拣', tone: 'success' },

  // ── 运费财务状态 (freight / receivable) ───────────────────
  Unpaid: { text: '未收款', tone: 'error' },
  PartiallyPaid: { text: '部分收款', tone: 'warning' },
  Settled: { text: '已结清', tone: 'success' },

  // ── 运输批次状态 (trip) ───────────────────────────────────
  'trip:Planned': { text: '已计划', tone: 'warning' },
  'trip:Departed': { text: '已发车', tone: 'processing' },
  'trip:Arrived': { text: '已到达', tone: 'success' },
  Planned: { text: '已计划', tone: 'warning' },
  Departed: { text: '已发车', tone: 'processing' },
  Arrived: { text: '已到达', tone: 'success' },

  // ── 网单运输状态 (waybill transport) ──────────────────────
  NotShipped: { text: '未发车', tone: 'default' },
  Shipped: { text: '运输中', tone: 'processing' },
  Dispatched: { text: '已发车', tone: 'processing' },
  WaitingPickup: { text: '待提货', tone: 'warning' },

  // ── 调拨状态 (transfer) ───────────────────────────────────
  'transfer:Draft': { text: '草稿', tone: 'warning' },
  Submitted: { text: '已提交', tone: 'processing' },
  'transfer:Dispatched': { text: '已发出', tone: 'processing' },
  'transfer:Received': { text: '已验收', tone: 'success' },
  'transfer:Cancelled': { text: '已取消', tone: 'default' },

  // ── 分拣任务状态 (sorting) ────────────────────────────────
  Pending: { text: '未完成', tone: 'warning' },
  Completed: { text: '已完成', tone: 'success' },

  // ── 规则版本状态 (pricing version) ────────────────────────
  Published: { text: '已发布', tone: 'success' },
  Superseded: { text: '已取代', tone: 'default' },

  // ── 月结账单状态 (statement) ──────────────────────────────
  Closed: { text: '已关闭', tone: 'default' },

  // ── 交付状态 (delivery) ───────────────────────────────────
  Rejected: { text: '已拒收', tone: 'error' },
  NotDelivered: { text: '未交付', tone: 'warning' },
  Delivered: { text: '已交付', tone: 'success' },
};

export type StatusCategory = 'waybill' | 'trip' | 'transfer';

export interface StatusTagProps {
  status?: string | null;
  /** 领域分类，用于消解同名状态（如 Received）。可省略，省略时取通用映射。 */
  category?: StatusCategory;
}

export function formatStatus(status?: string | null, category?: StatusCategory): string {
  if (!status) return '—';
  const meta = (category && STATUS_MAP[`${category}:${status}`]) || STATUS_MAP[status];
  return meta?.text ?? status;
}

export function StatusTag({ status, category }: StatusTagProps) {
  if (!status) {
    return <Tag>—</Tag>;
  }
  const meta = (category && STATUS_MAP[`${category}:${status}`]) || STATUS_MAP[status];
  if (!meta) {
    return <Tag>{status}</Tag>;
  }
  return <Tag color={meta.tone}>{meta.text}</Tag>;
}

/** 在车/已卸等布尔型标签独立提供，语义固定。 */
export function OnBoardTag({ active }: { active: boolean }) {
  return <Tag color={active ? 'success' : 'default'}>{active ? '在车' : '已卸'}</Tag>;
}
