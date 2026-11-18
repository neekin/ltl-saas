import { http } from '../../../shared/api/http';

export interface RouteItem {
  id: string;
  code: string;
  name: string;
  originBranchId: string;
  destBranchId: string;
  status: string;
  createdAt: string;
}

export interface RouteStopItem {
  id: string;
  branchId: string;
  sequence: number;
  remark: string | null;
}

export interface RouteDetail extends RouteItem {
  stops: RouteStopItem[];
}

export interface TransportPlan {
  id: string;
  routeId: string;
  name: string;
  status: string;
  segments: { id: string; sequence: number; fromBranchId: string; toBranchId: string }[];
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function listRoutes(
  page: number,
  pageSize: number,
  keyword?: string,
): Promise<Page<RouteItem>> {
  const response = await http.get<Page<RouteItem>>('/routes', {
    params: { page, pageSize, keyword },
  });
  return response.data;
}

export async function getRoute(id: string): Promise<RouteDetail> {
  const response = await http.get<RouteDetail>(`/routes/${id}`);
  return response.data;
}

export async function createRoute(payload: {
  code: string;
  name: string;
  originBranchId: string;
  destBranchId: string;
}): Promise<RouteDetail> {
  const response = await http.post<RouteDetail>('/routes', payload);
  return response.data;
}

export async function renameRoute(id: string, name: string): Promise<RouteDetail> {
  const response = await http.put<RouteDetail>(`/routes/${id}`, { name });
  return response.data;
}

export async function disableRoute(id: string): Promise<void> {
  await http.post(`/routes/${id}/disable`);
}

export async function enableRoute(id: string): Promise<void> {
  await http.post(`/routes/${id}/enable`);
}

export async function replaceStops(
  id: string,
  stops: { branchId: string; sequence: number; remark?: string }[],
): Promise<RouteDetail> {
  const response = await http.put<RouteDetail>(`/routes/${id}/stops`, { stops });
  return response.data;
}

export async function listPlans(routeId: string): Promise<TransportPlan[]> {
  const response = await http.get<TransportPlan[]>(`/routes/${routeId}/plans`);
  return response.data;
}

export async function createPlan(
  routeId: string,
  payload: {
    name: string;
    segments: { fromBranchId: string; toBranchId: string; sequence: number }[];
  },
): Promise<TransportPlan> {
  const response = await http.post<TransportPlan>(`/routes/${routeId}/plans`, payload);
  return response.data;
}
