import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  ParserReviewDetail,
  ParserReviewListItem,
  ParserReviewListParams,
  ParserReviewReply,
  ParserReviewReplyListParams
} from './parserReviews.types';

export async function getParserReviews(
  params: ParserReviewListParams
): Promise<PagedResponse<ParserReviewListItem>> {
  const response = await http.get<PagedResponse<ParserReviewListItem>>('/parser/reviews', { params });
  return response.data;
}

export async function getParserReview(id: string): Promise<ParserReviewDetail> {
  const response = await http.get<ParserReviewDetail>(`/parser/reviews/${id}`);
  return response.data;
}

export async function getParserReviewReplies(
  id: string,
  params: ParserReviewReplyListParams
): Promise<PagedResponse<ParserReviewReply>> {
  const response = await http.get<PagedResponse<ParserReviewReply>>(`/parser/reviews/${id}/replies`, { params });
  return response.data;
}
