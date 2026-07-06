export interface ParserProductListItem {
  id: string;
  parserRunId: string;
  parsedAtUtc: string;
  wbProductId: string;
  wbRootId: string | null;
  name: string;
  brandName: string | null;
  sellerName: string | null;
  priceRegular: number | null;
  priceDiscounted: number | null;
  priceWbWallet: number | null;
  discountPercent: number | null;
  totalQuantity: number | null;
  ratingRounded: number | null;
  reviewRating: number | null;
  feedbackCount: number | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceQuery: string | null;
  thumbnailUrl: string | null;
  rank?: ParserProductRankSummary | null;
  position?: ParserProductPosition | null;
  parsedReviewEvidence?: ParserProductReviewEvidence;
  logistics?: ParserProductLogisticsSummary | null;
}

export interface ParserProductDetail extends Omit<ParserProductListItem, 'thumbnailUrl'> {
  marketplace: string;
  skuProduct: string | null;
  entity: string | null;
  brandIdOnMp: number | null;
  sellerIdOnMp: number | null;
  feedbackCountSource: string | null;
  imageUrls: string[];
  imageCount: number | null;
  subjectParentId: number | null;
  subjectId: number | null;
  sourceRegionDest: string | null;
  sourceFileKind: string;
  sourceFileSha256: string;
  sourceLineNumber: number;
  rowHash: string;
  description: string | null;
  characteristics: Record<string, unknown> | unknown[] | null;
  groupedOptions: Record<string, unknown> | unknown[] | null;
  visualAnalysis: ParserProductVisualAnalysis | null;
  logisticsDetail?: ParserProductLogisticsDetail | null;
  deliveryProfile?: ParserProductDeliveryProfile | null;
}

export interface ParserProductVisualAnalysis {
  state: string;
  facts: string[];
}

export interface ParserProductLogisticsSummary {
  totalQuantityObserved: number | null;
  quantityIsCapped: boolean | null;
  quantityCapObserved: number | null;
  totalQuantityLabel: string;
  quantitySemantics: string | null;
  warehouseCount: number;
  destination: string | null;
  deliveryProfileKey: string | null;
  deliveryDestinationName: string | null;
  deliveryProfileVersion: string | null;
  deliveryDestinationCity: string | null;
  deliveryDestinationLabel: string | null;
  deliveryDestinationAddress: string | null;
  deliveryDestinationLatitude: number | null;
  deliveryDestinationLongitude: number | null;
  latestLogisticsRunId: string | null;
  observedAtUtc: string | null;
  hasWarehouseRows: boolean;
}

export interface ParserProductLogisticsDetail {
  summary: ParserProductLogisticsSummary | null;
  productWhRaw: string | null;
  deliveryProfileKey: string | null;
  deliveryDestinationName: string | null;
  deliveryProfileVersion: string | null;
  deliveryDestinationCity: string | null;
  deliveryDestinationLabel: string | null;
  deliveryDestinationAddress: string | null;
  deliveryDestinationLatitude: number | null;
  deliveryDestinationLongitude: number | null;
  productTime1Raw: number | null;
  productTime2Raw: number | null;
  productDtypeRaw: number | null;
  productDistRaw: number | null;
  warehouseRows: ParserWarehouseAvailability[];
}

export interface ParserProductDeliveryProfile {
  latestLogisticsRunId: string | null;
  destinationCount: number;
  warehouseSourceCount: number;
  bestProductTime1Raw: number | null;
  worstProductTime1Raw: number | null;
  productTime1SpreadRaw: number | null;
  locationEstimate: ParserProductDeliveryLocationEstimate | null;
  destinations: ParserProductDeliveryDestinationSignal[];
}

export interface ParserProductDeliveryLocationEstimate {
  status: string;
  zoneKey: string | null;
  zoneTitle: string | null;
  confidence: string | null;
  nearestDestinationName: string | null;
  nearestDeliveryLabel: string | null;
  nearestDeliveryHours: number | null;
  secondDestinationName: string | null;
  secondDeliveryHours: number | null;
  farthestDestinationName: string | null;
  deliverySpreadHours: number | null;
  evidence: string[];
}

export interface ParserProductDeliveryDestinationSignal {
  destination: string;
  deliveryProfileKey: string | null;
  deliveryDestinationName: string | null;
  deliveryProfileVersion: string | null;
  deliveryDestinationCity: string | null;
  deliveryDestinationLabel: string | null;
  deliveryDestinationAddress: string | null;
  deliveryDestinationLatitude: number | null;
  deliveryDestinationLongitude: number | null;
  totalQuantityObserved: number | null;
  warehouseCount: number;
  productTime1Raw: number | null;
  productTime2Raw: number | null;
  productDistRaw: number | null;
  visibleDeliveryStatus: string | null;
  visibleDeliveryLabel: string | null;
  visibleDeliveryDate: string | null;
  visibleDeliverySource: string | null;
  visibleDeliveryObservedAtUtc: string | null;
  visibleDeliveryRawPayload: Record<string, unknown> | null;
  observedAtUtc: string | null;
}

