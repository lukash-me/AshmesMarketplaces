import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { ExpenseListParams, ExpenseQueryState } from './expenses.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set([
  'name',
  '-name',
  'cost',
  '-cost',
  'datePay',
  '-datePay',
  'dateCreate',
  '-dateCreate',
  'dateUpdate',
  '-dateUpdate'
]);

export type ExpenseQueryFilterKey =
  | 'sort'
  | 'search'
  | 'idWorkspace'
  | 'idCategory'
  | 'idCreator'
  | 'idResponsible'
  | 'status'
  | 'datePayFrom'
  | 'datePayTo'
  | 'dateCreateFrom'
  | 'dateCreateTo';

export function parseExpensesQuery(query: LocationQuery): ExpenseQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    search: readString(query.search),
    idWorkspace: readString(query.idWorkspace),
    idCategory: readString(query.idCategory),
    idCreator: readString(query.idCreator),
    idResponsible: readString(query.idResponsible),
    status: readNonNegativeNumberString(query.status),
    datePayFrom: readDate(query.datePayFrom),
    datePayTo: readDate(query.datePayTo),
    dateCreateFrom: readDate(query.dateCreateFrom),
    dateCreateTo: readDate(query.dateCreateTo)
  };
}

export function toExpensesApiParams(state: ExpenseQueryState): ExpenseListParams {
  const status = Number(state.status);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.search ? { search: state.search } : {}),
    ...(state.idWorkspace ? { idWorkspace: state.idWorkspace } : {}),
    ...(state.idCategory ? { idCategory: state.idCategory } : {}),
    ...(state.idCreator ? { idCreator: state.idCreator } : {}),
    ...(state.idResponsible ? { idResponsible: state.idResponsible } : {}),
    ...(isNonNegativeNumberString(state.status) ? { status } : {}),
    ...(state.datePayFrom ? { datePayFrom: toUtcStartOfDay(state.datePayFrom) } : {}),
    ...(state.datePayTo ? { datePayTo: toUtcEndOfDay(state.datePayTo) } : {}),
    ...(state.dateCreateFrom ? { dateCreateFrom: toUtcStartOfDay(state.dateCreateFrom) } : {}),
    ...(state.dateCreateTo ? { dateCreateTo: toUtcEndOfDay(state.dateCreateTo) } : {})
  };
}

export function toExpensesRouteQuery(state: ExpenseQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    search: state.search || undefined,
    idWorkspace: state.idWorkspace || undefined,
    idCategory: state.idCategory || undefined,
    idCreator: state.idCreator || undefined,
    idResponsible: state.idResponsible || undefined,
    status: isNonNegativeNumberString(state.status) ? state.status : undefined,
    datePayFrom: DATE_PATTERN.test(state.datePayFrom) ? state.datePayFrom : undefined,
    datePayTo: DATE_PATTERN.test(state.datePayTo) ? state.datePayTo : undefined,
    dateCreateFrom: DATE_PATTERN.test(state.dateCreateFrom) ? state.dateCreateFrom : undefined,
    dateCreateTo: DATE_PATTERN.test(state.dateCreateTo) ? state.dateCreateTo : undefined
  };
}

export function resetExpenseQueryFilters(state: ExpenseQueryState): ExpenseQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    search: '',
    idWorkspace: '',
    idCategory: '',
    idCreator: '',
    idResponsible: '',
    status: '',
    datePayFrom: '',
    datePayTo: '',
    dateCreateFrom: '',
    dateCreateTo: ''
  };
}

export function removeExpenseQueryFilter(
  state: ExpenseQueryState,
  key: ExpenseQueryFilterKey
): ExpenseQueryState {
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
