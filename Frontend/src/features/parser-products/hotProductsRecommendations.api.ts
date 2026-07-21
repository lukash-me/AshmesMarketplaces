import { http, publicHttp } from '@/shared/api/http';

import type {
  HotProductsListParams,
  HotProductsListResponse,
  RecalculateHotProductsRequest,
  RecalculateHotProductsResponse
} from './hotProductsRecommendations.types';

export async function getHotProductsRecommendations(
  params: HotProductsListParams
): Promise<HotProductsListResponse> {
  const searchParams = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    if (Array.isArray(value)) {
      value.forEach((item) => {
        if (item) {
          searchParams.append(key, String(item));
        }
      });
      return;
    }

    searchParams.append(key, String(value));
  });

  const response = await publicHttp.get<HotProductsListResponse>('/market/recommendations/hot-products', {
    params: searchParams
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