export interface ParserWarehouseAvailability {
  warehouseIdOnMp: string | null;
  deliveryProfileKey: string | null;
  deliveryDestinationName: string | null;
  deliveryProfileVersion: string | null;
  deliveryDestinationCity: string | null;
  deliveryDestinationLabel: string | null;
  deliveryDestinationAddress: string | null;
  deliveryDestinationLatitude: number | null;
  deliveryDestinationLongitude: number | null;
  optionId: string | null;
  sizeName: string | null;
  sizeOrigName: string | null;
  quantityObserved: number | null;
  quantityIsCapped: boolean | null;
  quantityCapObserved: number | null;
  quantitySemantics: string | null;
  stockPriorityRaw: number | null;
  stockTime1Raw: number | null;
  stockTime2Raw: number | null;
  stockDtypeRaw: number | null;
  stockDistRaw: number | null;
  priceBasic: number | null;
  priceProduct: number | null;
  priceLogisticsRaw: number | null;
  priceReturnRaw: number | null;
}

export interface ParserProductLogisticsSummaryParams {
  parserRunId?: string;
  includeTestRuns?: boolean;
  testRunsOnly?: boolean;
  testLabel?: string;
  search?: string;
  sourceCategory?: string;
  sourceSubcategory?: string;
  brandName?: string;
  sellerName?: string;
  wbRootId?: string;
}

export interface ParserProductQuantityBuckets {
  zero: number;
  oneToFive: number;
  sixToTwenty: number;
  twentyOneToThirtyNine: number;
  fortyPlusOrHigh: number;
  unknown: number;
}

export interface ParserProductLogisticsDestinationSummary {
  destination: string | null;
  deliveryProfileKey: string | null;
  deliveryDestinationName: string | null;
  deliveryProfileVersion: string | null;
  deliveryDestinationCity: string | null;
  deliveryDestinationLabel: string | null;
  deliveryDestinationAddress: string | null;
  deliveryDestinationLatitude: number | null;
  deliveryDestinationLongitude: number | null;
  productsWithLogistics: number;
  latestObservedAtUtc: string | null;
}

export interface ParserProductLogisticsSummaryAggregate {
  productRunId: string | null;
  logisticsRunId: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  productsTotal: number;
  productsWithLogistics: number;
  productsWithoutLogistics: number;
  productsWithQuantity: number;
  productsWithoutQuantity: number;
  quantityMin: number | null;
  quantityMax: number | null;
  quantityAverage: number | null;
  quantityMedian: number | null;
  quantityBuckets: ParserProductQuantityBuckets;
  productsWithWarehouseRows: number;
  warehouseRowsTotal: number;
  distinctWarehouseIds: number;
  averageWarehousesPerProduct: number;
  destinations: ParserProductLogisticsDestinationSummary[];
  latestObservedAtUtc: string | null;
  warnings: string[];
}

export interface ParserProductRankSummary {
  absolutePosition: number;
  page: number;
  positionOnPage: number;
  query: string;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceRegionDest: string | null;
  sort: string | null;
  observedAtUtc: string;
  parserRunId: string;
  rankContextId: string;
  contextsCount: number;
}

export type ParserProductPositionState = 'observed' | 'beyondObservedRange' | 'unknown';

export interface ParserProductPosition {
  state: ParserProductPositionState;
  absolutePosition: number | null;
  observedRangeLimit: number | null;
  query: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  observedAtUtc: string | null;
}

export interface ParserProductReviewEvidence {
  rootFetchCount: number;
  parsedReviewCount: number;
  parsedReplyCount: number;
  latestReviewRunId: string | null;
  attributionMode: string;
  isRootScoped: boolean;
  isFullHistoryUnknown: boolean;
  hasCappedRootPayload: boolean;
  marketplaceFeedbackCount: number | null;
  fetchedReviewsCount: number;
  oldestReviewDateUtc: string | null;
  latestReviewDateUtc: string | null;
  coverageStatus: string;
  coverageSource: string;
  lastCoverageError: string | null;
}

export interface ParserProductListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  parserRunId?: string;
  includeTestRuns?: boolean;
  testRunsOnly?: boolean;
  testLabel?: string;
  requireDeliveryProfile?: boolean;
  sourceCategory?: string;
  sourceSubcategory?: string;
  brandName?: string;
  sellerName?: string;
  wbRootId?: string;
  priceDiscountedFrom?: number;
  priceDiscountedTo?: number;
  reviewRatingFrom?: number;
  reviewRatingTo?: number;
  feedbackCountFrom?: number;
  feedbackCountTo?: number;
}

export interface ParserProductFilterOptions {
  categories: string[];
  subcategories: string[];
  brands: string[];
  sellers: string[];
}

export interface ParserDemoCardSubcategories {
  category: string;
  subcategories: string[];
}

export interface ParserDemoCardCharacteristics {
  subcategory: string;
  characteristics: string[];
}

export interface ParserDemoCardOptions {
  categories: string[];
  subcategoriesByCategory: ParserDemoCardSubcategories[];
  characteristicsBySubcategory: ParserDemoCardCharacteristics[];
}

export interface ParserProductFilterOptionsParams {
  parserRunId?: string;
  includeTestRuns?: boolean;
  testRunsOnly?: boolean;
  testLabel?: string;
  search?: string;
  sourceCategory?: string;
  sourceSubcategory?: string;
  brandName?: string;
  sellerName?: string;
}

export type ParserProductQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  search: string;
  parserRunId: string;
  sourceCategory: string;
  sourceSubcategory: string;
  brandName: string;
  sellerName: string;
  wbRootId: string;
  priceDiscountedFrom: string;
  priceDiscountedTo: string;
  reviewRatingFrom: string;
  reviewRatingTo: string;
  feedbackCountFrom: string;
  feedbackCountTo: string;
};
