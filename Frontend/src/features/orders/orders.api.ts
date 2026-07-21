import { http, publicHttp } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type {
  ObservedStockDecreaseParams,
  ObservedStockDecreaseResponse,
  ObservedMarketEventParams,
  ObservedMarketEventResponse,
  OrderDetail,
  OrderListItem,
  OrderListParams
} from './orders.types';

export async function getOrders(params: OrderListParams): Promise<PagedResponse<OrderListItem>> {
  const response = await publicHttp.get<PagedResponse<OrderListItem>>('/orders', { params });
  return response.data;
}

export async function getOrder(id: string): Promise<OrderDetail> {
  const response = await publicHttp.get<OrderDetail>(`/orders/${id}`);
  return response.data;
}

export async function getObservedStockDecreases(
  params: ObservedStockDecreaseParams
): Promise<ObservedStockDecreaseResponse> {
  const response = await publicHttp.get<ObservedStockDecreaseResponse>(
    '/parser/logistics/observed-stock-decreases',
    { params }
  );
  return response.data;
}

export async function getObservedMarketEvents(
  params: ObservedMarketEventParams
): Promise<ObservedMarketEventResponse> {
  const response = await publicHttp.get<ObservedMarketEventResponse>(
    '/parser/logistics/observed-events',
    { params }
  );
  return response.data;
}
