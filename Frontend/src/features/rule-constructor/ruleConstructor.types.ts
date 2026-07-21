export type RuleConstructorFilterStatus = 'active' | 'experimental' | 'disabled';
export type RuleConstructorFilterTone = 'positive' | 'negative' | 'neutral';
export type RuleConstructorFilterVerificationStatus = 'not_ready' | 'needs_data_export' | 'ready';

export type RuleConstructorCombineMode = 'all' | 'any';

export type RuleGroupOperator = 'and' | 'or';

export type RuleExpression =
  | {
      kind: 'rule';
      ruleId: string;
      operator?: null;
      children?: null;
    }
  | {
      kind: 'group';
      operator: RuleGroupOperator;
      children: RuleExpression[];
      ruleId?: null;
    };

export interface RuleConstructorFilter {
  id: string;
  group: string;
  name: string;
  description: string;
  dataSources: string[];
  status: RuleConstructorFilterStatus;
  tone: RuleConstructorFilterTone;
  verificationStatus: RuleConstructorFilterVerificationStatus;
  requiresReviewByUser: boolean;
  unavailableReason: string | null;
}

export interface RuleConstructorSearchRequest {
  ruleIds: string[];
  combineMode: RuleConstructorCombineMode;
  sourceCategory?: string;
  sourceSubcategory?: string;
  search?: string;
  page: number;
  pageSize: number;
  expression?: RuleExpression | null;
}

export interface RuleConstructorCountsRequest {
  expression?: RuleExpression | null;
  sourceCategory?: string;
  sourceSubcategory?: string;
  search?: string;
  activeGroupPath?: number[];
  ruleIds?: string[];
  combineMode?: RuleConstructorCombineMode;
}

export interface RuleConstructorRuleCount {
  ruleId: string;
  count: number | null;
  available: boolean;
  alreadyUsed: boolean;
  unavailableReason: string | null;
}

export interface RuleConstructorCountsResponse {
  total: number;
  ruleCounts: RuleConstructorRuleCount[];
}

export interface RuleConstructorMatchedFact {
  id: string;
  name: string;
  description: string;
  tone: RuleConstructorFilterTone;
  value: string | null;
}

export interface RuleConstructorSearchItem {
  id: string;
  wbProductId: string;
  wbRootId: string | null;
  name: string;
  brandName: string | null;
  sellerName: string | null;
  priceDiscounted: number | null;
  totalQuantity: number | null;
  reviewRating: number | null;
  feedbackCount: number | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceQuery: string | null;
  thumbnailUrl: string | null;
  positionState: string | null;
  positionAbsolute: number | null;
  positionObservedRangeLimit: number | null;
  matchedFacts: RuleConstructorMatchedFact[];
}

export interface RuleConstructorSearchResponse {
  items: RuleConstructorSearchItem[];
  total: number;
  page: number;
  pageSize: number;
  appliedFilters: RuleConstructorFilter[];
}
