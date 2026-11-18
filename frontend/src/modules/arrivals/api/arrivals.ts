import { http } from '../../../shared/api/http';

export interface ArrivalDto {
  id: string;
  tripId: string;
  branchId: string;
  items: {
    id: string;
    waybillId: string;
    loadedPieces: number;
    receivedPieces: number;
    brokenPieces: number;
    remark: string | null;
  }[];
  operatorUserId: string | null;
  note: string | null;
  arrivedAt: string;
}

export interface ArrivalRecordResult {
  arrival: ArrivalDto;
  differences: {
    waybillId: string;
    loadedPieces: number;
    receivedPieces: number;
    brokenPieces: number;
    pieceDifference: number;
  }[];
}

export async function recordArrival(payload: {
  tripId: string;
  branchId: string;
  items: { waybillId: string; receivedPieces: number; brokenPieces: number; remark?: string }[];
  note?: string;
}): Promise<ArrivalRecordResult> {
  const response = await http.post<ArrivalRecordResult>('/arrivals', payload);
  return response.data;
}

export async function getArrivalByTrip(tripId: string): Promise<ArrivalDto | null> {
  const response = await http.get<ArrivalDto>('/arrivals', { params: { tripId } });
  return response.data;
}
