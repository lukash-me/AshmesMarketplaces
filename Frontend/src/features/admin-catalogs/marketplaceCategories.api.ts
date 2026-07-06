import { http } from '@/shared/api/http';

export interface WildberriesCategoryNode {
  id: number;
  name: string;
  sourceCategory: string;
  sourceSubcategory: string;
  path: string;
  searchQuery: string | null;
  parentId: number | null;
  isLeaf: boolean;
  level: number;
}

export interface WildberriesCategoryCatalog {
  marketplace: string;
  fetchedAtUtc: string;
  nodes: WildberriesCategoryNode[];
}

export async function getWildberriesCategoryTree(): Promise<WildberriesCategoryCatalog> {
  const response = await http.get<WildberriesCategoryCatalog>('/marketplace-categories/wildberries/tree');
  return response.data;
}
