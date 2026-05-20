import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  PermissionDetail,
  PermissionListItem,
  PermissionListParams,
  RoleDetail,
  RoleListItem,
  RoleListParams,
  RolePermissionListItem,
  RolePermissionListParams,
  UserDetail,
  UserListItem,
  UserListParams,
  UserWorkspaceDetail,
  UserWorkspaceListItem,
  UserWorkspaceListParams,
  WorkspaceDetail,
  WorkspaceListItem,
  WorkspaceListParams
} from './accessSettings.types';

export async function getUserWorkspaces(
  params: UserWorkspaceListParams
): Promise<PagedResponse<UserWorkspaceListItem>> {
  const response = await http.get<PagedResponse<UserWorkspaceListItem>>('/user-workspaces', {
    params
  });
  return response.data;
}

export async function getUserWorkspace(
  idUser: string,
  idWorkspace: string
): Promise<UserWorkspaceDetail> {
  const response = await http.get<UserWorkspaceDetail>(
    `/user-workspaces/${idUser}/${idWorkspace}`
  );
  return response.data;
}

export async function getUsers(params: UserListParams): Promise<PagedResponse<UserListItem>> {
  const response = await http.get<PagedResponse<UserListItem>>('/users', { params });
  return response.data;
}

export async function getUser(id: string): Promise<UserDetail> {
  const response = await http.get<UserDetail>(`/users/${id}`);
  return response.data;
}

export async function getWorkspaces(
  params: WorkspaceListParams
): Promise<PagedResponse<WorkspaceListItem>> {
  const response = await http.get<PagedResponse<WorkspaceListItem>>('/workspaces', { params });
  return response.data;
}

export async function getWorkspace(id: string): Promise<WorkspaceDetail> {
  const response = await http.get<WorkspaceDetail>(`/workspaces/${id}`);
  return response.data;
}

export async function getRoles(params: RoleListParams): Promise<PagedResponse<RoleListItem>> {
  const response = await http.get<PagedResponse<RoleListItem>>('/roles', { params });
  return response.data;
}

export async function getRole(id: string): Promise<RoleDetail> {
  const response = await http.get<RoleDetail>(`/roles/${id}`);
  return response.data;
}

export async function getPermissions(
  params: PermissionListParams
): Promise<PagedResponse<PermissionListItem>> {
  const response = await http.get<PagedResponse<PermissionListItem>>('/permissions', { params });
  return response.data;
}

export async function getPermission(id: string): Promise<PermissionDetail> {
  const response = await http.get<PermissionDetail>(`/permissions/${id}`);
  return response.data;
}

export async function getRolePermissions(
  params: RolePermissionListParams
): Promise<PagedResponse<RolePermissionListItem>> {
  const response = await http.get<PagedResponse<RolePermissionListItem>>('/role-permissions', {
    params
  });
  return response.data;
}

