import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type {
  ObservedMarketEventParams,
  ObservedMarketEventQueryState,
  ObservedMarketEventTab,
  ObservedMarketEventType,
  ObservedStockDecreaseParams,
  ObservedStockDecreaseQueryState,
  OrderListParams,
  OrderQueryState
} from './orders.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;
const DEFAULT_OBSERVED_STOCK_DECREASE_SORT = '-decrease';
const DEFAULT_OBSERVED_MARKET_EVENT_SORT = '';
const DEFAULT_MIN_DECREASE = '1';
const DEFAULT_MIN_QUANTITY_CHANGE = '1';
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
const ALLOWED_OBSERVED_STOCK_DECREASE_SORT_VALUES = new Set([
  'decrease',
  '-decrease',
  'currentQuantity',
  '-currentQuantity',
  'previousQuantity',
  '-previousQuantity',
  'observedAtUtc',
  '-observedAtUtc'
]);
const ALLOWED_OBSERVED_MARKET_EVENT_SORT_VALUES = new Set([
  '',
  'eventType',
  '-eventType',
  'quantityChange',
  '-quantityChange',
  'currentQuantity',
  '-currentQuantity',
  'previousQuantity',
  '-previousQuantity',
  'observedAtUtc',
  '-observedAtUtc'
]);
const OBSERVED_MARKET_EVENT_TABS = new Set<ObservedMarketEventTab>([
  'all',
  'assumed-orders',
  'new-products',
  'restocks'
]);

export type OrderQueryFilterKey =
  | 'search'
  | 'sort'
  | 'idProduct'
  | 'status'
  | 'dateOpenedFrom'
  | 'dateOpenedTo';

export type ObservedStockDecreaseQueryFilterKey =
  | 'search'
  | 'sort'
  | 'sourceCategory'
  | 'sourceSubcategory'
  | 'brandName'
  | 'sellerName'
  | 'minDecrease';

export type ObservedMarketEventQueryFilterKey =
  | 'search'
  | 'sort'
  | 'sourceCategory'
  | 'sourceSubcategory'
  | 'brandName'
  | 'sellerName'
  | 'minQuantityChange';

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

export function parseObservedStockDecreaseQuery(query: LocationQuery): ObservedStockDecreaseQueryState {
  const sort = readString(query.sort);

  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_OBSERVED_STOCK_DECREASE_SORT_VALUES.has(sort)
      ? sort
      : DEFAULT_OBSERVED_STOCK_DECREASE_SORT,
    sourceCategory: readString(query.sourceCategory),
    sourceSubcategory: readString(query.sourceSubcategory),
    brandName: readString(query.brandName),
    sellerName: readString(query.sellerName),
    minDecrease: readMinDecrease(query.minDecrease)
  };
}

export function toObservedStockDecreaseApiParams(
  state: ObservedStockDecreaseQueryState
): ObservedStockDecreaseParams {
  const minDecrease = Number(state.minDecrease);

  return {
    page: state.page,
    pageSize: state.pageSize,
    sort: state.sort,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sourceCategory ? { sourceCategory: state.sourceCategory } : {}),
    ...(state.sourceSubcategory ? { sourceSubcategory: state.sourceSubcategory } : {}),
    ...(state.brandName ? { brandName: state.brandName } : {}),
    ...(state.sellerName ? { sellerName: state.sellerName } : {}),
    ...(Number.isFinite(minDecrease) && minDecrease >= 1 ? { minDecrease } : {})
  };
}

export function toObservedStockDecreaseRouteQuery(
  state: ObservedStockDecreaseQueryState
): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: state.sort === DEFAULT_OBSERVED_STOCK_DECREASE_SORT ? undefined : state.sort,
    sourceCategory: state.sourceCategory || undefined,
    sourceSubcategory: state.sourceSubcategory || undefined,
    brandName: state.brandName || undefined,
    sellerName: state.sellerName || undefined,
    minDecrease: state.minDecrease === DEFAULT_MIN_DECREASE ? undefined : state.minDecrease
  };
}

export function resetObservedStockDecreaseQueryFilters(
  state: ObservedStockDecreaseQueryState
): ObservedStockDecreaseQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: DEFAULT_OBSERVED_STOCK_DECREASE_SORT,
    sourceCategory: '',
    sourceSubcategory: '',
    brandName: '',
    sellerName: '',
    minDecrease: DEFAULT_MIN_DECREASE
  };
}

export function removeObservedStockDecreaseQueryFilter(
  state: ObservedStockDecreaseQueryState,
  key: ObservedStockDecreaseQueryFilterKey
): ObservedStockDecreaseQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    [key]: key === 'sort'
      ? DEFAULT_OBSERVED_STOCK_DECREASE_SORT
      : key === 'minDecrease'
        ? DEFAULT_MIN_DECREASE
        : ''
  };
}

