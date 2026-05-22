import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { ParserReviewListParams, ParserReviewQueryState } from './parserReviews.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const BOOLEAN_VALUES = new Set(['true', 'false']);
const ALLOWED_SORT_VALUES = new Set([
  'createdAtOnMp',
  '-createdAtOnMp',
  'parsedAtUtc',
  '-parsedAtUtc',
  'rating',
  '-rating'
]);

export type ParserReviewQueryFilterKey =
  | 'sort'
  | 'search'
  | 'parserRunId'
  | 'wbProductId'
  | 'sourceWbRootId'
  | 'rating'
  | 'hasObservedReply'
  | 'cappedRootPayload'
  | 'reviewAttributionMode'
  | 'createdAtOnMpFrom'
  | 'createdAtOnMpTo';

export function parseParserReviewsQuery(query: LocationQuery): ParserReviewQueryState {
  const sort = readString(query.sort);
  const hasObservedReply = readString(query.hasObservedReply);
  const cappedRootPayload = readString(query.cappedRootPayload);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    search: readString(query.search),
    parserRunId: readString(query.parserRunId),
    wbProductId: readString(query.wbProductId),
    sourceWbRootId: readString(query.sourceWbRootId),
    rating: readString(query.rating),
    hasObservedReply: BOOLEAN_VALUES.has(hasObservedReply) ? hasObservedReply : '',
    cappedRootPayload: BOOLEAN_VALUES.has(cappedRootPayload) ? cappedRootPayload : '',
    reviewAttributionMode: readString(query.reviewAttributionMode),
    createdAtOnMpFrom: readDate(query.createdAtOnMpFrom),
    createdAtOnMpTo: readDate(query.createdAtOnMpTo)
  };
}

export function toParserReviewsApiParams(state: ParserReviewQueryState): ParserReviewListParams {
  const rating = Number(state.rating);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.search ? { search: state.search } : {}),
    ...(state.parserRunId ? { parserRunId: state.parserRunId } : {}),
    ...(state.wbProductId ? { wbProductId: state.wbProductId } : {}),
    ...(state.sourceWbRootId ? { sourceWbRootId: state.sourceWbRootId } : {}),
    ...(state.rating && Number.isFinite(rating) ? { rating } : {}),
    ...booleanParam('hasObservedReply', state.hasObservedReply),
    ...booleanParam('cappedRootPayload', state.cappedRootPayload),
    ...(state.reviewAttributionMode ? { reviewAttributionMode: state.reviewAttributionMode } : {}),
    ...(state.createdAtOnMpFrom ? { createdAtOnMpFrom: `${state.createdAtOnMpFrom}T00:00:00.000Z` } : {}),
    ...(state.createdAtOnMpTo ? { createdAtOnMpTo: `${state.createdAtOnMpTo}T23:59:59.999Z` } : {})
  };
}

export function toParserReviewsRouteQuery(state: ParserReviewQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: state.sort || undefined,
    search: state.search || undefined,
    parserRunId: state.parserRunId || undefined,
    wbProductId: state.wbProductId || undefined,
    sourceWbRootId: state.sourceWbRootId || undefined,
    rating: state.rating || undefined,
    hasObservedReply: state.hasObservedReply || undefined,
    cappedRootPayload: state.cappedRootPayload || undefined,
    reviewAttributionMode: state.reviewAttributionMode || undefined,
    createdAtOnMpFrom: state.createdAtOnMpFrom || undefined,
    createdAtOnMpTo: state.createdAtOnMpTo || undefined
  };
}

export function resetParserReviewQueryFilters(state: ParserReviewQueryState): ParserReviewQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    search: '',
    parserRunId: '',
    wbProductId: '',
    sourceWbRootId: '',
    rating: '',
    hasObservedReply: '',
    cappedRootPayload: '',
    reviewAttributionMode: '',
    createdAtOnMpFrom: '',
    createdAtOnMpTo: ''
  };
}

export function removeParserReviewQueryFilter(
  state: ParserReviewQueryState,
  key: ParserReviewQueryFilterKey
): ParserReviewQueryState {
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

function booleanParam<K extends keyof ParserReviewListParams>(key: K, value: string) {
  return value === 'true' || value === 'false' ? { [key]: value === 'true' } : {};
}
