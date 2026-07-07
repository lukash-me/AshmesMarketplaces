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

export interface PublicMarketIntelligenceAvailableContext {
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  query: string;
  sourceRegionDest: string | null;
  sort: string | null;
  topN: number;
  sampleSize: number;
  calculatedAtUtc: string | null;
  latestObservedAtUtc: string | null;
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
  marketConcentration: MarketConcentration;
  priceQualityMap: PriceQualityMap;
  priceCorridors: PriceCorridors;
  limitations: string[];
}

export interface PublicMarketConcentrationSnapshot {
  context: PublicMarketContext;
  observationWindow: ObservationWindow;
  marketConcentration: MarketConcentration;
  priceQualityMap: PriceQualityMap;
  sampleSize: number;
  calculatedAtUtc: string | null;
  latestObservedAtUtc: string | null;
  status: string;
  limitations: string[];
}

export interface PublicTopForecastParams extends PublicMarketIntelligenceParams {
  page?: number;
  pageSize?: number;
  minProbability?: number;
}

export interface PublicTopForecastResponse {
  context: PublicMarketContext;
  run: PublicTopForecastRun | null;
  items: PublicTopForecastItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  limitations: string[];
}

export interface PublicTopForecastRun {
  id: string;
  modelVersion: string;
  modelArtifactId: string;
  trainedAtUtc: string;
  calculatedAtUtc: string;
  sampleSize: number;
  trainingSampleSize: number;
  validationSampleSize: number;
  testSampleSize: number;
  positiveCount: number;
  predictionsCount: number;
  minProbability: number;
  metrics: unknown;
  featureSchema: unknown;
  warnings: string[];
}

export interface PublicTopForecastItem {
  wbProductId: string;
  wbRootId: string | null;
  productRowId: string | null;
  name: string | null;
  image: string | null;
  price: number | null;
  rating: number | null;
  feedbackCount: number | null;
  stock: number | null;
  currentPosition: number | null;
  currentPositionState: string | null;
  observedRangeLimit: number | null;
  predictedPosition: number | null;
  top100Probability: number;
  confidence: number;
  sellerName: string | null;
  brandName: string | null;
  featureCoveragePercent: number;
  reasons: string[];
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
  position: number | null;
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
  position: number | null;
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

export interface MarketConcentration {
  sampleSize: number;
  uniqueSellersCount: number;
  uniqueBrandsCount: number;
  top3SellersSharePercent: number;
  top5SellersSharePercent: number;
  hhi: number;
  normalizedConcentrationScore: number;
  sellerLeaders: MarketConcentrationLeader[];
  brandLeaders: MarketConcentrationLeader[];
  rootClusters: MarketConcentrationRootCluster[];
  rankings: MarketConcentrationRanking[];
  insight: string;
  limitations: string[];
}

export type MarketConcentrationRankingKey = 'count' | 'position' | 'reviews' | string;

export interface MarketConcentrationRanking {
  key: MarketConcentrationRankingKey;
  title: string;
  sampleSize: number;
  top3SellersSharePercent: number;
  top5SellersSharePercent: number;
  hhi: number;
  normalizedConcentrationScore: number;
  sellerLeaders: MarketConcentrationLeader[];
  brandLeaders: MarketConcentrationLeader[];
  rootClusters: MarketConcentrationRootCluster[];
  insight: string;
}

export interface MarketConcentrationLeader {
  name: string;
  slotsCount: number;
  sharePercent: number;
  bestPosition: number | null;
  feedbackCount: number;
  rankedSlotsCount: number;
  top100SharePercent: number;
  feedbackSharePercent: number;
}

export interface MarketConcentrationRootCluster {
  wbRootId: string;
  productCount: number;
  bestPosition: number | null;
  sharePercent: number;
  feedbackCount: number;
  rankedSlotsCount: number;
  top100SharePercent: number;
  feedbackSharePercent: number;
}

export interface PriceQualityMap {
  points: PriceQualityPoint[];
  summary: PriceQualityMapSummary;
  limitations: string[];
}

export type QualityBucket = 'strong' | 'medium' | 'weak' | 'unknown';
export type DeliveryBucket = 'fast' | 'medium' | 'slow' | 'unknown';

export interface PriceQualityPoint {
  wbProductId: string;
  wbRootId: string | null;
  productRowId: string | null;
  productName: string | null;
  thumbnailUrl: string | null;
  price: number | null;
  rating: number | null;
  feedbackCount: number | null;
  stock: number | null;
  position: number | null;
  sellerName: string | null;
  brandName: string | null;
  qualityBucket: QualityBucket;
  qualityReasons: string[];
  deliveryBucket: DeliveryBucket;
}

export interface PriceQualityMapSummary {
  totalPoints: number;
  withoutRating: number;
  strongCount: number;
  mediumCount: number;
  weakCount: number;
  unknownCount: number;
  medianPrice: number | null;
  medianRating: number | null;
  insight: string;
}

export interface PriceCorridors {
  sampleSize: number;
  min: number | null;
  p25: number | null;
  median: number | null;
  p75: number | null;
  p90: number | null;
  max: number | null;
  average: number | null;
  top10Median: number | null;
  top50Median: number | null;
  top100Median: number | null;
  segments: PriceCorridorSegment[];
  insight: string;
  limitations: string[];
}

export interface PriceCorridorSegment {
  key: 'lower' | 'mass' | 'premium' | string;
  title: string;
  fromPrice: number | null;
  toPrice: number | null;
  productsCount: number;
  sharePercent: number;
  medianRating: number | null;
}
