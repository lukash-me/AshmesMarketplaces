export interface ExpenseListItem {
  id: string;
  idWorkspace: string;
  idCategory: string | null;
  idCreator: string;
  idResponsible: string | null;
  name: string;
  cost: number | null;
  status: number;
  datePay: string | null;
  dateCreate: string;
  dateUpdate: string;
}

export interface ExpenseDetail extends ExpenseListItem {
  description: string | null;
}

export interface ExpenseCategoryListItem {
  id: string;
  name: string;
  description: string | null;
  dateCreate: string;
  dateUpdate: string;
}

export type ExpenseCategoryDetail = ExpenseCategoryListItem;

export interface ExpenseListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idWorkspace?: string;
  idCategory?: string;
  idCreator?: string;
  idResponsible?: string;
  status?: number;
  datePayFrom?: string;
  datePayTo?: string;
  dateCreateFrom?: string;
  dateCreateTo?: string;
}

export interface ExpenseCategoryListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
}

export type ExpenseQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  search: string;
  idWorkspace: string;
  idCategory: string;
  idCreator: string;
  idResponsible: string;
  status: string;
  datePayFrom: string;
  datePayTo: string;
  dateCreateFrom: string;
  dateCreateTo: string;
};
