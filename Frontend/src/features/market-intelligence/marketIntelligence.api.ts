import { http } from '@/shared/api/http';

import type {
  PublicMarketIntelligence,
  PublicMarketIntelligenceParams
} from './marketIntelligence.types';

export async function getPublicMarketIntelligence(
  params: PublicMarketIntelligenceParams
): Promise<PublicMarketIntelligence> {
  const response = await http.get<PublicMarketIntelligence>('/market-intelligence/public', { params });
  return response.data;
}
