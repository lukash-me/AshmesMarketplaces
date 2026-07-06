import { http } from '@/shared/api/http';

import type {
  ParserAdminBatch,
  ParserAdminBatchDetail,
  ParserAdminError,
  ParserAdminInstance,
  ParserAdminNiche,
  ParserInstanceConfiguration,
  ParserInstanceConfigurationSavePayload,
  ParserLaunchRequest,
  ParserLaunchSavePayload,
  ParserProxy,
  ParserProxySavePayload,
  ParserAdminProxyRunJournal,
  ParserAdminRetryResponse,
  ParserRunRollbackPreview,
  ParserRunRollbackResponse,
  WbCategoryLeaf
} from './parserAdminMonitoring.types';

export async function getParserAdminInstances(): Promise<ParserAdminInstance[]> {
  const response = await http.get<ParserAdminInstance[]>('/admin/parser/instances');
  return response.data;
}

export async function getParserAdminJournal(params: {
  page?: number;
  pageSize?: number;
} = {}): Promise<ParserAdminProxyRunJournal[]> {
  const response = await http.get<ParserAdminProxyRunJournal[]>('/admin/parser/journal', { params });
  return response.data;
}

export async function getParserAdminBatches(params: {
  status?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<ParserAdminBatch[]> {
  const response = await http.get<ParserAdminBatch[]>('/admin/parser/batches', { params });
  return response.data;
}

export async function getParserAdminBatch(id: string): Promise<ParserAdminBatchDetail> {
  const response = await http.get<ParserAdminBatchDetail>(`/admin/parser/batches/${id}`);
  return response.data;
}

export async function retryParserAdminBatch(id: string): Promise<ParserAdminRetryResponse> {
  const response = await http.post<ParserAdminRetryResponse>(`/admin/parser/batches/${id}/retry`);
  return response.data;
}

export async function getParserRunRollbackPreview(id: string): Promise<ParserRunRollbackPreview> {
  const response = await http.get<ParserRunRollbackPreview>(`/admin/parser/journal/${id}/rollback-preview`);
  return response.data;
}

export async function rollbackParserRun(id: string): Promise<ParserRunRollbackResponse> {
  const response = await http.post<ParserRunRollbackResponse>(`/admin/parser/journal/${id}/rollback`);
  return response.data;
}

export async function getParserAdminNiches(): Promise<ParserAdminNiche[]> {
  const response = await http.get<ParserAdminNiche[]>('/admin/parser/niches');
  return response.data;
}

export async function getParserAdminErrors(params: {
  page?: number;
  pageSize?: number;
} = {}): Promise<ParserAdminError[]> {
  const response = await http.get<ParserAdminError[]>('/admin/parser/errors', { params });
  return response.data;
}

export async function getParserAdminProxies(): Promise<ParserProxy[]> {
  const response = await http.get<ParserProxy[]>('/admin/parser/proxies');
  return response.data;
}

export async function getParserAdminInstanceConfigurations(): Promise<ParserInstanceConfiguration[]> {
  const response = await http.get<ParserInstanceConfiguration[]>('/admin/parser/instance-configurations');
  return response.data;
}

export async function createParserAdminInstanceConfiguration(
  payload: ParserInstanceConfigurationSavePayload
): Promise<ParserInstanceConfiguration> {
  const response = await http.post<ParserInstanceConfiguration>('/admin/parser/instance-configurations', payload);
  return response.data;
}

export async function updateParserAdminInstanceConfiguration(
  id: string,
  payload: ParserInstanceConfigurationSavePayload
): Promise<ParserInstanceConfiguration> {
  const response = await http.patch<ParserInstanceConfiguration>(`/admin/parser/instance-configurations/${id}`, payload);
  return response.data;
}

export async function deleteParserAdminInstanceConfiguration(id: string): Promise<void> {
  await http.delete(`/admin/parser/instance-configurations/${id}`);
}

export async function launchParserAdminInstance(
  id: string,
  payload: ParserLaunchSavePayload
): Promise<ParserLaunchRequest> {
  const response = await http.post<ParserLaunchRequest>(`/admin/parser/instance-configurations/${id}/launch`, payload);
  return response.data;
}

export async function createParserAdminProxy(payload: ParserProxySavePayload): Promise<ParserProxy> {
  const response = await http.post<ParserProxy>('/admin/parser/proxies', payload);
  return response.data;
}

export async function updateParserAdminProxy(id: string, payload: ParserProxySavePayload): Promise<ParserProxy> {
  const response = await http.patch<ParserProxy>(`/admin/parser/proxies/${id}`, payload);
  return response.data;
}

export async function deleteParserAdminProxy(id: string): Promise<void> {
  await http.delete(`/admin/parser/proxies/${id}`);
}

export async function searchWildberriesLeaves(query: string): Promise<WbCategoryLeaf[]> {
  const response = await http.get<WbCategoryLeaf[]>('/marketplace-categories/wildberries/search', {
    params: { query }
  });
  return response.data;
}
