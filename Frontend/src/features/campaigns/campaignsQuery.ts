import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { CampaignListParams, CampaignQueryState } from './campaigns.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set([
  'name',
  '-name',
  'dateCreate',
  '-dateCreate',
  'dateUpdate',
  '-dateUpdate',
  'dateStart',
  '-dateStart',
  'budget',
  '-budget'
]);

export type CampaignQueryFilterKey =
  | 'search'
  | 'sort'
  | 'idProduct'
  | 'idSetCampaign'
  | 'status'
  | 'type'
  | 'dateStartFrom'
  | 'dateStartTo'
  | 'dateEndFrom'
  | 'dateEndTo';

export function parseCampaignsQuery(query: LocationQuery): CampaignQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    idProduct: readString(query.idProduct),
    idSetCampaign: readString(query.idSetCampaign),
    status: readNonNegativeNumberString(query.status),
    type: readNonNegativeNumberString(query.type),
    dateStartFrom: readDate(query.dateStartFrom),
    dateStartTo: readDate(query.dateStartTo),
    dateEndFrom: readDate(query.dateEndFrom),
    dateEndTo: readDate(query.dateEndTo)
  };
}

export function toCampaignsApiParams(state: CampaignQueryState): CampaignListParams {
  const status = Number(state.status);
  const type = Number(state.type);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idProduct ? { idProduct: state.idProduct } : {}),
    ...(state.idSetCampaign ? { idSetCampaign: state.idSetCampaign } : {}),
    ...(isNonNegativeNumberString(state.status) ? { status } : {}),
    ...(isNonNegativeNumberString(state.type) ? { type } : {}),
    ...(state.dateStartFrom ? { dateStartFrom: toUtcStartOfDay(state.dateStartFrom) } : {}),
    ...(state.dateStartTo ? { dateStartTo: toUtcEndOfDay(state.dateStartTo) } : {}),
    ...(state.dateEndFrom ? { dateEndFrom: toUtcStartOfDay(state.dateEndFrom) } : {}),
    ...(state.dateEndTo ? { dateEndTo: toUtcEndOfDay(state.dateEndTo) } : {})
  };
}

export function toCampaignsRouteQuery(state: CampaignQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    idProduct: state.idProduct || undefined,
    idSetCampaign: state.idSetCampaign || undefined,
    status: isNonNegativeNumberString(state.status) ? state.status : undefined,
    type: isNonNegativeNumberString(state.type) ? state.type : undefined,
    dateStartFrom: DATE_PATTERN.test(state.dateStartFrom) ? state.dateStartFrom : undefined,
    dateStartTo: DATE_PATTERN.test(state.dateStartTo) ? state.dateStartTo : undefined,
    dateEndFrom: DATE_PATTERN.test(state.dateEndFrom) ? state.dateEndFrom : undefined,
    dateEndTo: DATE_PATTERN.test(state.dateEndTo) ? state.dateEndTo : undefined
  };
}

export function resetCampaignQueryFilters(state: CampaignQueryState): CampaignQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: '',
    idProduct: '',
    idSetCampaign: '',
    status: '',
    type: '',
    dateStartFrom: '',
    dateStartTo: '',
    dateEndFrom: '',
    dateEndTo: ''
  };
}

export function removeCampaignQueryFilter(
  state: CampaignQueryState,
  key: CampaignQueryFilterKey
): CampaignQueryState {
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

function readDate(value: LocationQuery[string]): string {
  const text = readString(value);
  return DATE_PATTERN.test(text) ? text : '';
}

function readPositiveInt(value: LocationQuery[string], fallback: number): number {
  const parsed = Number.parseInt(readString(value), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function readNonNegativeNumberString(value: LocationQuery[string]): string {
  const text = readString(value);
  return isNonNegativeNumberString(text) ? text : '';
}

function isNonNegativeNumberString(value: string): boolean {
  if (!value.trim()) {
    return false;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0;
}

function toUtcStartOfDay(value: string): string {
  return `${value}T00:00:00.000Z`;
}

function toUtcEndOfDay(value: string): string {
  return `${value}T23:59:59.999Z`;
}
