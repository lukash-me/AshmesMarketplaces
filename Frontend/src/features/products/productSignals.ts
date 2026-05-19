import type { ProductListItem } from './products.types';

export type ProductStatusCode = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type ProductHeatTier = 'dormant' | 'warm' | 'rising' | 'hot';

export const PRODUCT_STATUS_OPTIONS: Array<{ value: ProductStatusCode; label: string }> = [
  { value: 0, label: 'Draft' },
  { value: 1, label: 'Pending' },
  { value: 2, label: 'Active' },
  { value: 3, label: 'Rejected' },
  { value: 4, label: 'Blocked' },
  { value: 5, label: 'Archived' },
  { value: 6, label: 'Out of stock' },
  { value: 7, label: 'Disappeared' }
];

const STATUS_LABELS = new Map(PRODUCT_STATUS_OPTIONS.map((option) => [option.value, option.label]));

export function getProductStatusLabel(status: number): string {
  return STATUS_LABELS.get(status as ProductStatusCode) ?? `Status ${status}`;
}

export function getProductStatusTone(status: number): 'success' | 'warning' | 'danger' | 'neutral' | 'info' {
  if (status === 2) {
    return 'success';
  }

  if (status === 1 || status === 6) {
    return 'warning';
  }

  if (status === 3 || status === 4 || status === 7) {
    return 'danger';
  }

  if (status === 0) {
    return 'info';
  }

  return 'neutral';
}

export function getProductHeatTier(product: Pick<ProductListItem, 'status' | 'dateUpdated'>): ProductHeatTier {
  const age = getDaysSince(product.dateUpdated);

  if (!Number.isFinite(age)) {
    return 'dormant';
  }

  if (product.status === 2 && age <= 7) {
    return 'hot';
  }

  if ((product.status === 2 && age <= 30) || (product.status === 1 && age <= 7)) {
    return 'rising';
  }

  if (product.status === 2 || product.status === 1 || product.status === 6 || age <= 60) {
    return 'warm';
  }

  return 'dormant';
}

export function getProductHeatLabel(product: Pick<ProductListItem, 'status' | 'dateUpdated'>): string {
  const tier = getProductHeatTier(product);

  if (tier === 'hot') {
    return 'Hot';
  }

  if (tier === 'rising') {
    return 'Rising';
  }

  if (tier === 'warm') {
    return 'Warm';
  }

  return 'Dormant';
}

export function getProductHeatTone(product: Pick<ProductListItem, 'status' | 'dateUpdated'>): 'neutral' | 'warning' | 'ember' | 'hot' {
  const tier = getProductHeatTier(product);

  if (tier === 'hot') {
    return 'hot';
  }

  if (tier === 'rising') {
    return 'ember';
  }

  if (tier === 'warm') {
    return 'warning';
  }

  return 'neutral';
}

export function getProductSignalTitle(product: Pick<ProductListItem, 'status' | 'dateUpdated'>): string {
  return `Visual signal derived only from status (${getProductStatusLabel(product.status)}) and update recency.`;
}

export function getDaysSince(value: string | null | undefined): number {
  if (!value) {
    return Number.POSITIVE_INFINITY;
  }

  const updated = new Date(value).getTime();

  if (!Number.isFinite(updated)) {
    return Number.POSITIVE_INFINITY;
  }

  return Math.max(0, Math.floor((Date.now() - updated) / 86_400_000));
}
