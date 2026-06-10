export interface HotProductsListParams {
  sourceCategory?: string;
  sourceSubcategory?: string;
  groupKey?: string;
  wbProductId?: string;
  wbRootId?: string;
  page?: number;
  pageSize?: number;
}

export interface RecalculateHotProductsRequest {
  sourceCategory?: string;
  sourceSubcategory?: string;
  productParserRunId?: string;
  rankParserRunId?: string;
  maxProducts?: number;
  forceRecalculate?: boolean;
  maxRecommendations?: number;
  minConfidence?: number;
  minProductsForScoring?: number;
}

export interface RecalculateHotProductsResponse {
  runId: string;
  status: string;
  algorithm: string;
  algorithmVersion: string;
  modelVersion: string;
  inputSnapshotHash: string;
  productParserRunId: string | null;
  rankParserRunId: string | null;
  productCountSent: number;
  recommendationsCount: number;
  warningCount: number;
  validUntilUtc: string | null;
  createdAtUtc: string;
  warnings: string[];
}

export interface HotProductsListResponse {
  run: HotProductsRunSummary | null;
  page: number;
  pageSize: number;
  totalCount: number;
  items: HotProductRecommendationItem[];
  groups: HotProductsGroup[];
}

export interface HotProductsGroup {
  key: string;
  title: string;
  description: string;
  totalCount: number;
  items: HotProductRecommendationItem[];
  clusters: HotProductsDuplicateCluster[];
}

export interface HotProductsDuplicateCluster {
  key: string;
  title: string;
  totalCount: number;
  items: HotProductRecommendationItem[];
}

export interface HotProductsRunSummary {
  runId: string;
  computedAtUtc: string;
  validUntilUtc: string | null;
  algorithm: string;
  algorithmVersion: string;
  itemsTotal: number;
  warningCount: number;
}

export interface HotProductRecommendationItem {
  id: string;
  rankOrder: number;
  productName: string;
  thumbnailUrl: string | null;
  wbProductId: string | null;
  wbRootId: string | null;
  parserProductRowId: string | null;
  brandName: string | null;
  sellerName: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  price: number | null;
  priceWithoutDiscount: number | null;
  walletPrice: number | null;
  rating: number | null;
  feedbackCount: number | null;
  parsedReviewCount: number | null;
  parsedReplyCount: number | null;
  position: number | null;
  positionState: string | null;
  observedRangeLimit: number | null;
  totalQuantity: number | null;
  score: number;
  confidence: number;
  title: string;
  reason: string;
  factors: HotProductRecommendationFactor[];
  validUntilUtc: string | null;
}

export interface HotProductRecommendationFactor {
  code: string;
  label: string;
  value: string | number | boolean | null;
  weight: number;
  direction: 'positive' | 'negative' | 'neutral' | string;
}
