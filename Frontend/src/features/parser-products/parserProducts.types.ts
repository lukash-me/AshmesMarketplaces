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
  logisticsDetail?: ParserProductLogisticsDetail | null;
}

export interface ParserProductLogisticsSummary {
  totalQuantityObserved: number | null;
  quantityIsCapped: boolean | null;
  quantityCapObserved: number | null;
  totalQuantityLabel: string;
  quantitySemantics: string | null;
  warehouseCount: number;
  destination: string | null;
  latestLogisticsRunId: string | null;
  observedAtUtc: string | null;
  hasWarehouseRows: boolean;
}

export interface ParserProductLogisticsDetail {
  summary: ParserProductLogisticsSummary | null;
  productWhRaw: string | null;
  productTime1Raw: number | null;
  productTime2Raw: number | null;
  productDtypeRaw: number | null;
  productDistRaw: number | null;
  warehouseRows: ParserWarehouseAvailability[];
}

export interface ParserWarehouseAvailability {
  warehouseIdOnMp: string | null;
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
}

export interface ParserProductListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  parserRunId?: string;
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

export interface ParserProductFilterOptionsParams {
  parserRunId?: string;
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
