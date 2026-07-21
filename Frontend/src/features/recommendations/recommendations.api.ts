import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  RecommendationCategoryListItem,
  RecommendationCategoryListParams,
  RecommendationDetail,
  RecommendationListItem,
  RecommendationListParams,
  RecommendationProductListItem,
  RecommendationProductListParams
} from './recommendations.types';

export async function getRecommendations(
  params: RecommendationListParams
): Promise<PagedResponse<RecommendationListItem>> {
  const response = await http.get<PagedResponse<RecommendationListItem>>('/recommendations', {
    params
  });
  return response.data;
}

export async function getRecommendation(id: string): Promise<RecommendationDetail> {
  const response = await http.get<RecommendationDetail>(`/recommendations/${id}`);
  return response.data;
}

export async function getRecommendationProducts(
  params: RecommendationProductListParams
): Promise<PagedResponse<RecommendationProductListItem>> {
  const response = await http.get<PagedResponse<RecommendationProductListItem>>(
    '/recommendation-products',
    { params }
  );
  return response.data;
}

export async function getRecommendationCategories(
  params: RecommendationCategoryListParams
): Promise<PagedResponse<RecommendationCategoryListItem>> {
  const response = await http.get<PagedResponse<RecommendationCategoryListItem>>(
    '/recommendation-categories',
    { params }
  );
  return response.data;
}
