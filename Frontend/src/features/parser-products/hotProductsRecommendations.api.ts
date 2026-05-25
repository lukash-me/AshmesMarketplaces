import { http } from '@/shared/api/http';

import type {
  HotProductsListParams,
  HotProductsListResponse
} from './hotProductsRecommendations.types';

export async function getHotProductsRecommendations(
  params: HotProductsListParams
): Promise<HotProductsListResponse> {
  const response = await http.get<HotProductsListResponse>('/market/recommendations/hot-products', {
    params
  });
  return response.data;
}
