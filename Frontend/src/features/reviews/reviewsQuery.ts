import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { ReviewListParams, ReviewQueryState } from './reviews.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set(['dateCreate', '-dateCreate', 'rating', '-rating']);
const BOOLEAN_VALUES = new Set(['true', 'false']);

export type ReviewQueryFilterKey =
  | 'search'
  | 'sort'
  | 'idProduct'
  | 'rating'
  | 'isReplied'
  | 'dateCreateFrom'
  | 'dateCreateTo';

export function parseReviewsQuery(query: LocationQuery): ReviewQueryState {
  const sort = readString(query.sort);
  const isReplied = readString(query.isReplied);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    idProduct: readString(query.idProduct),
    rating: readString(query.rating),
    isReplied: BOOLEAN_VALUES.has(isReplied) ? isReplied : '',
    dateCreateFrom: readDate(query.dateCreateFrom),
    dateCreateTo: readDate(query.dateCreateTo)
  };
}

export function toReviewsApiParams(state: ReviewQueryState): ReviewListParams {
  const rating = Number(state.rating);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idProduct ? { idProduct: state.idProduct } : {}),
    ...(state.rating && Number.isFinite(rating) ? { rating } : {}),
    ...(state.isReplied === 'true' || state.isReplied === 'false'
      ? { isReplied: state.isReplied === 'true' }
      : {}),
    ...(state.dateCreateFrom ? { dateCreateFrom: toUtcStartOfDay(state.dateCreateFrom) } : {}),
    ...(state.dateCreateTo ? { dateCreateTo: toUtcEndOfDay(state.dateCreateTo) } : {})
  };
}

export function toReviewsRouteQuery(state: ReviewQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: state.sort || undefined,
    idProduct: state.idProduct || undefined,
    rating: state.rating || undefined,
    isReplied: state.isReplied || undefined,
    dateCreateFrom: state.dateCreateFrom || undefined,
    dateCreateTo: state.dateCreateTo || undefined
  };
}

export function resetReviewQueryFilters(state: ReviewQueryState): ReviewQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: '',
    idProduct: '',
    rating: '',
    isReplied: '',
    dateCreateFrom: '',
    dateCreateTo: ''
  };
}

export function removeReviewQueryFilter(
  state: ReviewQueryState,
  key: ReviewQueryFilterKey
): ReviewQueryState {
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
