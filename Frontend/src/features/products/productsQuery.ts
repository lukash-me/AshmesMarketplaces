import type { LocationQuery, LocationQueryRaw } from 'vue-router';

import type { ProductListParams, ProductQueryState } from './products.types';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 50;

export function parseProductsQuery(query: LocationQuery): ProductQueryState {
  return {
    page: readPositiveInt(query.page, DEFAULT_PAGE),
    pageSize: readPositiveInt(query.pageSize, DEFAULT_PAGE_SIZE),
    search: readString(query.search),
    sort: readString(query.sort),
    idMp: readString(query.idMp),
    idBrand: readString(query.idBrand),
    idCategory: readString(query.idCategory),
    status: readString(query.status)
  };
}

export function toProductsApiParams(state: ProductQueryState): ProductListParams {
  const status = Number(state.status);

  return {
    page: state.page,
    pageSize: state.pageSize,
    ...(state.search ? { search: state.search } : {}),
    ...(state.sort ? { sort: state.sort } : {}),
    ...(state.idMp ? { idMp: state.idMp } : {}),
    ...(state.idBrand ? { idBrand: state.idBrand } : {}),
    ...(state.idCategory ? { idCategory: state.idCategory } : {}),
    ...(state.status && Number.isFinite(status) ? { status } : {})
  };
}

export function toProductsRouteQuery(state: ProductQueryState): LocationQueryRaw {
  return {
    page: state.page === DEFAULT_PAGE ? undefined : String(state.page),
    pageSize: state.pageSize === DEFAULT_PAGE_SIZE ? undefined : String(state.pageSize),
    search: state.search || undefined,
    sort: state.sort || undefined,
    idMp: state.idMp || undefined,
    idBrand: state.idBrand || undefined,
    idCategory: state.idCategory || undefined,
    status: state.status || undefined
  };
}

function readString(value: LocationQuery[string]): string {
  if (Array.isArray(value)) {
    return value[0] ?? '';
  }

  return typeof value === 'string' ? value : '';
}

function readPositiveInt(value: LocationQuery[string], fallback: number): number {
  const parsed = Number.parseInt(readString(value), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}
