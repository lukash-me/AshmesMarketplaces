import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type {
  ParserProductFilterOptionsParams,
  ParserProductLogisticsSummaryParams,
  ParserProductListParams,
  ParserProductQueryState
} from './parserProducts.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const ALLOWED_SORT_VALUES = new Set([
  'name',
  '-name',
  'parsedAtUtc',
  '-parsedAtUtc',
  'wbProductId',
  '-wbProductId',
  'position',
  '-position',
  'price',
  '-price',
  'priceDiscounted',
  '-priceDiscounted',
  'reviewRating',
  '-reviewRating',
  'feedbackCount',
  '-feedbackCount'
]);

export type ParserProductQueryFilterKey =
  | 'sort'
  | 'search'
  | 'parserRunId'
  | 'sourcePath'
  | 'wbCategoryId'
  | 'sourceCategory'
  | 'sourceSubcategory'
  | 'brandName'
  | 'sellerName'
  | 'wbRootId'
  | 'priceDiscountedFrom'
  | 'priceDiscountedTo'
  | 'reviewRatingFrom'
  | 'reviewRatingTo'
  | 'feedbackCountFrom'
  | 'feedbackCountTo';

export function parseParserProductsQuery(query: LocationQuery): ParserProductQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    sort: ALLOWED_SORT_VALUES.has(sort) ? sort : '',
    search: readString(query.search),
    parserRunId: readString(query.parserRunId),
    sourceCategory: readString(query.sourceCategory),
    sourceSubcategory: readString(query.sourceSubcategory),
    wbCategoryId: readString(query.wbCategoryId),
    sourcePath: readString(query.sourcePath),
    brandName: readString(query.brandName),
    sellerName: readString(query.sellerName),
    wbRootId: readString(query.wbRootId),
    priceDiscountedFrom: readNumberText(query.priceDiscountedFrom),
    priceDiscountedTo: readNumberText(query.priceDiscountedTo),
    reviewRatingFrom: readNumberText(query.reviewRatingFrom),
    reviewRatingTo: readNumberText(query.reviewRatingTo),
    feedbackCountFrom: readNumberText(query.feedbackCountFrom),
    feedbackCountTo: readNumberText(query.feedbackCountTo)
  };
}

export function toParserProductsApiParams(state: ParserProductQueryState): ParserProductListParams {
  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.search ? { search: state.search } : {}),
    ...(state.parserRunId ? { parserRunId: state.parserRunId } : {}),
    ...(state.sourceCategory ? { sourceCategory: state.sourceCategory } : {}),
    ...(state.sourceSubcategory ? { sourceSubcategory: state.sourceSubcategory } : {}),
    ...(state.brandName ? { brandName: state.brandName } : {}),
    ...(state.sellerName ? { sellerName: state.sellerName } : {}),
    ...(state.wbRootId ? { wbRootId: state.wbRootId } : {}),
    ...numberParam('priceDiscountedFrom', state.priceDiscountedFrom),
    ...numberParam('priceDiscountedTo', state.priceDiscountedTo),
    ...numberParam('reviewRatingFrom', state.reviewRatingFrom),
    ...numberParam('reviewRatingTo', state.reviewRatingTo),
    ...numberParam('feedbackCountFrom', state.feedbackCountFrom),
    ...numberParam('feedbackCountTo', state.feedbackCountTo)
  };
}

export function toParserProductFilterOptionsParams(
  state: ParserProductQueryState
): ParserProductFilterOptionsParams {
  return {
    ...(state.search ? { search: state.search } : {}),
    ...(state.parserRunId ? { parserRunId: state.parserRunId } : {}),
    ...(state.sourceCategory ? { sourceCategory: state.sourceCategory } : {}),
    ...(state.sourceSubcategory ? { sourceSubcategory: state.sourceSubcategory } : {}),
    ...(state.brandName ? { brandName: state.brandName } : {}),
    ...(state.sellerName ? { sellerName: state.sellerName } : {})
  };
}

export function toParserProductLogisticsSummaryParams(
  state: ParserProductQueryState
): ParserProductLogisticsSummaryParams {
  return {
    ...(state.search ? { search: state.search } : {}),
    ...(state.parserRunId ? { parserRunId: state.parserRunId } : {}),
    ...(state.sourceCategory ? { sourceCategory: state.sourceCategory } : {}),
    ...(state.sourceSubcategory ? { sourceSubcategory: state.sourceSubcategory } : {}),
    ...(state.brandName ? { brandName: state.brandName } : {}),
    ...(state.sellerName ? { sellerName: state.sellerName } : {}),
    ...(state.wbRootId ? { wbRootId: state.wbRootId } : {})
  };
}

export function toParserProductsRouteQuery(state: ParserProductQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    sort: state.sort || undefined,
    search: state.search || undefined,
    parserRunId: state.parserRunId || undefined,
    wbCategoryId: state.wbCategoryId || undefined,
    sourcePath: state.sourcePath || undefined,
    sourceCategory: state.sourceCategory || undefined,
    sourceSubcategory: state.sourceSubcategory || undefined,
    brandName: state.brandName || undefined,
    sellerName: state.sellerName || undefined,
    wbRootId: state.wbRootId || undefined,
    priceDiscountedFrom: state.priceDiscountedFrom || undefined,
    priceDiscountedTo: state.priceDiscountedTo || undefined,
    reviewRatingFrom: state.reviewRatingFrom || undefined,
    reviewRatingTo: state.reviewRatingTo || undefined,
    feedbackCountFrom: state.feedbackCountFrom || undefined,
    feedbackCountTo: state.feedbackCountTo || undefined
  };
}

export function resetParserProductQueryFilters(
  state: ParserProductQueryState
): ParserProductQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    sort: '',
    search: '',
    parserRunId: '',
    sourceCategory: '',
    sourceSubcategory: '',
    wbCategoryId: '',
    sourcePath: '',
    brandName: '',
    sellerName: '',
    wbRootId: '',
    priceDiscountedFrom: '',
    priceDiscountedTo: '',
    reviewRatingFrom: '',
    reviewRatingTo: '',
    feedbackCountFrom: '',
    feedbackCountTo: ''
  };
}

export function removeParserProductQueryFilter(
  state: ParserProductQueryState,
  key: ParserProductQueryFilterKey
): ParserProductQueryState {
  if (key === 'sourcePath' || key === 'wbCategoryId' || key === 'sourceCategory' || key === 'sourceSubcategory') {
    return {
      ...state,
      page: DEFAULT_PAGE,
      wbCategoryId: '',
      sourcePath: '',
      sourceCategory: '',
      sourceSubcategory: ''
    };
  }

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

function readNumberText(value: LocationQuery[string]): string {
  const text = readString(value);
  return text === '' || Number.isFinite(Number(text)) ? text : '';
}

function readPositiveInt(value: LocationQuery[string], fallback: number): number {
  const parsed = Number.parseInt(readString(value), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function numberParam<K extends keyof ParserProductListParams>(key: K, value: string) {
  const number = Number(value);
  return value && Number.isFinite(number) ? { [key]: number } : {};
}
