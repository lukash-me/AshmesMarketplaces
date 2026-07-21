import type {
  PermissionListItem,
  RoleListItem,
  UserListItem,
  WorkspaceListItem
} from './accessSettings.types';

export type AccessBadgeTone =
  | 'neutral'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'ember'
  | 'hot';

export type UserLookup = Record<string, UserListItem>;
export type WorkspaceLookup = Record<string, WorkspaceListItem>;
export type RoleLookup = Record<string, RoleListItem>;
export type PermissionLookup = Record<string, PermissionListItem>;

export function compactId(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

export function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

export function getAccessNeutralTone(): AccessBadgeTone {
  return 'neutral';
}

export function getStatusLabel(status: number | null | undefined): string {
  return Number.isFinite(status) ? `Status ${status}` : 'Status -';
}

export function getDomainLabel(domain: number | null | undefined): string {
  return Number.isFinite(domain) ? `Domain ${domain}` : 'Domain -';
}

export function getUserLabel(idUser: string, usersById: UserLookup): string {
  const user = usersById[idUser];

  if (!user) {
    return compactId(idUser);
  }

  return user.email ? `${user.login} / ${user.email}` : user.login;
}

export function getWorkspaceLabel(idWorkspace: string, workspacesById: WorkspaceLookup): string {
  return workspacesById[idWorkspace]?.name ?? compactId(idWorkspace);
}

export function getRoleLabel(idRole: string, rolesById: RoleLookup): string {
  return rolesById[idRole]?.name ?? compactId(idRole);
}

export function getPermissionLabel(
  idPermission: string,
  permissionsById: PermissionLookup
): string {
  return permissionsById[idPermission]?.name ?? compactId(idPermission);
}

export function membershipKey(idUser: string, idWorkspace: string): string {
  return `${idUser}:${idWorkspace}`;
}

export function getAccessSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'idUser') {
    return `User ID ${direction}`;
  }

  if (field === 'idWorkspace') {
    return `Workspace ID ${direction}`;
  }

  return `${field} ${direction}`;
}

