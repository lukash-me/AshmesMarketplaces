import { publicHttp } from '@/shared/api/http';

import type {
  PublicMarketIntelligenceAvailableContext,
  PublicMarketConcentrationSnapshot,
  PublicTopForecastParams,
  PublicTopForecastResponse,
  PriceQualityPoint,
  PublicMarketIntelligence,
  PublicMarketIntelligenceParams
} from './marketIntelligence.types';

export async function getPublicMarketIntelligence(
  params: PublicMarketIntelligenceParams
): Promise<PublicMarketIntelligence> {
  const response = await publicHttp.get<PublicMarketIntelligence>('/market-intelligence/public', { params });
  return response.data;
}

export async function getPublicMarketIntelligenceContexts(): Promise<PublicMarketIntelligenceAvailableContext[]> {
  const response = await publicHttp.get<PublicMarketIntelligenceAvailableContext[]>('/market-intelligence/public/contexts');
  return response.data;
}

export async function getPublicMarketConcentrationContexts(): Promise<PublicMarketIntelligenceAvailableContext[]> {
  const response = await publicHttp.get<PublicMarketIntelligenceAvailableContext[]>('/market-intelligence/public/concentration/contexts');
  return response.data;
}

export async function getPublicMarketConcentration(
  params: PublicMarketIntelligenceParams
): Promise<PublicMarketConcentrationSnapshot> {
  const response = await publicHttp.get<PublicMarketConcentrationSnapshot>('/market-intelligence/public/concentration', { params });
  return response.data;
}

export async function getPublicMarketConcentrationProducts(
  params: PublicMarketIntelligenceParams,
  kind: 'seller' | 'brand' | 'root',
  key: string
): Promise<PriceQualityPoint[]> {
  const response = await publicHttp.get<{ products: PriceQualityPoint[] }>('/market-intelligence/public/concentration/products', {
    params: {
      ...params,
      kind,
      key
    }
  });
  return response.data.products;
}

export async function getPublicTopForecast(
  params: PublicTopForecastParams
): Promise<PublicTopForecastResponse> {
  const response = await publicHttp.get<PublicTopForecastResponse>('/market-intelligence/public/top-forecast', { params });
  return response.data;
}
