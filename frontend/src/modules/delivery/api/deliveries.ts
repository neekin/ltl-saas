import { http } from '../../../shared/api/http';

export interface DeliveryOrder {
  id: string;
  waybillId: string;
  branchId: string;
  deliveryType: string;
  status: string;
  driverId: string | null;
  receiverName: string | null;
  completedAt: string | null;
  createdAt: string;
}

export async function createDelivery(payload: {
  waybillId: string;
  branchId: string;
  deliveryType: string;
  driverId?: string;
}): Promise<DeliveryOrder> {
  const response = await http.post<DeliveryOrder>('/deliveries', payload);
  return response.data;
}

export async function getDeliveryByWaybill(waybillId: string): Promise<DeliveryOrder | null> {
  const response = await http.get<DeliveryOrder>('/deliveries', { params: { waybillId } });
  return response.data;
}

export async function completeDelivery(
  id: string,
  receiverName: string,
  photoFileId?: string,
): Promise<DeliveryOrder> {
  const response = await http.post<DeliveryOrder>(`/deliveries/${id}/complete`, {
    receiverName,
    photoFileId,
  });
  return response.data;
}

export async function rejectDelivery(id: string): Promise<DeliveryOrder> {
  const response = await http.post<DeliveryOrder>(`/deliveries/${id}/reject`);
  return response.data;
}
