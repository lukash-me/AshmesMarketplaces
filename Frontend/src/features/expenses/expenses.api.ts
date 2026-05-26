import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  ExpenseCategoryDetail,
  ExpenseCategoryListItem,
  ExpenseCategoryListParams,
  ExpenseDetail,
  ExpenseListItem,
  ExpenseListParams,
  ExpenseSummary,
  ExpenseSummaryParams,
  ExpenseUserListItem,
  ExpenseUserListParams,
  ExpenseWorkspaceListItem,
  ExpenseWorkspaceListParams,
  SaveExpenseRequest
} from './expenses.types';

export async function getExpenses(
  params: ExpenseListParams
): Promise<PagedResponse<ExpenseListItem>> {
  const response = await http.get<PagedResponse<ExpenseListItem>>('/expenses', { params });
  return response.data;
}

export async function getExpense(id: string): Promise<ExpenseDetail> {
  const response = await http.get<ExpenseDetail>(`/expenses/${id}`);
  return response.data;
}

export async function getExpenseSummary(params: ExpenseSummaryParams): Promise<ExpenseSummary> {
  const response = await http.get<ExpenseSummary>('/expenses/summary', { params });
  return response.data;
}

export async function createExpense(request: SaveExpenseRequest): Promise<ExpenseDetail> {
  const response = await http.post<ExpenseDetail>('/expenses', request);
  return response.data;
}

export async function updateExpense(id: string, request: SaveExpenseRequest): Promise<ExpenseDetail> {
  const response = await http.put<ExpenseDetail>(`/expenses/${id}`, request);
  return response.data;
}

export async function getExpenseCategories(
  params: ExpenseCategoryListParams
): Promise<PagedResponse<ExpenseCategoryListItem>> {
  const response = await http.get<PagedResponse<ExpenseCategoryListItem>>('/expense-categories', {
    params
  });
  return response.data;
}

export async function getExpenseCategory(id: string): Promise<ExpenseCategoryDetail> {
  const response = await http.get<ExpenseCategoryDetail>(`/expense-categories/${id}`);
  return response.data;
}

export async function getExpenseUsers(
  params: ExpenseUserListParams
): Promise<PagedResponse<ExpenseUserListItem>> {
  const response = await http.get<PagedResponse<ExpenseUserListItem>>('/users', { params });
  return response.data;
}

export async function getExpenseWorkspaces(
  params: ExpenseWorkspaceListParams
): Promise<PagedResponse<ExpenseWorkspaceListItem>> {
  const response = await http.get<PagedResponse<ExpenseWorkspaceListItem>>('/workspaces', { params });
  return response.data;
}
