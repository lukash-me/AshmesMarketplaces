import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type { ProductDetail, ProductListItem, ProductListParams } from './products.types';

export async function getProducts(params: ProductListParams): Promise<PagedResponse<ProductListItem>> {
  const response = await http.get<PagedResponse<ProductListItem>>('/products', { params });
  return response.data;
}

export async function getProduct(id: string): Promise<ProductDetail> {
  const response = await http.get<ProductDetail>(`/products/${id}`);
  return response.data;
}
