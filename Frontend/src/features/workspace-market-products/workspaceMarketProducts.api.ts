import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  CreateWorkspaceMarketProductRequest,
  CreateDemoWorkspaceMarketProductRequest,
  SaveWorkspaceMarketProductRequest,
  WorkspaceMarketProductDetail,
  WorkspaceMarketProductHistory,
  WorkspaceMarketProductListItem,
  WorkspaceMarketProductListParams
} from './workspaceMarketProducts.types';

function basePath(workspaceId: string): string {
  return `/workspaces/${workspaceId}/market-products`;
}

export async function getWorkspaceMarketProducts(
  workspaceId: string,
  params: WorkspaceMarketProductListParams
): Promise<PagedResponse<WorkspaceMarketProductListItem>> {
  const response = await http.get<PagedResponse<WorkspaceMarketProductListItem>>(basePath(workspaceId), { params });
  return response.data;
}

export async function getWorkspaceMarketProduct(
  workspaceId: string,
  id: string
): Promise<WorkspaceMarketProductDetail> {
  const response = await http.get<WorkspaceMarketProductDetail>(`${basePath(workspaceId)}/${id}`);
  return response.data;
}

export async function addWorkspaceMarketProduct(
  workspaceId: string,
  request: CreateWorkspaceMarketProductRequest
): Promise<WorkspaceMarketProductDetail> {
  const response = await http.post<WorkspaceMarketProductDetail>(basePath(workspaceId), request);
  return response.data;
}

export async function addDemoWorkspaceMarketProduct(
  workspaceId: string,
  request: CreateDemoWorkspaceMarketProductRequest
): Promise<WorkspaceMarketProductDetail> {
  const formData = new FormData();
  formData.append('name', request.name);
  formData.append('sourceCategory', request.sourceCategory);
  formData.append('sourceSubcategory', request.sourceSubcategory);
  formData.append('price', String(request.price));
  if (request.costPrice !== null) {
    formData.append('costPrice', String(request.costPrice));
  }
  if (request.description) {
    formData.append('description', request.description);
  }
  if (request.characteristicsJson) {
    formData.append('characteristicsJson', request.characteristicsJson);
  }
  if (request.supplierName) {
    formData.append('supplierName', request.supplierName);
  }
  if (request.supplierUrl) {
    formData.append('supplierUrl', request.supplierUrl);
  }
  if (request.note) {
    formData.append('note', request.note);
  }
  for (const file of request.media) {
    formData.append('media', file);
  }

  const response = await http.post<WorkspaceMarketProductDetail>(`${basePath(workspaceId)}/demo`, formData);
  return response.data;
}

export async function updateWorkspaceMarketProduct(
  workspaceId: string,
  id: string,
  request: SaveWorkspaceMarketProductRequest
): Promise<WorkspaceMarketProductDetail> {
  const response = await http.put<WorkspaceMarketProductDetail>(`${basePath(workspaceId)}/${id}`, request);
  return response.data;
}

export async function deleteWorkspaceMarketProduct(workspaceId: string, id: string): Promise<void> {
  await http.delete(`${basePath(workspaceId)}/${id}`);
}

export async function getWorkspaceMarketProductHistory(
  workspaceId: string,
  id: string
): Promise<WorkspaceMarketProductHistory> {
  const response = await http.get<WorkspaceMarketProductHistory>(`${basePath(workspaceId)}/${id}/history`);
  return response.data;
}
