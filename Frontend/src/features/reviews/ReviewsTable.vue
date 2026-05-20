<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  fieldValue,
  formatDateShort,
  formatRating,
  getReplyStateLabel,
  getReplyStateTone,
  getReviewSignal
} from './reviewDisplay';
import type { ReviewListItem } from './reviews.types';

const props = defineProps<{
  rows: ReviewListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ReviewListItem];
}>();

const columns = [
  { key: 'signal', label: 'Signal', className: 'table__cell--signal' },
  { key: 'dateCreate', label: 'Created', sortable: true },
  { key: 'rating', label: 'Rating', sortable: true, align: 'right' },
  { key: 'isReplied', label: 'Reply' },
  { key: 'text', label: 'Review text', className: 'table__cell--review-text' },
  { key: 'idProduct', label: 'Product ID' },
  { key: 'idOnMp', label: 'Marketplace ID' },
  { key: 'dateReply', label: 'Reply date' },
  { key: 'id', label: 'Review ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="reviews-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open review ${compactId(row.id)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-signal="{ row }">
        <Badge :tone="getReviewSignal(row).tone">
          {{ getReviewSignal(row).label }}
        </Badge>
      </template>

      <template #cell-dateCreate="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
      </template>

      <template #cell-rating="{ value }">
        <span class="numeric rating-cell">{{ formatRating(Number(value)) }}</span>
      </template>

      <template #cell-isReplied="{ value }">
        <Badge :tone="getReplyStateTone(Boolean(value))">
          {{ getReplyStateLabel(Boolean(value)) }}
        </Badge>
      </template>

      <template #cell-text="{ value }">
        <span class="review-text" :title="fieldValue(value as string | null)">
          {{ fieldValue(value as string | null) }}
        </span>
      </template>

      <template #cell-idProduct="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-idOnMp="{ value }">
        <code class="code-cell" :title="String(value)">{{ value }}</code>
      </template>

      <template #cell-dateReply="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(value ? String(value) : null) }}</span>
        </span>
      </template>

      <template #cell-id="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>
    </DataTable>

    <footer class="reviews-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="reviews-table__pager">
        <Button variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">
          Previous
        </Button>
        <span class="numeric">Page {{ page }} / {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">
          Next
        </Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.reviews-table {
  overflow: hidden;
}

.code-cell {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
}

.date-cell {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.rating-cell {
  font-weight: 720;
}

.review-text {
  display: -webkit-box;
  max-width: 24rem;
  overflow: hidden;
  color: var(--color-text);
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.reviews-table__footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.reviews-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
