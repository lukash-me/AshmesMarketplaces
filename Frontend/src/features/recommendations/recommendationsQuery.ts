import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { RecommendationListParams, RecommendationQueryState } from './recommendations.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const ALLOWED_SORT_VALUES = new Set([
  'score',
  '-score',
  'dateCreate',
  '-dateCreate',
  'dateUpdate',
  '-dateUpdate'
]);

export type RecommendationQueryFilterKey =
  | 'sort'
  | 'idModel'
  | 'type'
  | 'typeObject'
  | 'dateCreateFrom'
  | 'dateCreateTo';

export function parseRecommendationsQuery(query: LocationQuery): RecommendationQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    idModel: readString(query.idModel),
    type: readNonNegativeNumberString(query.type),
    typeObject: readNonNegativeNumberString(query.typeObject),
    dateCreateFrom: readDate(query.dateCreateFrom),
    dateCreateTo: readDate(query.dateCreateTo)
  };
}

export function toRecommendationsApiParams(
  state: RecommendationQueryState
): RecommendationListParams {
  const type = Number(state.type);
  const typeObject = Number(state.typeObject);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idModel ? { idModel: state.idModel } : {}),
    ...(isNonNegativeNumberString(state.type) ? { type } : {}),
    ...(isNonNegativeNumberString(state.typeObject) ? { typeObject } : {}),
    ...(state.dateCreateFrom ? { dateCreateFrom: toUtcStartOfDay(state.dateCreateFrom) } : {}),
    ...(state.dateCreateTo ? { dateCreateTo: toUtcEndOfDay(state.dateCreateTo) } : {})
  };
}

export function toRecommendationsRouteQuery(state: RecommendationQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: ALLOWED_SORT_VALUES.has(state.sort) ? state.sort : undefined,
    idModel: state.idModel || undefined,
    type: isNonNegativeNumberString(state.type) ? state.type : undefined,
    typeObject: isNonNegativeNumberString(state.typeObject) ? state.typeObject : undefined,
    dateCreateFrom: DATE_PATTERN.test(state.dateCreateFrom) ? state.dateCreateFrom : undefined,
    dateCreateTo: DATE_PATTERN.test(state.dateCreateTo) ? state.dateCreateTo : undefined
  };
}

export function resetRecommendationQueryFilters(
  state: RecommendationQueryState
): RecommendationQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    idModel: '',
    type: '',
    typeObject: '',
    dateCreateFrom: '',
    dateCreateTo: ''
  };
}

export function removeRecommendationQueryFilter(
  state: RecommendationQueryState,
  key: RecommendationQueryFilterKey
): RecommendationQueryState {
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
