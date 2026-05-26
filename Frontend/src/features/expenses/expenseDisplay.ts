import type { ExpenseCategoryListItem, ExpenseStatusKey, ExpenseUserListItem } from './expenses.types';

export type ExpenseBadgeTone =
  | 'neutral'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'ember'
  | 'hot';

export type ExpenseCategoryLookup = Record<string, ExpenseCategoryListItem>;
export type ExpenseUserLookup = Record<string, ExpenseUserListItem>;

export const expenseStatusOptions: Array<{ key: ExpenseStatusKey; label: string; status: number }> = [
  { key: 'planned', label: 'Запланирован', status: 0 },
  { key: 'pending_payment', label: 'К оплате', status: 1 },
  { key: 'paid', label: 'Оплачен', status: 2 },
  { key: 'cancelled', label: 'Отменён', status: 3 }
];

export function getExpenseStatusLabel(statusKey: string | null | undefined, fallback?: string): string {
  return expenseStatusOptions.find((option) => option.key === statusKey)?.label ?? fallback ?? 'Другой статус';
}

export function getExpenseStatusTone(statusKey: string | null | undefined): ExpenseBadgeTone {
  if (statusKey === 'paid') {
    return 'success';
  }

  if (statusKey === 'pending_payment') {
    return 'warning';
  }

  if (statusKey === 'planned') {
    return 'info';
  }

  if (statusKey === 'cancelled') {
    return 'danger';
  }

  return 'neutral';
}

export function getExpenseStatusNumber(statusKey: ExpenseStatusKey): number {
  return expenseStatusOptions.find((option) => option.key === statusKey)?.status ?? 0;
}

export function getExpenseCategoryLabel(
  idCategory: string | null | undefined,
  categoriesById: ExpenseCategoryLookup,
  hydratedName?: string | null
): string {
  if (hydratedName?.trim()) {
    return hydratedName;
  }

  if (!idCategory) {
    return 'Без категории';
  }

  return categoriesById[idCategory]?.name ?? 'Без категории';
}

export function getUserLabel(
  idUser: string | null | undefined,
  usersById: ExpenseUserLookup,
  login?: string | null,
  email?: string | null
): string {
  if (login?.trim()) {
    return login;
  }

  if (email?.trim()) {
    return email;
  }

  if (idUser && usersById[idUser]) {
    return usersById[idUser].login || usersById[idUser].email || 'Пользователь';
  }

  return 'Не назначен';
}

export function formatDateTime(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

export function formatDateShort(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date);
}

export function formatAmount(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }

  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: 'RUB',
    maximumFractionDigits: 0
  }).format(value);
}

export function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

export function getExpenseSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'по убыванию' : 'по возрастанию';
  const field = value.replace(/^-/, '');

  if (field === 'name') {
    return `Название ${direction}`;
  }

  if (field === 'cost') {
    return `Сумма ${direction}`;
  }

  if (field === 'datePay') {
    return `Дата оплаты ${direction}`;
  }

  if (field === 'dateUpdate') {
    return `Обновлено ${direction}`;
  }

  return `${field} ${direction}`;
}
