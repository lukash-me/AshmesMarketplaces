import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  ParserTestingListParams,
  ParserTestingLogisticsSummary,
  ParserTestingLogisticsSummaryParams,
  ParserTestingProduct,
  ParserTestingProductDetail
} from './parserTesting.types';

export async function getParserTestingProducts(
  params: ParserTestingListParams
): Promise<PagedResponse<ParserTestingProduct>> {
  const response = await http.get<PagedResponse<ParserTestingProduct>>('/parser/products', { params });
  return response.data;
}

export async function getParserTestingProduct(id: string): Promise<ParserTestingProductDetail> {
  const response = await http.get<ParserTestingProductDetail>(`/parser/products/${id}`);
  return response.data;
}

export async function getParserTestingLogisticsSummary(
  params: ParserTestingLogisticsSummaryParams
): Promise<ParserTestingLogisticsSummary> {
  const response = await http.get<ParserTestingLogisticsSummary>(
    '/parser/products/logistics-summary',
    { params }
  );
  return response.data;
}
