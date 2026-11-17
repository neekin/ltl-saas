import { http } from '../../../shared/api/http';

export interface Vehicle {
  id: string;
  plateNo: string;
  vehicleType: string;
  maxLoadKg: number;
  maxVolumeM3: number;
  status: string;
  note: string | null;
}

export interface Driver {
  id: string;
  name: string;
  phone: string;
  licenseNo: string;
  status: string;
  note: string | null;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function listVehicles(page = 1, pageSize = 50): Promise<Page<Vehicle>> {
  const response = await http.get<Page<Vehicle>>('/transport/vehicles', { params: { page, pageSize } });
  return response.data;
}

export async function createVehicle(payload: {
  plateNo: string;
  vehicleType: string;
  maxLoadKg: number;
  maxVolumeM3: number;
  note?: string;
}): Promise<Vehicle> {
  const response = await http.post<Vehicle>('/transport/vehicles', payload);
  return response.data;
}

export async function disableVehicle(id: string): Promise<void> {
  await http.post(`/transport/vehicles/${id}/disable`);
}

export async function enableVehicle(id: string): Promise<void> {
  await http.post(`/transport/vehicles/${id}/enable`);
}

export async function listDrivers(page = 1, pageSize = 50): Promise<Page<Driver>> {
  const response = await http.get<Page<Driver>>('/transport/drivers', { params: { page, pageSize } });
  return response.data;
}

export async function createDriver(payload: { name: string; phone: string; licenseNo: string; note?: string }): Promise<Driver> {
  const response = await http.post<Driver>('/transport/drivers', payload);
  return response.data;
}

export async function disableDriver(id: string): Promise<void> {
  await http.post(`/transport/drivers/${id}/disable`);
}

export async function enableDriver(id: string): Promise<void> {
  await http.post(`/transport/drivers/${id}/enable`);
}
