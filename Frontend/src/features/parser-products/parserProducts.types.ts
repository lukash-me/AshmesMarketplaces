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
  ratingRounded: number | null;
  reviewRating: number | null;
  feedbackCount: number | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceQuery: string | null;
  thumbnailUrl: string | null;
}

export interface ParserProductDetail extends Omit<ParserProductListItem, 'thumbnailUrl'> {
  marketplace: string;
  skuProduct: string | null;
  entity: string | null;
  brandIdOnMp: number | null;
  sellerIdOnMp: number | null;
  totalQuantity: number | null;
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
