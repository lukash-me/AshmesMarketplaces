import { publicHttp } from '@/shared/api/http';
import type {
  RuleConstructorCountsRequest,
  RuleConstructorCountsResponse,
  RuleConstructorFilter,
  RuleConstructorSearchRequest,
  RuleConstructorSearchResponse
} from './ruleConstructor.types';

export async function getRuleConstructorFilters(): Promise<RuleConstructorFilter[]> {
  const response = await publicHttp.get<RuleConstructorFilter[]>('/rule-constructor/filters');
  return response.data;
}

export async function searchRuleConstructor(
  request: RuleConstructorSearchRequest
): Promise<RuleConstructorSearchResponse> {
  const response = await publicHttp.post<RuleConstructorSearchResponse>('/rule-constructor/search', request);
  return response.data;
}

export async function getRuleConstructorCounts(
  request: RuleConstructorCountsRequest
): Promise<RuleConstructorCountsResponse> {
  const response = await publicHttp.post<RuleConstructorCountsResponse>('/rule-constructor/counts', request);
  return response.data;
}
