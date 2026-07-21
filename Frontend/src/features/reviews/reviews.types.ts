export interface ReviewListItem {
  id: string;
  idProduct: string;
  idOnMp: string;
  rating: number;
  text: string | null;
  isReplied: boolean;
  dateCreate: string;
  dateReply: string | null;
}

export type ReviewDetail = ReviewListItem;

export interface ReviewReplyListItem {
  id: string;
  idReview: string;
  idOnMp: string;
  text: string;
  status: number;
  dateCreate: string;
  dateUpdate: string;
}

export type ReviewReplyDetail = ReviewReplyListItem;

export interface ReviewListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idProduct?: string;
  rating?: number;
  isReplied?: boolean;
  dateCreateFrom?: string;
  dateCreateTo?: string;
}

export interface ReviewReplyListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idReview?: string;
  status?: number;
  dateCreateFrom?: string;
  dateCreateTo?: string;
}

export type ReviewQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idProduct: string;
  rating: string;
  isReplied: string;
  dateCreateFrom: string;
  dateCreateTo: string;
};
