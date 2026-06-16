export interface OrderListItem {
  id: string;
  idProduct: string;
  price: number;
  discount: number;
  amount: number;
  locationSource: string | null;
  locationDestination: string | null;
  status: number;
  dateDelivered: string | null;
  dateOpened: string;
  dateClosed: string | null;
  dateUpdate: string;
}

export type OrderDetail = OrderListItem;

export interface OrderListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idProduct?: string;
  status?: number;
  dateOpenedFrom?: string;
  dateOpenedTo?: string;
}

export type OrderQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idProduct: string;
  status: string;
  dateOpenedFrom: string;
  dateOpenedTo: string;
};

export interface ObservedStockDecreaseItem {
  wbProductId: number;
  wbRootId: number | null;
  name: string | null;
  brandName: string | null;
  sellerName: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  destination: string | null;
  previousQuantity: number;
  currentQuantity: number;
  quantityDecrease: number;
  previousObservedAtUtc: string | null;
  currentObservedAtUtc: string | null;
  price: number | null;
  rating: number | null;
  feedbackCount: number | null;
  imageUrl: string | null;
}

export interface ObservedStockDecreaseSummary {
  comparedProductsCount: number;
  productsWithDecrease: number;
  productsWithoutComparableQuantity: number;
  currentOnlyProductsCount: number;
  previousOnlyProductsCount: number;
  nonOverlappingProductsCount: number;
  totalObservedDecrease: number;
  maxObservedDecrease: number | null;
  averageObservedDecrease: number | null;
}

export interface ObservedStockDecreaseResponse {
  currentLogisticsRunId: string | null;
  previousLogisticsRunId: string | null;
  currentObservedAtUtc: string | null;
  previousObservedAtUtc: string | null;
  totalCount: number;
  page: number;
  pageSize: number;
  items: ObservedStockDecreaseItem[];
  summary: ObservedStockDecreaseSummary;
  warnings: string[];
}

export interface ObservedStockDecreaseParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  sourceCategory?: string;
  sourceSubcategory?: string;
  brandName?: string;
  sellerName?: string;
  minDecrease?: number;
}

export type ObservedStockDecreaseQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  sourceCategory: string;
  sourceSubcategory: string;
  brandName: string;
  sellerName: string;
  minDecrease: string;
};

export type ObservedMarketEventType =
  | 'stock_decreased'
  | 'stock_increased'
  | 'new_product_observed'
  | 'product_missing_in_current';

export type ObservedMarketEventTab = 'assumed-orders' | 'new-products' | 'restocks';

export interface ObservedMarketEventItem {
  eventType: ObservedMarketEventType;
  parserProductRowId: string | null;
  wbProductId: string;
  wbRootId: string | null;
  name: string | null;
  brandName: string | null;
  sellerName: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  destination: string | null;
  previousQuantity: number | null;
  currentQuantity: number | null;
  quantityChange: number | null;
  previousObservedAtUtc: string | null;
  currentObservedAtUtc: string | null;
  price: number | null;
  priceRegular: number | null;
  priceDiscounted: number | null;
  priceWbWallet: number | null;
  rating: number | null;
  feedbackCount: number | null;
  imageUrl: string | null;
}

export interface ObservedMarketEventSummary {
  comparedPairsCount: number;
  stockDecreasedCount: number;
  stockIncreasedCount: number;
  newProductObservedCount: number;
  productMissingInCurrentCount: number;
  unchangedCount: number;
  notComparableQuantityCount: number;
  currentOnlyProductsCount: number;
  previousOnlyProductsCount: number;
  overlappingProductsCount: number;
  totalObservedDecrease: number;
  totalObservedIncrease: number;
}

export interface ObservedMarketEventResponse {
  currentLogisticsRunId: string | null;
  previousLogisticsRunId: string | null;
  currentObservedAtUtc: string | null;
  previousObservedAtUtc: string | null;
  totalCount: number;
  page: number;
  pageSize: number;
  items: ObservedMarketEventItem[];
  summary: ObservedMarketEventSummary;
  warnings: string[];
}

export interface ObservedMarketEventParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  sourceCategory?: string;
  sourceSubcategory?: string;
  brandName?: string;
  sellerName?: string;
  eventType?: ObservedMarketEventType;
  minQuantityChange?: number;
}

export type ObservedMarketEventQueryState = {
  tab: ObservedMarketEventTab;
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  sourceCategory: string;
  sourceSubcategory: string;
  brandName: string;
  sellerName: string;
  minQuantityChange: string;
};
