import { getPublicMarketIntelligence } from '@/features/market-intelligence/marketIntelligence.api';
import { getObservedMarketEvents, getObservedStockDecreases } from '@/features/orders/orders.api';
import { getParserProductFilterOptions, getParserProductLogisticsSummary, getParserProducts } from '@/features/parser-products/parserProducts.api';
import { getExpenses } from '@/features/expenses/expenses.api';

import type {
  OverviewData,
  OverviewFilterOptions,
  OverviewMarketContext,
  OverviewMarketProducts,
  OverviewObservedEvents,
  OverviewPagedCountSource,
  OverviewResource,
  OverviewWorkspaceCount
} from './overview.types';

const MARKET_INTELLIGENCE_DEFAULT_PARAMS = {
  sourceCategory: 'Товары для дома',
  sourceSubcategory: 'Светильники бра',
  query: 'Светильники бра',
  sourceRegionDest: '12354108',
  sort: 'popular',
  topN: 100
} as const;

export const OVERVIEW_DEFAULT_SOURCE_CATEGORY = MARKET_INTELLIGENCE_DEFAULT_PARAMS.sourceCategory;
export const OVERVIEW_DEFAULT_SUBCATEGORY = MARKET_INTELLIGENCE_DEFAULT_PARAMS.sourceSubcategory;

export const MARKET_INTELLIGENCE_DEFAULT_LINK = {
  path: '/market/intelligence',
  query: {
    ...MARKET_INTELLIGENCE_DEFAULT_PARAMS,
    topN: String(MARKET_INTELLIGENCE_DEFAULT_PARAMS.topN),
    section: 'events'
  }
} as const;

export function marketIntelligenceLink(sourceSubcategory: string) {
  return {
    path: '/market/intelligence',
    query: {
      sourceCategory: OVERVIEW_DEFAULT_SOURCE_CATEGORY,
      sourceSubcategory,
      query: sourceSubcategory,
      sourceRegionDest: MARKET_INTELLIGENCE_DEFAULT_PARAMS.sourceRegionDest,
      sort: MARKET_INTELLIGENCE_DEFAULT_PARAMS.sort,
      topN: String(MARKET_INTELLIGENCE_DEFAULT_PARAMS.topN),
      section: 'events'
    }
  } as const;
}

export async function loadOverviewFilterOptions(): Promise<OverviewResource<OverviewFilterOptions>> {
  return toOverviewResource(getParserProductFilterOptions({}));
}

export async function loadOverviewData(context?: OverviewMarketContext): Promise<OverviewData> {
  const sourceSubcategory = context?.sourceSubcategory;
  const [
    marketProducts,
    logisticsSummary,
    observedStockDecreases,
    observedEvents,
    marketIntelligence,
    expenses
  ] = await Promise.all([
    toOverviewResource(loadMarketProducts(sourceSubcategory)),
    toOverviewResource(getParserProductLogisticsSummary({ ...(sourceSubcategory ? { sourceSubcategory } : {}) })),
    toOverviewResource(getObservedStockDecreases({
      page: 1,
      pageSize: 1,
      sort: '-decrease',
      minDecrease: 1,
      ...(sourceSubcategory ? { sourceSubcategory } : {})
    })),
    loadObservedEventsWithFallback(sourceSubcategory),
    toOverviewResource(getPublicMarketIntelligence({
      ...MARKET_INTELLIGENCE_DEFAULT_PARAMS,
      ...(sourceSubcategory
        ? {
            sourceSubcategory,
            query: sourceSubcategory
          }
        : {})
    })),
    toOverviewResource(loadPagedCount(() => getExpenses({ page: 1, pageSize: 1 })))
  ]);

  return {
    marketProducts,
    logisticsSummary,
    observedStockDecreases,
    observedEvents,
    marketIntelligence,
    expenses
  };
}

async function loadMarketProducts(sourceSubcategory?: string): Promise<OverviewMarketProducts> {
  const response = await getParserProducts({
    page: 1,
    pageSize: 1,
    sort: '-parsedAtUtc',
    ...(sourceSubcategory ? { sourceSubcategory } : {})
  });

  return {
    totalCount: response.totalCount,
    latestProduct: response.items[0] ?? null
  };
}

async function loadObservedEventsWithFallback(
  sourceSubcategory?: string
): Promise<OverviewResource<OverviewObservedEvents>> {
  try {
    const response = await getObservedMarketEvents({
      page: 1,
      pageSize: 1,
      ...(sourceSubcategory ? { sourceSubcategory } : {})
    });

    return {
      status: 'ready',
      data: {
        mode: 'events',
        totalCount: response.totalCount,
        currentObservedAtUtc: response.currentObservedAtUtc,
        previousObservedAtUtc: response.previousObservedAtUtc,
        warnings: response.warnings,
        summary: {
          newProducts: response.summary.newProductObservedCount,
          stockDecreased: response.summary.stockDecreasedCount,
          stockIncreased: response.summary.stockIncreasedCount,
          missingProducts: response.summary.productMissingInCurrentCount,
          comparedProducts: response.summary.comparedPairsCount,
          totalObservedDecrease: response.summary.totalObservedDecrease
        }
      }
    };
  } catch {
    try {
      const response = await getObservedStockDecreases({
        page: 1,
        pageSize: 1,
        sort: '-decrease',
        minDecrease: 1,
        ...(sourceSubcategory ? { sourceSubcategory } : {})
      });

      return {
        status: 'ready',
        data: {
          mode: 'stock-decreases',
          totalCount: response.totalCount,
          currentObservedAtUtc: response.currentObservedAtUtc,
          previousObservedAtUtc: response.previousObservedAtUtc,
          warnings: response.warnings,
          summary: {
            newProducts: null,
            stockDecreased: response.totalCount,
            stockIncreased: null,
            missingProducts: null,
            comparedProducts: response.summary.comparedProductsCount,
            totalObservedDecrease: response.summary.totalObservedDecrease
          }
        }
      };
    } catch {
      return {
        status: 'unavailable',
        data: null
      };
    }
  }
}

async function loadPagedCount(
  loader: () => Promise<OverviewPagedCountSource>
): Promise<OverviewWorkspaceCount> {
  const response = await loader();

  return {
    totalCount: response.totalCount
  };
}

async function toOverviewResource<T>(promise: Promise<T>): Promise<OverviewResource<T>> {
  try {
    return {
      status: 'ready',
      data: await promise
    };
  } catch {
    return {
      status: 'unavailable',
      data: null
    };
  }
}
