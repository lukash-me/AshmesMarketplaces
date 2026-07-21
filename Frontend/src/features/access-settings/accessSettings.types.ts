export interface UserWorkspaceListItem {
  idUser: string;
  idWorkspace: string;
  idRole: string;
}

export type UserWorkspaceDetail = UserWorkspaceListItem;

export interface UserWorkspaceListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idUser?: string;
  idWorkspace?: string;
  idRole?: string;
}

export interface UserListItem {
  id: string;
  idRole: string;
  login: string;
  email: string | null;
  phone: string;
  status: number;
  dateCreate: string;
  dateLogin: string;
}

export type UserDetail = UserListItem;

export interface UserListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idRole?: string;
  status?: number;
}

export interface WorkspaceListItem {
  id: string;
  idBrand: string | null;
  name: string;
  description: string | null;
  urlInvite: string | null;
  status: number;
  dateCreate: string;
  dateUpdate: string;
}

export type WorkspaceDetail = WorkspaceListItem;

export interface WorkspaceListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idBrand?: string;
  status?: number;
}

export interface CreateWorkspaceRequest {
  idBrand: string | null;
  name: string;
  description: string | null;
  urlInvite: string | null;
  status: number;
  dateCreate: string;
  dateUpdate: string;
}

export interface RoleListItem {
  id: string;
  name: string;
  description: string | null;
  dateCreate: string;
  dateUpdate: string;
}

export type RoleDetail = RoleListItem;

export interface RoleListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
}

export interface PermissionListItem {
  id: string;
  idCategory: string;
  name: string;
  description: string;
  domain: number;
  dateCreate: string;
  dateUpdate: string;
}

export type PermissionDetail = PermissionListItem;

export interface PermissionListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idCategory?: string;
  domain?: number;
}

export interface RolePermissionListItem {
  idRole: string;
  idPermission: string;
}

export interface RolePermissionListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idRole?: string;
  idPermission?: string;
}

export interface CreateUserWorkspaceRequest {
  idUser: string;
  idWorkspace: string;
  idRole: string;
}

export interface ManagementWorkspaceMember {
  idUser: string;
  login: string;
  email: string | null;
  idRole: string;
  roleName: string;
  isCurrentUser: boolean;
}

export interface ManagementWorkspaceRole {
  id: string;
  name: string;
}

export interface ManagementWorkspace {
  id: string;
  idBrand: string | null;
  name: string;
  description: string | null;
  status: number;
  dateCreate: string;
  dateUpdate: string;
  members: ManagementWorkspaceMember[];
}

export interface CreateManagementWorkspaceRequest {
  name: string;
  description: string | null;
}

export type UpdateManagementWorkspaceRequest = CreateManagementWorkspaceRequest;

export interface AddManagementWorkspaceMemberRequest {
  email: string;
  idRole: string;
}

export interface UpdateManagementWorkspaceMemberRoleRequest {
  idRole: string;
}

export type AccessSettingsQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  search: string;
  idUser: string;
  idWorkspace: string;
  idRole: string;
};
