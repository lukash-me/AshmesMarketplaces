export type ExpenseStatusKey = 'planned' | 'pending_payment' | 'paid' | 'cancelled';

export interface ExpenseListItem {
  id: string;
  idWorkspace: string;
  idCategory: string | null;
  idCreator: string;
  idResponsible: string | null;
  name: string;
  cost: number | null;
  status: number;
  statusKey: ExpenseStatusKey | null;
  statusLabel: string;
  categoryName: string | null;
  workspaceName: string;
  creatorLogin: string;
  creatorEmail: string | null;
  responsibleLogin: string | null;
  responsibleEmail: string | null;
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

export interface ExpenseSummaryBucket {
  key: string;
  label: string;
  count: number;
  amount: number;
}

export interface ExpenseSummary {
  totalCount: number;
  totalAmount: number;
  paidCount: number;
  paidAmount: number;
  pendingPaymentCount: number;
  pendingPaymentAmount: number;
  plannedCount: number;
  cancelledCount: number;
  withoutPaymentDateCount: number;
  averageAmount: number | null;
  latestPaymentDate: string | null;
  byCategory: ExpenseSummaryBucket[];
  byStatus: ExpenseSummaryBucket[];
  byMonth: ExpenseSummaryBucket[];
}

export interface ExpenseListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idWorkspace?: string;
  idCategory?: string;
  categoryId?: string;
  idResponsible?: string;
  responsibleUserId?: string;
  status?: number;
  statusKey?: ExpenseStatusKey;
  datePayFrom?: string;
  datePayTo?: string;
  amountFrom?: number;
  amountTo?: number;
}

export type ExpenseSummaryParams = Omit<ExpenseListParams, 'page' | 'pageSize' | 'sort'>;

export interface ExpenseCategoryListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
}

export interface ExpenseUserListItem {
  id: string;
  login: string;
  email: string | null;
}

export interface ExpenseUserListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
}

export interface ExpenseWorkspaceListItem {
  id: string;
  name: string;
}

export interface ExpenseWorkspaceListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
}

export interface SaveExpenseRequest {
  idWorkspace: string;
  idCategory: string | null;
  idCreator: string;
  idResponsible: string | null;
  name: string;
  description: string | null;
  cost: number;
  status: number;
  statusKey: ExpenseStatusKey;
  datePay: string | null;
  dateCreate: string;
  dateUpdate: string;
}

export type ExpenseQueryState = {
  page: number;
  pageSize: number;
  sort: string;
  search: string;
  idCategory: string;
  idResponsible: string;
  statusKey: string;
  datePayFrom: string;
  datePayTo: string;
  amountFrom: string;
  amountTo: string;
};
