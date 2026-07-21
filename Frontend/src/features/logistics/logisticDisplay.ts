export type LogisticBadgeTone =
  | 'neutral'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'ember'
  | 'hot';

export function getLogisticTypeLabel(type: number): string {
  return Number.isFinite(type) ? `Type ${type}` : 'Type -';
}

export function getWarehouseActiveLabel(isActive: boolean): string {
  return isActive ? 'Active' : 'Inactive';
}

export function getWarehouseActiveTone(isActive: boolean): LogisticBadgeTone {
  return isActive ? 'success' : 'neutral';
}

export function getNeutralTone(): LogisticBadgeTone {
  return 'neutral';
}

export function formatDateTime(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
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

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric'
  }).format(date);
}

export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }

  return new Intl.NumberFormat('en', {
    maximumFractionDigits: 2
  }).format(value);
}

export function compactId(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

export function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

export function getLogisticSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'date') {
    return `Date ${direction}`;
  }

  if (field === 'stockAmountStatistic') {
    return `Stock statistic ${direction}`;
  }

  return `${field} ${direction}`;
}
