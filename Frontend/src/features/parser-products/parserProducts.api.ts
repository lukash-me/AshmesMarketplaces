import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  ParserProductDetail,
  ParserProductListItem,
  ParserProductListParams
} from './parserProducts.types';

export async function getParserProducts(
  params: ParserProductListParams
): Promise<PagedResponse<ParserProductListItem>> {
  const response = await http.get<PagedResponse<ParserProductListItem>>('/parser/products', { params });
  return response.data;
}

export async function getParserProduct(id: string): Promise<ParserProductDetail> {
  const response = await http.get<ParserProductDetail>(`/parser/products/${id}`);
  return response.data;
}
