import { http } from '../../../shared/api/http';

export interface Trip {
  id: string;
  tripNo: string;
  originBranchId: string;
  destBranchId: string;
  status: string;
  vehicleId: string;
  driverId: string | null;
  note: string | null;
  createdAt: string;
  waybills: {
    id: string;
    waybillId: string;
    active: boolean;
    loadedAt: string;
    loadedBy: string | null;
  }[];
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TripFee {
  id: string;
  tripId: string;
  feeType: string;
  amount: number;
  note: string | null;
  operatorUserId: string | null;
  occurredAt: string;
}

export async function listTrips(page = 1, pageSize = 10): Promise<Page<Trip>> {
  const response = await http.get<Page<Trip>>('/transport/trips', { params: { page, pageSize } });
  return response.data;
}

export async function getTrip(id: string): Promise<Trip> {
  const response = await http.get<Trip>(`/transport/trips/${id}`);
  return response.data;
}

export async function createTrip(payload: {
  originBranchId: string;
  destBranchId: string;
  vehicleId: string;
  driverId?: string | null;
  routeId?: string | null;
  note?: string;
}): Promise<Trip> {
  const response = await http.post<Trip>('/transport/trips', payload);
  return response.data;
}

export async function loadWaybill(tripId: string, waybillId: string): Promise<void> {
  await http.post(`/transport/trips/${tripId}/waybills`, { waybillId });
}

export async function removeWaybill(tripId: string, assignmentId: string): Promise<void> {
  await http.delete(`/transport/trips/${tripId}/waybills/${assignmentId}`);
}

export async function departTrip(tripId: string): Promise<void> {
  await http.post(`/transport/trips/${tripId}/depart`);
}

export async function arriveTrip(tripId: string): Promise<void> {
  await http.post(`/transport/trips/${tripId}/arrive`);
}

export async function recordTripFee(
  tripId: string,
  feeType: string,
  amount: number,
  note?: string,
): Promise<void> {
  await http.post(`/transport/trips/${tripId}/fees`, { feeType, amount, note });
}

export async function listTripFees(tripId: string): Promise<TripFee[]> {
  const response = await http.get<TripFee[]>(`/transport/trips/${tripId}/fees`);
  return response.data;
}
