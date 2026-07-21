import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type {
  AccessSettingsQueryState,
  UserWorkspaceListParams
} from './accessSettings.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const ALLOWED_SORT_VALUES = new Set(['idUser', '-idUser', 'idWorkspace', '-idWorkspace']);

export type AccessSettingsQueryFilterKey =
  | 'sort'
  | 'search'
  | 'idUser'
  | 'idWorkspace'
  | 'idRole';

export function parseAccessSettingsQuery(query: LocationQuery): AccessSettingsQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    search: readString(query.search),
    idUser: readString(query.idUser),
    idWorkspace: readString(query.idWorkspace),
    idRole: readString(query.idRole)
  };
}

export function toAccessSettingsApiParams(
  state: AccessSettingsQueryState
): UserWorkspaceListParams {
  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.search ? { search: state.search } : {}),
    ...(state.idUser ? { idUser: state.idUser } : {}),
    ...(state.idWorkspace ? { idWorkspace: state.idWorkspace } : {}),
    ...(state.idRole ? { idRole: state.idRole } : {})
  };
}

export function toAccessSettingsRouteQuery(state: AccessSettingsQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    search: state.search || undefined,
    idUser: state.idUser || undefined,
    idWorkspace: state.idWorkspace || undefined,
    idRole: state.idRole || undefined
  };
}

export function resetAccessSettingsQueryFilters(
  state: AccessSettingsQueryState
): AccessSettingsQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    search: '',
    idUser: '',
    idWorkspace: '',
    idRole: ''
  };
}

export function removeAccessSettingsQueryFilter(
  state: AccessSettingsQueryState,
  key: AccessSettingsQueryFilterKey
): AccessSettingsQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    [key]: ''
  };
}

function readString(value: LocationQuery[string]): string {
  if (Array.isArray(value)) {
    return value[0] ?? '';
  }

  return typeof value === 'string' ? value : '';
}

function readPositiveInt(value: LocationQuery[string], fallback: number): number {
  const parsed = Number.parseInt(readString(value), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