export function parseObservedMarketEventQuery(query: LocationQuery): ObservedMarketEventQueryState {
  const sort = readString(query.sort);

  return {
    tab: readObservedMarketEventTab(query.tab),
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: ALLOWED_OBSERVED_MARKET_EVENT_SORT_VALUES.has(sort)
      ? sort
      : DEFAULT_OBSERVED_MARKET_EVENT_SORT,
    sourceCategory: readString(query.sourceCategory),
    sourceSubcategory: readString(query.sourceSubcategory),
    brandName: readString(query.brandName),
    sellerName: readString(query.sellerName),
    minQuantityChange: readMinQuantityChange(query.minQuantityChange ?? query.minDecrease)
  };
}

export function toObservedMarketEventApiParams(
  state: ObservedMarketEventQueryState,
  eventType?: ObservedMarketEventType,
  overrides: Partial<Pick<ObservedMarketEventParams, 'page' | 'pageSize'>> = {}
): ObservedMarketEventParams {
  const minQuantityChange = Number(state.minQuantityChange);

  return {
    page: overrides.page ?? state.page,
    pageSize: overrides.pageSize ?? state.pageSize,
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.search ? { search: state.search } : {}),
    ...(state.sourceCategory ? { sourceCategory: state.sourceCategory } : {}),
    ...(state.sourceSubcategory ? { sourceSubcategory: state.sourceSubcategory } : {}),
    ...(state.brandName ? { brandName: state.brandName } : {}),
    ...(state.sellerName ? { sellerName: state.sellerName } : {}),
    ...(eventType ? { eventType } : {}),
    ...(Number.isFinite(minQuantityChange) && minQuantityChange >= 1 ? { minQuantityChange } : {})
  };
}

export function toObservedMarketEventSummaryApiParams(
  state: ObservedMarketEventQueryState
): ObservedMarketEventParams {
  return toObservedMarketEventApiParams(state, undefined, { page: 1, pageSize: 1 });
}

export function toObservedMarketEventRouteQuery(
  state: ObservedMarketEventQueryState
): LocationQueryRaw {
  return {
    tab: state.tab === 'all' ? undefined : state.tab,
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: state.sort === DEFAULT_OBSERVED_MARKET_EVENT_SORT ? undefined : state.sort,
    sourceCategory: state.sourceCategory || undefined,
    sourceSubcategory: state.sourceSubcategory || undefined,
    brandName: state.brandName || undefined,
    sellerName: state.sellerName || undefined,
    minQuantityChange: state.minQuantityChange === DEFAULT_MIN_QUANTITY_CHANGE
      ? undefined
      : state.minQuantityChange
  };
}

export function resetObservedMarketEventQueryFilters(
  state: ObservedMarketEventQueryState
): ObservedMarketEventQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    search: '',
    sort: DEFAULT_OBSERVED_MARKET_EVENT_SORT,
    sourceCategory: '',
    sourceSubcategory: '',
    brandName: '',
    sellerName: '',
    minQuantityChange: DEFAULT_MIN_QUANTITY_CHANGE
  };
}

export function removeObservedMarketEventQueryFilter(
  state: ObservedMarketEventQueryState,
  key: ObservedMarketEventQueryFilterKey
): ObservedMarketEventQueryState {
  return {
    ...state,
    page: DEFAULT_PAGE,
    [key]: key === 'sort'
      ? DEFAULT_OBSERVED_MARKET_EVENT_SORT
      : key === 'minQuantityChange'
        ? DEFAULT_MIN_QUANTITY_CHANGE
        : ''
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

function readMinDecrease(value: LocationQuery[string]): string {
  const text = readString(value);
  const parsed = Number(text);
  return text !== '' && Number.isFinite(parsed) && parsed >= 1 ? text : DEFAULT_MIN_DECREASE;
}

function readMinQuantityChange(value: LocationQuery[string]): string {
  const text = readString(value);
  const parsed = Number(text);
  return text !== '' && Number.isFinite(parsed) && parsed >= 1 ? text : DEFAULT_MIN_QUANTITY_CHANGE;
}

function readObservedMarketEventTab(value: LocationQuery[string]): ObservedMarketEventTab {
  const raw = readString(value);

  if (raw === 'stock-changes') {
    return 'restocks';
  }

  const text = raw as ObservedMarketEventTab;
  return OBSERVED_MARKET_EVENT_TABS.has(text) ? text : 'all';
}

function toUtcStartOfDay(value: string): string {
  return `${value}T00:00:00.000Z`;
}

function toUtcEndOfDay(value: string): string {
  return `${value}T23:59:59.999Z`;
}
