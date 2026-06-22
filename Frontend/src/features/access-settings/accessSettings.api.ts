import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  PermissionDetail,
  PermissionListItem,
  PermissionListParams,
  RoleDetail,
  RoleListItem,
  RoleListParams,
  AddManagementWorkspaceMemberRequest,
  CreateManagementWorkspaceRequest,
  CreateUserWorkspaceRequest,
  CreateWorkspaceRequest,
  ManagementWorkspace,
  ManagementWorkspaceMember,
  ManagementWorkspaceRole,
  UpdateManagementWorkspaceRequest,
  UpdateManagementWorkspaceMemberRoleRequest,
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

export async function getManagementWorkspaces(): Promise<ManagementWorkspace[]> {
  const response = await http.get<ManagementWorkspace[]>('/management/workspaces');
  return response.data;
}

export async function getManagementWorkspaceRoles(): Promise<ManagementWorkspaceRole[]> {
  const response = await http.get<ManagementWorkspaceRole[]>('/management/workspaces/roles');
  return response.data;
}

export async function createManagementWorkspace(
  request: CreateManagementWorkspaceRequest
): Promise<ManagementWorkspace> {
  const response = await http.post<ManagementWorkspace>('/management/workspaces', request);
  return response.data;
}

export async function updateManagementWorkspace(
  workspaceId: string,
  request: UpdateManagementWorkspaceRequest
): Promise<ManagementWorkspace> {
  const response = await http.put<ManagementWorkspace>(`/management/workspaces/${workspaceId}`, request);
  return response.data;
}

export async function deleteManagementWorkspace(workspaceId: string): Promise<void> {
  await http.delete(`/management/workspaces/${workspaceId}`);
}

export async function addManagementWorkspaceMember(
  workspaceId: string,
  request: AddManagementWorkspaceMemberRequest
): Promise<ManagementWorkspaceMember> {
  const response = await http.post<ManagementWorkspaceMember>(
    `/management/workspaces/${workspaceId}/members`,
    request
  );
  return response.data;
}

export async function updateManagementWorkspaceMemberRole(
  workspaceId: string,
  userId: string,
  request: UpdateManagementWorkspaceMemberRoleRequest
): Promise<ManagementWorkspaceMember> {
  const response = await http.put<ManagementWorkspaceMember>(
    `/management/workspaces/${workspaceId}/members/${userId}/role`,
    request
  );
  return response.data;
}

export async function deleteManagementWorkspaceMember(
  workspaceId: string,
  userId: string
): Promise<void> {
  await http.delete(`/management/workspaces/${workspaceId}/members/${userId}`);
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

export async function createUserWorkspace(
  request: CreateUserWorkspaceRequest
): Promise<UserWorkspaceDetail> {
  const response = await http.post<UserWorkspaceDetail>('/user-workspaces', request);
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

export async function createWorkspace(request: CreateWorkspaceRequest): Promise<WorkspaceDetail> {
  const response = await http.post<WorkspaceDetail>('/workspaces', request);
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
