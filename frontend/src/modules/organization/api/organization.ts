import { http } from '../../../shared/api/http';

export interface Branch {
  id: string;
  code: string;
  name: string;
  status: string;
  remark: string | null;
}

export interface User {
  id: string;
  username: string;
  displayName: string;
  status: string;
  tenantWideScope: boolean;
}

export interface UserDetail extends User {
  roleIds: string[];
  branchScopeIds: string[];
}

export interface Role {
  id: string;
  code: string;
  name: string;
  status: string;
}

export interface RoleDetail extends Role {
  permissionCodes: string[];
}

export interface Permission {
  code: string;
  name: string;
  module: string;
}

export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ===== 网点 =====
export async function listBranches(page = 1, pageSize = 100): Promise<Page<Branch>> {
  const response = await http.get<Page<Branch>>('/organization/branches', {
    params: { page, pageSize },
  });
  return response.data;
}

export async function createBranch(payload: { code: string; name: string }): Promise<Branch> {
  const response = await http.post<Branch>('/organization/branches', payload);
  return response.data;
}

export async function renameBranch(id: string, name: string): Promise<Branch> {
  const response = await http.put<Branch>(`/organization/branches/${id}`, { name });
  return response.data;
}

export async function disableBranch(id: string): Promise<void> {
  await http.post(`/organization/branches/${id}/disable`);
}

export async function enableBranch(id: string): Promise<void> {
  await http.post(`/organization/branches/${id}/enable`);
}

// ===== 员工 =====
export async function listUsers(page = 1, pageSize = 100): Promise<Page<User>> {
  const response = await http.get<Page<User>>('/organization/users', {
    params: { page, pageSize },
  });
  return response.data;
}

export async function getUser(id: string): Promise<UserDetail> {
  const response = await http.get<UserDetail>(`/organization/users/${id}`);
  return response.data;
}

export async function createUser(payload: {
  username: string;
  displayName: string;
  password: string;
}): Promise<User> {
  const response = await http.post<User>('/organization/users', {
    username: payload.username,
    displayName: payload.displayName,
    password: payload.password,
  });
  return response.data;
}

export async function renameUser(id: string, displayName: string): Promise<User> {
  const response = await http.put<User>(`/organization/users/${id}/name`, { displayName });
  return response.data;
}

export async function resetPassword(id: string, newPassword: string): Promise<void> {
  await http.post(`/organization/users/${id}/reset-password`, { password: newPassword });
}

export async function disableUser(id: string): Promise<void> {
  await http.post(`/organization/users/${id}/disable`);
}

export async function enableUser(id: string): Promise<void> {
  await http.post(`/organization/users/${id}/enable`);
}

export async function setUserRoles(id: string, roleIds: string[]): Promise<void> {
  await http.put(`/organization/users/${id}/roles`, { roleIds });
}

export async function setBranchScopes(id: string, branchIds: string[]): Promise<void> {
  await http.put(`/organization/users/${id}/branch-scopes`, { branchIds });
}

// ===== 角色 =====
export async function listRoles(page = 1, pageSize = 100): Promise<Page<Role>> {
  const response = await http.get<Page<Role>>('/organization/roles', {
    params: { page, pageSize },
  });
  return response.data;
}

export async function getRole(id: string): Promise<RoleDetail> {
  const response = await http.get<RoleDetail>(`/organization/roles/${id}`);
  return response.data;
}

export async function createRole(payload: { code: string; name: string }): Promise<Role> {
  const response = await http.post<Role>('/organization/roles', payload);
  return response.data;
}

export async function renameRole(id: string, name: string): Promise<Role> {
  const response = await http.put<Role>(`/organization/roles/${id}`, { name });
  return response.data;
}

export async function disableRole(id: string): Promise<void> {
  await http.post(`/organization/roles/${id}/disable`);
}

export async function enableRole(id: string): Promise<void> {
  await http.post(`/organization/roles/${id}/enable`);
}

export async function setRolePermissions(id: string, permissionCodes: string[]): Promise<void> {
  await http.put(`/organization/roles/${id}/permissions`, { permissionCodes });
}

/** 平台预置权限点目录（权限配置界面数据源）。 */
export async function listPermissions(): Promise<Permission[]> {
  const response = await http.get<Permission[]>('/organization/roles/permissions');
  return response.data;
}

/** 当前用户可访问的网点（工作网点切换器数据源；仅数据范围事实，不授予权限）。 */
export interface AccessibleBranch {
  id: string;
  code: string;
  name: string;
}

export interface MyBranches {
  tenantWide: boolean;
  branches: AccessibleBranch[];
}

export async function getMyBranches(): Promise<MyBranches> {
  const response = await http.get<MyBranches>('/organization/my-branches');
  return response.data;
}
