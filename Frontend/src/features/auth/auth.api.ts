import { http, publicHttp } from '@/shared/api/http';

import type { AuthUser, LoginRequest, LoginResponse, RefreshRequest } from './auth.types';

export async function login(request: LoginRequest): Promise<LoginResponse> {
  const response = await publicHttp.post<LoginResponse>('/auth/login', request);
  return response.data;
}

export async function refresh(request: RefreshRequest): Promise<LoginResponse> {
  const response = await publicHttp.post<LoginResponse>('/auth/refresh', request);
  return response.data;
}

export async function logout(): Promise<void> {
  await http.post('/auth/logout');
}

export async function getMe(): Promise<AuthUser> {
  const response = await http.get<AuthUser>('/auth/me');
  return response.data;
}
