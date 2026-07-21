export interface RecommendationListItem {
  id: string;
  idModel: string;
  type: number;
  typeObject: number;
  score: number;
  dateCreate: string;
  dateUpdate: string;
}

export interface RecommendationDetail extends RecommendationListItem {
  explanation: unknown | null;
  snapshot: unknown | null;
}

export interface RecommendationProductListItem {
  idRecommendation: string;
  idProduct: string;
}

export type RecommendationProductDetail = RecommendationProductListItem;

export interface RecommendationCategoryListItem {
  idRecommendation: string;
  idCategory: string;
}

export type RecommendationCategoryDetail = RecommendationCategoryListItem;

export interface RecommendationListParams {
  page: number;
  pageSize: number;
  sort?: string;
  idModel?: string;
  type?: number;
  typeObject?: number;
  dateCreateFrom?: string;
  dateCreateTo?: string;
}

export interface RecommendationProductListParams {
  page: number;
  pageSize: number;
  sort?: string;
  idRecommendation?: string;
  idProduct?: string;
}

export interface RecommendationCategoryListParams {
  page: number;
  pageSize: number;
  sort?: string;
  idRecommendation?: string;
  idCategory?: string;
}

export type RecommendationQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  idModel: string;
  type: string;
  typeObject: string;
  dateCreateFrom: string;
  dateCreateTo: string;
};
