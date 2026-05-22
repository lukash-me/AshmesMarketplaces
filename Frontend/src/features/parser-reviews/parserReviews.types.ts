export interface ParserReviewListItem {
  id: string;
  parserRunId: string;
  parsedAtUtc: string;
  sourceWbRootId: string;
  wbProductId: string;
  reviewIdOnMp: string;
  reviewAttributionMode: string;
  rating: number | null;
  textPreview: string | null;
  createdAtOnMp: string | null;
  hasObservedReply: boolean;
  isPartialSnapshot: boolean;
  isCappedRootPayload: boolean;
  isFullHistoryUnknown: boolean;
}

export interface ParserReviewRootFetchSummary {
  status: string;
  timestampUtc: string;
  payloadFeedbackCount: number | null;
  payloadFeedbackRowsSeen: number;
  selectedReviewRowsSeen: number;
  reviewsWritten: number;
  repliesWritten: number;
}

export interface ParserReviewDetail extends ParserReviewListItem {
  text: string | null;
  pros: string | null;
  cons: string | null;
  reviewerName: string | null;
  reviewerCountry: string | null;
  reviewerHasPhoto: boolean | null;
  helpfulPlus: number | null;
  helpfulMinus: number | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  sourceQuery: string | null;
  sourceRegionDest: string | null;
  inputProductsParserRunId: string | null;
  sourceFileKind: string;
  sourceFileSha256: string;
  sourceLineNumber: number;
  rowHash: string;
  rootFetch: ParserReviewRootFetchSummary | null;
}

export interface ParserReviewReply {
  id: string;
  parserRunId: string;
  sourceWbRootId: string;
  wbProductId: string;
  reviewIdOnMp: string;
  reviewAttributionMode: string;
  replyIdOnMp: string | null;
  replyFallbackHash: string | null;
  hasStableReplyId: boolean;
  text: string | null;
  createdAtOnMp: string | null;
  updatedAtOnMp: string | null;
  replyAuthor: string | null;
  replyState: string | null;
  isPartialSnapshot: boolean;
  isCappedRootPayload: boolean;
  isFullHistoryUnknown: boolean;
  sourceFileKind: string;
  sourceLineNumber: number;
}

export interface ParserReviewListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  parserRunId?: string;
  wbProductId?: string;
  sourceWbRootId?: string;
  rating?: number;
  hasObservedReply?: boolean;
  cappedRootPayload?: boolean;
  reviewAttributionMode?: string;
  createdAtOnMpFrom?: string;
  createdAtOnMpTo?: string;
}

export interface ParserReviewReplyListParams {
  page: number;
  pageSize: number;
  sort?: string;
}

export type ParserReviewQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  search: string;
  parserRunId: string;
  wbProductId: string;
  sourceWbRootId: string;
  rating: string;
  hasObservedReply: string;
  cappedRootPayload: string;
  reviewAttributionMode: string;
  createdAtOnMpFrom: string;
  createdAtOnMpTo: string;
};
