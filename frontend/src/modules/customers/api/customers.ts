import { http } from '../../../shared/api/http';

export interface Customer {
  id: string;
  code: string;
  name: string;
  status: string;
  remark: string | null;
  createdAt: string;
}

export interface CustomerDetail extends Customer {
  contacts: { id: string; name: string; phone: string; isPrimary: boolean; remark: string | null }[];
  addresses: { id: string; address: string; label: string | null; isDefault: boolean; remark: string | null }[];
  servicedBranchIds: string[];
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function listCustomers(
  page: number,
  pageSize: number,
  keyword?: string,
): Promise<Page<Customer>> {
  const response = await http.get<Page<Customer>>('/customers', {
    params: { page, pageSize, keyword },
  });
  return response.data;
}

export async function getCustomer(id: string): Promise<CustomerDetail> {
  const response = await http.get<CustomerDetail>(`/customers/${id}`);
  return response.data;
}

export async function createCustomer(code: string, name: string, remark?: string): Promise<Customer> {
  const response = await http.post<Customer>('/customers', { code, name, remark });
  return response.data;
}

export async function updateCustomer(id: string, name: string, remark?: string): Promise<Customer> {
  const response = await http.put<Customer>(`/customers/${id}`, { name, remark });
  return response.data;
}

export async function disableCustomer(id: string): Promise<void> {
  await http.post(`/customers/${id}/disable`);
}

export async function enableCustomer(id: string): Promise<void> {
  await http.post(`/customers/${id}/enable`);
}

export async function addContact(
  customerId: string,
  payload: { name: string; phone: string; isPrimary: boolean; remark?: string },
): Promise<CustomerDetail> {
  const response = await http.post<CustomerDetail>(`/customers/${customerId}/contacts`, payload);
  return response.data;
}

export async function removeContact(customerId: string, contactId: string): Promise<CustomerDetail> {
  const response = await http.delete<CustomerDetail>(`/customers/${customerId}/contacts/${contactId}`);
  return response.data;
}

export async function addAddress(
  customerId: string,
  payload: { address: string; label?: string; isDefault: boolean; remark?: string },
): Promise<CustomerDetail> {
  const response = await http.post<CustomerDetail>(`/customers/${customerId}/addresses`, payload);
  return response.data;
}

export async function removeAddress(customerId: string, addressId: string): Promise<CustomerDetail> {
  const response = await http.delete<CustomerDetail>(`/customers/${customerId}/addresses/${addressId}`);
  return response.data;
}

export async function grantBranch(customerId: string, branchId: string): Promise<CustomerDetail> {
  const response = await http.post<CustomerDetail>(`/customers/${customerId}/branches`, { branchId });
  return response.data;
}

export async function revokeBranch(customerId: string, branchId: string): Promise<CustomerDetail> {
  const response = await http.delete<CustomerDetail>(`/customers/${customerId}/branches/${branchId}`);
  return response.data;
}
