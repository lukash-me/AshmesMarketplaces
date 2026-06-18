import type { PagedResponse } from '@/entities/pagination';
import { publicHttp } from '@/shared/api/http';

import type {
  ParserDemoCardOptions,
  ParserDemoCardCharacteristics,
  ParserProductDetail,
  ParserProductFilterOptions,
  ParserProductFilterOptionsParams,
  ParserProductLogisticsSummaryAggregate,
  ParserProductLogisticsSummaryParams,
  ParserProductListItem,
  ParserProductListParams
} from './parserProducts.types';

let demoCardOptionsCache: ParserDemoCardOptions | null = null;
let demoCardOptionsRequest: Promise<ParserDemoCardOptions> | null = null;
const demoCardCharacteristicsCache = new Map<string, ParserDemoCardCharacteristics>();
const demoCardCharacteristicsRequests = new Map<string, Promise<ParserDemoCardCharacteristics>>();

export async function getParserProducts(
  params: ParserProductListParams
): Promise<PagedResponse<ParserProductListItem>> {
  const response = await publicHttp.get<PagedResponse<ParserProductListItem>>('/parser/products', { params });
  return response.data;
}

export async function getParserProductFilterOptions(
  params: ParserProductFilterOptionsParams
): Promise<ParserProductFilterOptions> {
  const response = await publicHttp.get<ParserProductFilterOptions>('/parser/products/filter-options', { params });
  return response.data;
}

export async function getParserDemoCardOptions(): Promise<ParserDemoCardOptions> {
  if (demoCardOptionsCache) {
    return demoCardOptionsCache;
  }

  demoCardOptionsRequest ??= publicHttp
    .get<ParserDemoCardOptions>('/parser/products/demo-card-options', {
      params: { includeCharacteristics: false }
    })
    .then((response) => {
      demoCardOptionsCache = response.data;
      return response.data;
    })
    .finally(() => {
      demoCardOptionsRequest = null;
    });

  return demoCardOptionsRequest;
}

export async function getParserDemoCardCharacteristics(
  subcategory: string
): Promise<ParserDemoCardCharacteristics> {
  const key = subcategory.trim();
  const cached = demoCardCharacteristicsCache.get(key);
  if (cached) {
    return cached;
  }

  if (!demoCardCharacteristicsRequests.has(key)) {
    demoCardCharacteristicsRequests.set(
      key,
      publicHttp
        .get<ParserDemoCardCharacteristics>('/parser/products/demo-card-options/characteristics', {
          params: { subcategory: key }
        })
        .then((response) => {
          demoCardCharacteristicsCache.set(key, response.data);
          return response.data;
        })
        .finally(() => {
          demoCardCharacteristicsRequests.delete(key);
        })
    );
  }

  return demoCardCharacteristicsRequests.get(key)!;
}

export async function getParserProductLogisticsSummary(
  params: ParserProductLogisticsSummaryParams
): Promise<ParserProductLogisticsSummaryAggregate> {
  const response = await publicHttp.get<ParserProductLogisticsSummaryAggregate>(
    '/parser/products/logistics-summary',
    { params }
  );
  return response.data;
}

export async function getParserProduct(id: string): Promise<ParserProductDetail> {
  const response = await publicHttp.get<ParserProductDetail>(`/parser/products/${id}`);
  return response.data;
}
