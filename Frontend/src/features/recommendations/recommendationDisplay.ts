export type RecommendationBadgeTone =
  | 'neutral'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'ember'
  | 'hot';

export function getRecommendationTypeLabel(type: number): string {
  return Number.isFinite(type) ? `Type ${type}` : 'Type -';
}

export function getRecommendationObjectTypeLabel(typeObject: number): string {
  return Number.isFinite(typeObject) ? `Object type ${typeObject}` : 'Object type -';
}

export function getRecommendationNeutralTone(): RecommendationBadgeTone {
  return 'neutral';
}

export function formatScore(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }

  return new Intl.NumberFormat('en', {
    maximumFractionDigits: 6
  }).format(value);
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

export function compactId(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

export function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

export function formatJson(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

export function getRecommendationSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'score') {
    return `Score ${direction}`;
  }

  if (field === 'dateCreate') {
    return `Created ${direction}`;
  }

  if (field === 'dateUpdate') {
    return `Updated ${direction}`;
  }

  return `${field} ${direction}`;
}
