import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type { ProductListItem, ProductListParams } from './products.types';

export async function getProducts(params: ProductListParams): Promise<PagedResponse<ProductListItem>> {
  const response = await http.get<PagedResponse<ProductListItem>>('/products', { params });
  return response.data;
}
