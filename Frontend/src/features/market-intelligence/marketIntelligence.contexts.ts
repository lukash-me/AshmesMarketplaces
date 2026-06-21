import type { PublicMarketIntelligenceParams } from './marketIntelligence.types';

export const marketIntelligenceDefaultRegionDest = '12354108';
export const marketIntelligenceDefaultSort = 'popular';
export const marketIntelligenceTopN = 1000;

export const marketIntelligenceContexts = [
  {
    label: 'Светильники бра',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Светильники бра',
    query: 'Светильники бра'
  },
  {
    label: 'Коврики для ванной',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Коврики для ванной',
    query: 'Коврики для ванной'
  },
  {
    label: 'Органайзеры для хранения вещей',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Органайзеры для хранения вещей',
    query: 'Органайзеры для хранения вещей'
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
