export type WorkspaceOverviewTagKey = 'competitor' | 'idea' | 'created';
export type WorkspaceOverviewGroupKey = WorkspaceOverviewTagKey | 'new';
export type WorkspaceOverviewChangeState = 'positive' | 'negative' | 'neutral' | 'unknown';

export interface WorkspaceOverviewRun {
  id: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  algorithm: string;
  algorithmVersion: string;
  modelVersion: string;
  warnings: string[];
  errorMessage: string | null;
}

export interface WorkspaceOverviewChange {
  key: string;
  label: string;
  currentValue: number | null;
  previousValue: number | null;
  delta: number | null;
  displayValue: string;
  state: WorkspaceOverviewChangeState;
}

export interface WorkspaceOverviewSignal {
  code: string;
  severity: 'high' | 'medium' | 'low' | string;
  title: string;
  description: string;
  metricFacts: string[];
  confidence: number;
  value?: Record<string, unknown> | null;
}

export interface WorkspaceOverviewSimilarProduct {
  productKey: string;
  parserProductRowId: string | null;
  wbProductId: string | null;
  wbRootId: string | null;
  name: string;
  brandName: string | null;
  sellerName: string | null;
  thumbnailUrl: string | null;
  sourceSubcategory: string | null;
  price: number | null;
  rating: number | null;
  feedbackCount: number | null;
  position: number | null;
  totalQuantity: number | null;
  similarityScore: number;
  reason: string;
}

export interface WorkspaceOverviewSimilarProductGroupItem {
  product: WorkspaceOverviewSimilarProduct;
  facts: string[];
  tags: string[];
}

export interface WorkspaceOverviewSimilarProductGroup {
  key: string;
  title: string;
  description: string;
  items: WorkspaceOverviewSimilarProductGroupItem[];
}

export interface WorkspaceOverviewProduct {
  id: string;
  parserProductRowId: string | null;
  wbProductId: string | null;
  wbRootId: string | null;
  sourceType: 'parser' | 'demo' | string;
  isDemo: boolean;
  tagKey: WorkspaceOverviewTagKey;
  note: string | null;
  name: string;
  brandName: string | null;
  sellerName: string | null;
  thumbnailUrl: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  currentPrice: number | null;
  costPrice: number | null;
  description: string | null;
  supplierName: string | null;
  supplierUrl: string | null;
  currentPosition: number | null;
  currentStock: number | null;
  currentFeedbackCount: number | null;
  currentReviewRating: number | null;
  latestObservedAtUtc: string | null;
  priceChange: WorkspaceOverviewChange;
  positionChange: WorkspaceOverviewChange;
  stockChange: WorkspaceOverviewChange;
  feedbackChange: WorkspaceOverviewChange;
  reviewRatingChange: WorkspaceOverviewChange;
  signals: WorkspaceOverviewSignal[];
  similarProducts: WorkspaceOverviewSimilarProduct[];
  similarProductGroups: WorkspaceOverviewSimilarProductGroup[];
}

export interface WorkspaceOverviewGroup {
  key: WorkspaceOverviewGroupKey;
  label: string;
  count: number;
  products: WorkspaceOverviewProduct[];
}

export interface WorkspaceOverview {
  lastAnalysis: WorkspaceOverviewRun | null;
  workspaceProductCount: number;
  signalCount: number;
  similarProductCount: number;
  newItems: WorkspaceOverviewGroup;
  competitors: WorkspaceOverviewGroup;
  ideas: WorkspaceOverviewGroup;
  created: WorkspaceOverviewGroup;
}

export interface WorkspaceOverviewRecalculateResponse {
  run: WorkspaceOverviewRun;
  productCount: number;
  signalCount: number;
  similarProductCount: number;
  warnings: string[];
}

export interface WorkspaceOverviewMarkViewedRequest {
  productIds: string[];
}

export interface WorkspaceOverviewMarkViewedResponse {
  updatedCount: number;
  viewedAtUtc: string;
}
