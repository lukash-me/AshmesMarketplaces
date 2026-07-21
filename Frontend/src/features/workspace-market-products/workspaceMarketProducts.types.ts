export type WorkspaceMarketProductTagKey = 'competitor' | 'idea' | 'created';

export interface WorkspaceMarketProductListItem {
  id: string;
  idWorkspace: string;
  parserProductRowId: string | null;
  wbProductId: string | null;
  wbRootId: string | null;
  sourceType: 'parser' | 'demo' | string;
  isDemo: boolean;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceRegionDest: string | null;
  sourceQuery: string | null;
  tagKey: WorkspaceMarketProductTagKey;
  note: string | null;
  name: string;
  brandName: string | null;
  sellerName: string | null;
  thumbnailUrl: string | null;
  priceRegular: number | null;
  priceDiscounted: number | null;
  priceWbWallet: number | null;
  reviewRating: number | null;
  feedbackCount: number | null;
  positionAbsolute: number | null;
  totalQuantity: number | null;
  costPrice: number | null;
  description: string | null;
  characteristicsJson: string | null;
  supplierName: string | null;
  supplierUrl: string | null;
  media: WorkspaceMarketProductMedia[];
  dateCreate: string;
  dateUpdate: string;
  latestObservedAtUtc: string | null;
}

export interface WorkspaceMarketProductDetail extends WorkspaceMarketProductListItem {
  idCreatedByUser: string;
}

export interface WorkspaceMarketProductListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  tagKey?: WorkspaceMarketProductTagKey;
  parserProductRowId?: string;
}

export interface SaveWorkspaceMarketProductRequest {
  tagKey: WorkspaceMarketProductTagKey;
  note: string | null;
}

export interface CreateWorkspaceMarketProductRequest extends SaveWorkspaceMarketProductRequest {
  parserProductRowId: string;
}

export interface WorkspaceMarketProductMedia {
  id: string;
  url: string;
  fileName: string;
  contentType: string;
  sortOrder: number;
  kind: string;
  uploadedAtUtc: string;
}

export interface CreateDemoWorkspaceMarketProductRequest {
  name: string;
  sourceCategory: string;
  sourceSubcategory: string;
  price: number;
  costPrice: number | null;
  description: string | null;
  characteristicsJson: string | null;
  supplierName: string | null;
  supplierUrl: string | null;
  note: string | null;
  media: File[];
}

export interface WorkspaceMarketProductHistoryPoint {
  observedAtUtc: string;
  value: number | null;
  displayValue: string;
  context: string | null;
}

export interface WorkspaceMarketProductHistoryGroup {
  key: string;
  label: string;
  items: WorkspaceMarketProductHistoryPoint[];
}

export interface WorkspaceMarketProductHistory {
  id: string;
  wbProductId: string | null;
  groups: WorkspaceMarketProductHistoryGroup[];
}
