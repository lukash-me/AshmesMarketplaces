import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type {
  ReviewDetail,
  ReviewListItem,
  ReviewListParams,
  ReviewReplyDetail,
  ReviewReplyListItem,
  ReviewReplyListParams
} from './reviews.types';

export async function getReviews(params: ReviewListParams): Promise<PagedResponse<ReviewListItem>> {
  const response = await http.get<PagedResponse<ReviewListItem>>('/reviews', { params });
  return response.data;
}

export async function getReview(id: string): Promise<ReviewDetail> {
  const response = await http.get<ReviewDetail>(`/reviews/${id}`);
  return response.data;
}

export async function getReviewReplies(
  params: ReviewReplyListParams
): Promise<PagedResponse<ReviewReplyListItem>> {
  const response = await http.get<PagedResponse<ReviewReplyListItem>>('/review-replies', { params });
  return response.data;
}

export async function getReviewReply(id: string): Promise<ReviewReplyDetail> {
  const response = await http.get<ReviewReplyDetail>(`/review-replies/${id}`);
  return response.data;
}
