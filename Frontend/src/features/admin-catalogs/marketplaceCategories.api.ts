import { http } from '@/shared/api/http';

export interface WildberriesCategoryNode {
  id: number;
  name: string;
  sourceCategory: string;
  sourceSubcategory: string;
  path: string;
  searchQuery: string | null;
  humanSearchQuery: string | null;
  parentId: number | null;
  isLeaf: boolean;
  level: number;
}

export interface WildberriesCategoryCatalog {
  marketplace: string;
  fetchedAtUtc: string;
  nodes: WildberriesCategoryNode[];
}

export type WbScopeSubjectMappingStatus = 'active' | 'rejected' | 'needs_review';

export interface WbCategoryScopeSubjectMapping {
  id: string;
  wbMenuId: number;
  menuToken: string;
  sourcePath: string;
  subjectId: number;
  subjectName: string | null;
  status: WbScopeSubjectMappingStatus;
  mappingSource: string;
  observedAtUtc: string;
  updatedAtUtc: string;
}

export async function getWildberriesCategoryTree(): Promise<WildberriesCategoryCatalog> {
  const response = await http.get<WildberriesCategoryCatalog>('/marketplace-categories/wildberries/tree');
  return response.data;
}

export async function getWbCategoryScopeSubjectMappings(wbMenuId: number): Promise<WbCategoryScopeSubjectMapping[]> {
  const response = await http.get<WbCategoryScopeSubjectMapping[]>('/admin/parser/scope-subject-mappings', {
    params: { wbMenuId }
  });
  return response.data;
}

export async function updateWbCategoryScopeSubjectMapping(
  id: string,
  status: WbScopeSubjectMappingStatus
): Promise<WbCategoryScopeSubjectMapping> {
  const response = await http.patch<WbCategoryScopeSubjectMapping>(`/admin/parser/scope-subject-mappings/${id}`, {
    status
  });
  return response.data;
}
