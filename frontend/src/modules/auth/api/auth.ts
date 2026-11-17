import { http } from '../../../shared/api/http';

export interface AuthUser {
  id: string;
  username: string;
  displayName: string;
  tenantId: string;
  permissions: string[];
}

export interface LoginResult {
  access_token: string;
  expires_in: number;
  refresh_token: string;
  user: AuthUser;
}

export async function login(tenantCode: string, username: string, password: string): Promise<LoginResult> {
  const response = await http.post<LoginResult>('/auth/login', {
    tenant_code: tenantCode,
    username,
    password,
  });
  return response.data;
}

export async function logout(refreshToken: string): Promise<void> {
  await http.post('/auth/logout', { refresh_token: refreshToken });
}

export async function fetchCurrentUser(): Promise<AuthUser> {
  const response = await http.get<{
    userId: string;
    username: string;
    displayName: string;
    tenantId: string;
    permissions: string[];
  }>('/auth/me');
  return {
    id: response.data.userId,
    username: response.data.username,
    displayName: response.data.displayName,
    tenantId: response.data.tenantId,
    permissions: response.data.permissions,
  };
}
