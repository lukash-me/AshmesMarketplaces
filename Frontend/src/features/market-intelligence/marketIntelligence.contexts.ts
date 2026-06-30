import type { PublicMarketIntelligenceParams } from './marketIntelligence.types';

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
  context: MarketIntelligenceContext,
  overrides: Partial<PublicMarketIntelligenceParams> = {}
): PublicMarketIntelligenceParams {
  return {
    sourceCategory: context.sourceCategory,
    sourceSubcategory: context.sourceSubcategory,
    query: context.query,
    sourceRegionDest: marketIntelligenceDefaultRegionDest,
    sort: marketIntelligenceDefaultSort,
    topN: marketIntelligenceTopN,
    ...overrides
  };
}
