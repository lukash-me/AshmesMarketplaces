import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { OrderListParams, OrderQueryState } from './orders.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set([
  'dateOpened',
  '-dateOpened',
  'dateUpdate',
  '-dateUpdate',
  'price',
  '-price',
  'amount',
  '-amount'
]);

export type OrderQueryFilterKey =
  | 'search'
  | 'sort'
  | 'idProduct'
  | 'status'
  | 'dateOpenedFrom'
  | 'dateOpenedTo';

export function parseOrdersQuery(query: LocationQuery): OrderQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    idProduct: readString(query.idProduct),
    status: readString(query.status),
    dateOpenedFrom: readDate(query.dateOpenedFrom),
    dateOpenedTo: readDate(query.dateOpenedTo)
  };
}

export function toOrdersApiParams(state: OrderQueryState): OrderListParams {
  const status = Number(state.status);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idProduct ? { idProduct: state.idProduct } : {}),
    ...(state.status && Number.isFinite(status) ? { status } : {}),
    ...(state.dateOpenedFrom ? { dateOpenedFrom: toUtcStartOfDay(state.dateOpenedFrom) } : {}),
    ...(state.dateOpenedTo ? { dateOpenedTo: toUtcEndOfDay(state.dateOpenedTo) } : {})
  };
}

export function toOrdersRouteQuery(state: OrderQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: state.sort || undefined,
    idProduct: state.idProduct || undefined,
    status: state.status || undefined,
    dateOpenedFrom: state.dateOpenedFrom || undefined,
    dateOpenedTo: state.dateOpenedTo || undefined
  };
}

export function resetOrderQueryFilters(state: OrderQueryState): OrderQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: '',
    idProduct: '',
    status: '',
    dateOpenedFrom: '',
    dateOpenedTo: ''
  };
}

export function removeOrderQueryFilter(
  state: OrderQueryState,
  key: OrderQueryFilterKey
): OrderQueryState {
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

function toUtcStartOfDay(value: string): string {
  return `${value}T00:00:00.000Z`;
}

function toUtcEndOfDay(value: string): string {
  return `${value}T23:59:59.999Z`;
}
