export type WorkspaceMarketProductTagKey = 'competitor' | 'idea';

export interface WorkspaceMarketProductListItem {
  id: string;
  idWorkspace: string;
  parserProductRowId: string;
  wbProductId: string;
  wbRootId: string | null;
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
  wbProductId: string;
  groups: WorkspaceMarketProductHistoryGroup[];
}
