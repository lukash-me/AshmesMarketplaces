import { http } from '@/shared/api/http';

import type {
  ParserAdminBatch,
  ParserAdminBatchDetail,
  ParserAdminError,
  ParserAdminInstance,
  ParserAdminNiche,
  ParserAdminProxyRunJournal,
  ParserAdminRetryResponse
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
