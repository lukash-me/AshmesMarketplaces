import type { PagedResponse } from '@/entities/pagination';
import { http } from '@/shared/api/http';

import type {
  ExpenseCategoryDetail,
  ExpenseCategoryListItem,
  ExpenseCategoryListParams,
  ExpenseDetail,
  ExpenseListItem,
  ExpenseListParams
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
