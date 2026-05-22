<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import type { ParserReviewListItem } from './parserReviews.types';

const props = defineProps<{
  rows: ParserReviewListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ParserReviewListItem];
}>();

const columns = [
  { key: 'rating', label: 'Rating', sortable: true, align: 'right' },
  { key: 'textPreview', label: 'Review text', className: 'table__cell--review' },
  { key: 'wbProductId', label: 'WB product' },
  { key: 'sourceWbRootId', label: 'Source root' },
  { key: 'reviewAttributionMode', label: 'Attribution' },
  { key: 'hasObservedReply', label: 'Reply' },
  { key: 'createdAtOnMp', label: 'Created', sortable: true },
  { key: 'parsedAtUtc', label: 'Completeness', sortable: true },
  { key: 'parserRunId', label: 'Parser run' }
] as const;

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));

function formatDate(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('en', {
        month: 'short',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : '-';
}

function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}
</script>

<template>
  <div class="parser-reviews-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open staged parser review ${row.reviewIdOnMp}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-rating="{ value }">
        <strong class="numeric">{{ fieldValue(value as number | null) }}</strong>
      </template>

      <template #cell-textPreview="{ value, row }">
        <span class="review-text" :title="fieldValue(value as string | null)">
          {{ fieldValue(value as string | null) }}
        </span>
        <code class="review-id">{{ row.reviewIdOnMp }}</code>
      </template>

      <template #cell-wbProductId="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-sourceWbRootId="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-reviewAttributionMode="{ value }">
        <Badge tone="info" title="Review attribution comes from parser staging evidence">
          {{ value }}
        </Badge>
      </template>

      <template #cell-hasObservedReply="{ value }">
        <Badge :tone="value ? 'success' : 'neutral'">
          {{ value ? 'Observed reply' : 'No reply observed' }}
        </Badge>
      </template>

      <template #cell-createdAtOnMp="{ value }">
        <span class="date-cell numeric">{{ formatDate(value ? String(value) : null) }}</span>
      </template>

      <template #cell-parsedAtUtc="{ row }">
        <div class="flag-stack">
          <Badge v-if="row.isPartialSnapshot" tone="warning">Partial snapshot</Badge>
          <Badge v-if="row.isCappedRootPayload" tone="ember">Root payload capped</Badge>
          <Badge v-if="row.isFullHistoryUnknown" tone="neutral">Full history unknown</Badge>
        </div>
      </template>

      <template #cell-parserRunId="{ value }">
        <code class="run-cell" :title="String(value)">{{ value }}</code>
      </template>
    </DataTable>

    <footer class="table-footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="pager">
        <Button variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">Previous</Button>
        <span class="numeric">Page {{ page }} / {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">Next</Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.parser-reviews-table {
  overflow: hidden;
}

.review-text {
  display: -webkit-box;
  max-width: 24rem;
  overflow: hidden;
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.review-id,
.code-cell,
.run-cell {
  display: block;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.74rem;
  white-space: nowrap;
}

.run-cell {
  max-width: 12rem;
  overflow: hidden;
  text-overflow: ellipsis;
}

.date-cell {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.flag-stack {
  display: flex;
  max-width: 14rem;
  flex-wrap: wrap;
  gap: var(--space-1);
}

.table-footer {
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

.pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
