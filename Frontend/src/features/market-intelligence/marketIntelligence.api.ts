import { publicHttp } from '@/shared/api/http';

import type {
  PublicMarketIntelligence,
  PublicMarketIntelligenceParams
} from './marketIntelligence.types';

export async function getPublicMarketIntelligence(
  params: PublicMarketIntelligenceParams
): Promise<PublicMarketIntelligence> {
  const response = await publicHttp.get<PublicMarketIntelligence>('/market-intelligence/public', { params });
  return response.data;
}
