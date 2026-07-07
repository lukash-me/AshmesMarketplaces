import type { PublicMarketIntelligenceAvailableContext, PublicMarketIntelligenceParams } from './marketIntelligence.types';

export const marketIntelligenceDefaultRegionDest = '12354108';
export const marketIntelligenceDefaultSort = 'popular';
export const marketIntelligenceTopN = 1000;

export const marketIntelligenceContexts = [
  {
    label: 'Платья и сарафаны',
    sourceCategory: 'Женщинам',
    sourceSubcategory: 'Платья и сарафаны',
    query: 'menu_v3_8137 платье женские'
  },
  {
    label: 'Кеды и кроссовки',
    sourceCategory: 'Обувь',
    sourceSubcategory: 'Кеды и кроссовки',
    query: 'menu_redirect_subject_v2_8194 мужские кеды и кроссовки'
  },
  {
    label: 'Органическая косметика',
    sourceCategory: 'Красота',
    sourceSubcategory: 'Органическая косметика',
    query: 'menu_redirect_subject_v2_10012 органическая косметика'
  }
] as const;

export type MarketIntelligenceContext = typeof marketIntelligenceContexts[number];

export function resolveMarketIntelligenceContext(sourceSubcategory: string): MarketIntelligenceContext {
  return marketIntelligenceContexts.find((context) => context.sourceSubcategory === sourceSubcategory)
    ?? marketIntelligenceContexts[0];
}

export function isMarketIntelligenceSubcategory(value: unknown): value is string {
  return typeof value === 'string'
    && marketIntelligenceContexts.some((context) => context.sourceSubcategory === value);
}

export function buildMarketIntelligenceParams(
  context: MarketIntelligenceContext | PublicMarketIntelligenceAvailableContext,
  overrides: Partial<PublicMarketIntelligenceParams> = {}
): PublicMarketIntelligenceParams {
  const sourceRegionDest = 'sourceRegionDest' in context ? context.sourceRegionDest : undefined;
  const sort = 'sort' in context ? context.sort : undefined;
  const topN = 'topN' in context ? context.topN : undefined;

  return {
    sourceCategory: context.sourceCategory ?? undefined,
    sourceSubcategory: context.sourceSubcategory ?? undefined,
    query: context.query,
    sourceRegionDest: sourceRegionDest ?? marketIntelligenceDefaultRegionDest,
    sort: sort ?? marketIntelligenceDefaultSort,
    topN: topN ?? marketIntelligenceTopN,
    ...overrides
  };
}
