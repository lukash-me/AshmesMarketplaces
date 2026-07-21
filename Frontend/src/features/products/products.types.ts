export interface ProductListItem {
  id: string;
  idMp: string;
  idBrand: string | null;
  idCategory: string | null;
  idOnMp: string | null;
  skuProduct: string | null;
  skuSeller: string;
  name: string;
  barcode: string | null;
  commission: number | null;
  status: number;
  dateCreated: string | null;
  dateUpdated: string;
}

export interface ProductImage {
  id: string;
  url: string;
  sortOrder: number;
  isMain: boolean;
}

export interface ProductVideo {
  id: string;
  url: string;
  sortOrder: number;
}

export interface ProductDetail extends ProductListItem {
  description: string | null;
  characteristics: unknown | null;
  images: ProductImage[];
  videos: ProductVideo[];
}

export interface ProductListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idMp?: string;
  idBrand?: string;
  idCategory?: string;
  status?: number;
}

export type ProductQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idMp: string;
  idBrand: string;
  idCategory: string;
  status: string;
};
