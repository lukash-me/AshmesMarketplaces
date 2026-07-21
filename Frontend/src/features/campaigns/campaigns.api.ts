import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type {
  CampaignDetail,
  CampaignListItem,
  CampaignListParams,
  CampaignMetricListItem,
  CampaignMetricListParams
} from './campaigns.types';

export async function getCampaigns(
  params: CampaignListParams
): Promise<PagedResponse<CampaignListItem>> {
  const response = await http.get<PagedResponse<CampaignListItem>>('/campaigns', { params });
  return response.data;
}

export async function getCampaign(id: string): Promise<CampaignDetail> {
  const response = await http.get<CampaignDetail>(`/campaigns/${id}`);
  return response.data;
}

export async function getCampaignMetrics(
  params: CampaignMetricListParams
): Promise<PagedResponse<CampaignMetricListItem>> {
  const response = await http.get<PagedResponse<CampaignMetricListItem>>('/campaign-metrics', {
    params
  });
  return response.data;
}
