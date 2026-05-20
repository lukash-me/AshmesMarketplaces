import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { LogisticListParams, LogisticQueryState } from './logistics.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set([
  'date',
  '-date',
  'stockAmountStatistic',
  '-stockAmountStatistic'
]);

export type LogisticQueryFilterKey =
  | 'search'
  | 'sort'
  | 'idProduct'
  | 'idWarehouse'
  | 'type'
  | 'dateFrom'
  | 'dateTo';

export function parseLogisticsQuery(query: LocationQuery): LogisticQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    idProduct: readString(query.idProduct),
    idWarehouse: readString(query.idWarehouse),
    type: readNonNegativeNumberString(query.type),
    dateFrom: readDate(query.dateFrom),
    dateTo: readDate(query.dateTo)
  };
}

export function toLogisticsApiParams(state: LogisticQueryState): LogisticListParams {
  const type = Number(state.type);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idProduct ? { idProduct: state.idProduct } : {}),
    ...(state.idWarehouse ? { idWarehouse: state.idWarehouse } : {}),
    ...(isNonNegativeNumberString(state.type) ? { type } : {}),
    ...(state.dateFrom ? { dateFrom: toUtcStartOfDay(state.dateFrom) } : {}),
    ...(state.dateTo ? { dateTo: toUtcEndOfDay(state.dateTo) } : {})
  };
}

export function toLogisticsRouteQuery(state: LogisticQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    idProduct: state.idProduct || undefined,
    idWarehouse: state.idWarehouse || undefined,
    type: isNonNegativeNumberString(state.type) ? state.type : undefined,
    dateFrom: DATE_PATTERN.test(state.dateFrom) ? state.dateFrom : undefined,
    dateTo: DATE_PATTERN.test(state.dateTo) ? state.dateTo : undefined
  };
}

export function resetLogisticQueryFilters(state: LogisticQueryState): LogisticQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: '',
    idProduct: '',
    idWarehouse: '',
    type: '',
    dateFrom: '',
    dateTo: ''
  };
}

export function removeLogisticQueryFilter(
  state: LogisticQueryState,
  key: LogisticQueryFilterKey
): LogisticQueryState {
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
