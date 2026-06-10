import { http } from '@/shared/api/http';

import type {
  HotProductsListParams,
  HotProductsListResponse,
  RecalculateHotProductsRequest,
  RecalculateHotProductsResponse
} from './hotProductsRecommendations.types';

export async function getHotProductsRecommendations(
  params: HotProductsListParams
): Promise<HotProductsListResponse> {
  const response = await http.get<HotProductsListResponse>('/market/recommendations/hot-products', {
    params
  });
  return response.data;
}

export async function recalculateHotProductsRecommendations(
  payload: RecalculateHotProductsRequest
): Promise<RecalculateHotProductsResponse> {
  const response = await http.post<RecalculateHotProductsResponse>(
    '/market/recommendations/hot-products/recalculate',
    payload
  );
  return response.data;
}
