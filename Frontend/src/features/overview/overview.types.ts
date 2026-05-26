import type { PublicMarketIntelligence } from '@/features/market-intelligence/marketIntelligence.types';
import type {
  ObservedMarketEventResponse,
  ObservedStockDecreaseResponse
} from '@/features/orders/orders.types';
import type {
  ParserProductFilterOptions,
  ParserProductListItem,
  ParserProductLogisticsSummaryAggregate
} from '@/features/parser-products/parserProducts.types';
import type { PagedResponse } from '@/entities/pagination';

export type OverviewCardStatus = 'loading' | 'ready' | 'empty' | 'unavailable';

export interface OverviewMetric {
  label: string;
  value: string;
  caption?: string;
  key?: string;
  numericValue?: number | null;
  suffix?: string;
  fractionDigits?: number;
  animate?: boolean;
}

export interface OverviewMarketContext {
  sourceSubcategory: string;
}

export type OverviewResource<T> =
  | {
      status: 'ready';
      data: T;
    }
  | {
      status: 'unavailable';
      data: null;
    };

export interface OverviewMarketProducts {
  totalCount: number;
  latestProduct: ParserProductListItem | null;
}

export interface OverviewWorkspaceCount {
  totalCount: number;
}

export interface OverviewObservedEvents {
  mode: 'events' | 'stock-decreases';
  totalCount: number;
  currentObservedAtUtc: string | null;
  previousObservedAtUtc: string | null;
  warnings: string[];
  summary: {
    newProducts: number | null;
    stockDecreased: number;
    stockIncreased: number | null;
    missingProducts: number | null;
    comparedProducts: number | null;
    totalObservedDecrease: number | null;
  };
}

export interface OverviewData {
  marketProducts: OverviewResource<OverviewMarketProducts>;
  logisticsSummary: OverviewResource<ParserProductLogisticsSummaryAggregate>;
  observedStockDecreases: OverviewResource<ObservedStockDecreaseResponse>;
  observedEvents: OverviewResource<OverviewObservedEvents>;
  marketIntelligence: OverviewResource<PublicMarketIntelligence>;
  expenses: OverviewResource<OverviewWorkspaceCount>;
}

export type OverviewPagedCountSource = PagedResponse<unknown>;
export type OverviewFilterOptions = ParserProductFilterOptions;
export type OverviewObservedEventResponseSource = ObservedMarketEventResponse | ObservedStockDecreaseResponse;
