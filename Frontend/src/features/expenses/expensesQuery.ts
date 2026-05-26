import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { ExpenseListParams, ExpenseQueryState, ExpenseStatusKey, ExpenseSummaryParams } from './expenses.types';

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
  'dateUpdate',
  '-dateUpdate'
]);
const ALLOWED_STATUS_KEYS = new Set(['planned', 'pending_payment', 'paid', 'cancelled']);

export type ExpenseQueryFilterKey =
  | 'sort'
  | 'search'
  | 'idCategory'
  | 'idResponsible'
  | 'statusKey'
  | 'datePayFrom'
  | 'datePayTo'
  | 'amountFrom'
  | 'amountTo';

export function parseExpensesQuery(query: LocationQuery): ExpenseQueryState {
  const sort = readString(query.sort);
  const statusKey = readString(query.statusKey);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    search: readString(query.search),
    idCategory: readString(query.idCategory),
    idResponsible: readString(query.idResponsible),
    statusKey: ALLOWED_STATUS_KEYS.has(statusKey) ? statusKey : '',
    datePayFrom: readDate(query.datePayFrom),
    datePayTo: readDate(query.datePayTo),
    amountFrom: readNonNegativeNumberString(query.amountFrom),
    amountTo: readNonNegativeNumberString(query.amountTo)
  };
}

export function toExpensesApiParams(state: ExpenseQueryState): ExpenseListParams {
  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...toExpenseFilterParams(state)
  };
}

export function toExpensesSummaryParams(state: ExpenseQueryState): ExpenseSummaryParams {
  return toExpenseFilterParams(state);
}

export function toExpensesRouteQuery(state: ExpenseQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    search: state.search || undefined,
    idCategory: state.idCategory || undefined,
    idResponsible: state.idResponsible || undefined,
    statusKey: ALLOWED_STATUS_KEYS.has(state.statusKey) ? state.statusKey : undefined,
    datePayFrom: DATE_PATTERN.test(state.datePayFrom) ? state.datePayFrom : undefined,
    datePayTo: DATE_PATTERN.test(state.datePayTo) ? state.datePayTo : undefined,
    amountFrom: isNonNegativeNumberString(state.amountFrom) ? state.amountFrom : undefined,
    amountTo: isNonNegativeNumberString(state.amountTo) ? state.amountTo : undefined
  };
}

export function resetExpenseQueryFilters(state: ExpenseQueryState): ExpenseQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    search: '',
    idCategory: '',
    idResponsible: '',
    statusKey: '',
    datePayFrom: '',
    datePayTo: '',
    amountFrom: '',
    amountTo: ''
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

function toExpenseFilterParams(state: ExpenseQueryState): ExpenseSummaryParams {
  const amountFrom = Number(state.amountFrom);
  const amountTo = Number(state.amountTo);

  return {
    ...(state.search ? { search: state.search } : {}),
    ...(state.idCategory ? { idCategory: state.idCategory, categoryId: state.idCategory } : {}),
    ...(state.idResponsible ? { idResponsible: state.idResponsible, responsibleUserId: state.idResponsible } : {}),
    ...(ALLOWED_STATUS_KEYS.has(state.statusKey) ? { statusKey: state.statusKey as ExpenseStatusKey } : {}),
    ...(state.datePayFrom ? { datePayFrom: toUtcStartOfDay(state.datePayFrom) } : {}),
    ...(state.datePayTo ? { datePayTo: toUtcEndOfDay(state.datePayTo) } : {}),
    ...(isNonNegativeNumberString(state.amountFrom) ? { amountFrom } : {}),
    ...(isNonNegativeNumberString(state.amountTo) ? { amountTo } : {})
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
