export interface PublicMarketIntelligenceParams {
  sourceCategory?: string;
  sourceSubcategory?: string;
  query?: string;
  sourceRegionDest?: string;
  sort?: string;
  topN?: number;
  latestRankRunId?: string;
  baselineRankRunId?: string;
  latestProductRunId?: string;
  baselineProductRunId?: string;
}

export interface PublicMarketIntelligence {
  context: PublicMarketContext;
  observationWindow: ObservationWindow;
  events: MarketEvent[];
  competitorWeaknesses: CompetitorWeakness[];
  promoPressure: PromoPressure;
  pricePressure: PricePressure;
  stockPressure: StockPressure;
  concentration: ConcentrationSummary;
  limitations: string[];
}

export interface PublicMarketContext {
  marketplace: string;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  query: string;
  sourceRegionDest: string | null;
  sort: string | null;
  topN: number;
}

export interface ObservationWindow {
  latestRankRunId: string | null;
  baselineRankRunId: string | null;
  latestProductRunId: string | null;
  baselineProductRunId: string | null;
  latestObservedAtUtc: string | null;
  baselineObservedAtUtc: string | null;
  isComparable: boolean;
  coverageStatus: string;
  limitations: string[];
}

export interface MarketEvent {
  type: string;
  group: string;
  severity: string;
  title: string;
  description: string;
  wbProductId: string;
  wbRootId: string | null;
  productName: string | null;
  brandName: string | null;
  sellerName: string | null;
  productRowId: string | null;
  thumbnailUrl: string | null;
  beforeValue: string | null;
  afterValue: string | null;
  observedAtUtc: string;
  confidence: number;
  limitations: string[];
}

export interface CompetitorWeakness {
  type: string;
  severity: string;
  title: string;
  description: string;
  wbProductId: string;
  wbRootId: string | null;
  productName: string | null;
  brandName: string | null;
  sellerName: string | null;
  productRowId: string | null;
  thumbnailUrl: string | null;
  position: number;
  metricValue: string | null;
  referenceValue: string | null;
  explanation: string;
  limitations: string[];
}

export interface PressureSummary {
  type: string;
  title: string;
  description: string;
  currentValue: number | null;
  baselineValue: number | null;
  delta: number | null;
  unit: string;
  limitations: string[];
}

export interface PromoPressure {
  summaries: PressureSummary[];
  limitations: string[];
}

export interface PricePressure {
  summaries: PressureSummary[];
  highPriceVisibleProducts: HighPriceVisibleProduct[];
  limitations: string[];
}

export interface HighPriceVisibleProduct {
  wbProductId: string;
  wbRootId: string | null;
  productName: string | null;
  brandName: string | null;
  sellerName: string | null;
  productRowId: string | null;
  thumbnailUrl: string | null;
  position: number;
  currentPrice: number | null;
  referencePrice: number | null;
  explanation: string;
  limitations: string[];
}

export type StockStatus = 'exact' | 'capped' | 'unknown';

export interface StockPressure {
  exactZeroCount: number;
  exactLowStockCount: number;
  cappedCount: number;
  unknownCount: number;
  statusBreakdown: StockStatusSummary[];
  highRankLowStockProducts: HighRankLowStockProduct[];
  limitations: string[];
}

export interface StockStatusSummary {
  status: StockStatus;
  count: number;
  example: StockValue;
}

export interface HighRankLowStockProduct {
  wbProductId: string;
  wbRootId: string | null;
  productName: string | null;
  brandName: string | null;
  sellerName: string | null;
  productRowId: string | null;
  thumbnailUrl: string | null;
  position: number;
  stock: StockValue;
  explanation: string;
  limitations: string[];
}

export interface StockValue {
  status: StockStatus;
  value: number | null;
  displayValue: string;
}

export interface ConcentrationSummary {
  sellerLeaders: ConcentrationLeader[];
  brandLeaders: ConcentrationLeader[];
  rootClusters: RootCluster[];
  limitations: string[];
}

export interface ConcentrationLeader {
  name: string;
  slotsCount: number;
  sharePercent: number;
  description: string;
}

export interface RootCluster {
  wbRootId: string;
  productCount: number;
  bestPosition: number;
  description: string;
}
