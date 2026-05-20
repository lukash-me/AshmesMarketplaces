import { http } from '@/shared/api/http';
import type { PagedResponse } from '@/entities/pagination';

import type {
  LogisticDetail,
  LogisticListItem,
  LogisticListParams,
  WarehouseDetail
} from './logistics.types';

export async function getLogistics(
  params: LogisticListParams
): Promise<PagedResponse<LogisticListItem>> {
  const response = await http.get<PagedResponse<LogisticListItem>>('/logistics', { params });
  return response.data;
}

export async function getLogistic(id: string): Promise<LogisticDetail> {
  const response = await http.get<LogisticDetail>(`/logistics/${id}`);
  return response.data;
}

export async function getWarehouse(id: string): Promise<WarehouseDetail> {
  const response = await http.get<WarehouseDetail>(`/warehouses/${id}`);
  return response.data;
}
