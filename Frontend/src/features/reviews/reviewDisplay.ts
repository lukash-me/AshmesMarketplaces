import type { ReviewListItem } from './reviews.types';

export type ReviewBadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'ember' | 'hot';

export type ReviewSignal = {
  label: string;
  tone: ReviewBadgeTone;
};

const RECENT_DAYS = 7;
const DAY_IN_MS = 24 * 60 * 60 * 1000;

export function getReviewSignal(review: ReviewListItem): ReviewSignal {
  if (review.rating <= 2) {
    return { label: 'Low rating', tone: 'danger' };
  }

  if (!review.isReplied) {
    return { label: 'Unreplied', tone: 'ember' };
  }

  if (isRecentlyCreated(review.dateCreate)) {
    return { label: 'Recent', tone: 'info' };
  }

  return { label: 'Replied', tone: 'success' };
}

export function getReplyStateLabel(isReplied: boolean): string {
  return isReplied ? 'Replied' : 'Unreplied';
}

export function getReplyStateTone(isReplied: boolean): ReviewBadgeTone {
  return isReplied ? 'success' : 'ember';
}

export function getReplyStatusLabel(status: number): string {
  return Number.isFinite(status) ? `Status ${status}` : 'Status -';
}

export function getReplyStatusTone(): ReviewBadgeTone {
  return 'neutral';
}

export function formatRating(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }

  return new Intl.NumberFormat('en', {
    maximumFractionDigits: 2
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

export function getReviewSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'dateCreate') {
    return `Created ${direction}`;
  }

  if (field === 'rating') {
    return `Rating ${direction}`;
  }

  return `${field} ${direction}`;
}

function isRecentlyCreated(value: string): boolean {
  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return false;
  }

  const age = Date.now() - date.getTime();
  return age >= 0 && age <= RECENT_DAYS * DAY_IN_MS;
}
