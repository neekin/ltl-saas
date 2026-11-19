import { http } from '../../../shared/api/http';

export interface NotificationRecord {
  id: string;
  channel: string;
  eventType: string;
  waybillId: string | null;
  recipient: string;
  content: string;
  status: string;
  failureReason: string | null;
  sentAt: string | null;
  createdAt: string;
}

export interface NotificationPage {
  items: NotificationRecord[];
  total: number;
}

/** 通知记录列表（NOTIFY-001）。 */
export async function listNotifications(
  page = 1,
  pageSize = 20,
  eventType?: string,
): Promise<NotificationPage> {
  const response = await http.get<NotificationPage>('/notifications', {
    params: { page, pageSize, eventType },
  });
  return response.data;
}
