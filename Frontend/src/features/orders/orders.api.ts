import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type { OrderDetail, OrderListItem, OrderListParams } from './orders.types';

export async function getOrders(params: OrderListParams): Promise<PagedResponse<OrderListItem>> {
  const response = await http.get<PagedResponse<OrderListItem>>('/orders', { params });
  return response.data;
}

export async function getOrder(id: string): Promise<OrderDetail> {
  const response = await http.get<OrderDetail>(`/orders/${id}`);
  return response.data;
}
