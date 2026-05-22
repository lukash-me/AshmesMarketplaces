<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import type { ParserProductListItem } from './parserProducts.types';

const props = defineProps<{
  rows: ParserProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ParserProductListItem];
}>();

const columns = [
  { key: 'thumbnailUrl', label: 'Image' },
  { key: 'wbProductId', label: 'WB product', sortable: true },
  { key: 'name', label: 'Name', sortable: true, className: 'table__cell--name' },
  { key: 'brandName', label: 'Brand' },
  { key: 'sellerName', label: 'Seller' },
  { key: 'priceDiscounted', label: 'Prices', sortable: true, align: 'right' },
  { key: 'reviewRating', label: 'Rating', sortable: true, align: 'right' },
  { key: 'sourceCategory', label: 'Source' },
  { key: 'parsedAtUtc', label: 'Parsed', sortable: true },
  { key: 'parserRunId', label: 'Parser run' }
] as const;

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));

function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

function formatMoney(value: number | null): string {
  return value === null
    ? '-'
    : new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value: string): string {
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
</script>

<template>
  <div class="parser-products-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open staged parser product ${row.wbProductId}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-thumbnailUrl="{ row }">
        <img v-if="row.thumbnailUrl" class="thumb" :src="row.thumbnailUrl" :alt="row.name" />
        <span v-else class="thumb thumb--empty">-</span>
      </template>

      <template #cell-wbProductId="{ value, row }">
        <code class="code-cell">{{ value }}</code>
        <span v-if="row.wbRootId" class="subtle code-cell">root {{ row.wbRootId }}</span>
      </template>

      <template #cell-name="{ value, row }">
        <strong class="name-cell">{{ value }}</strong>
        <span class="subtle">{{ fieldValue(row.sourceQuery) }}</span>
      </template>

      <template #cell-brandName="{ value }">
        {{ fieldValue(value as string | null) }}
      </template>

      <template #cell-sellerName="{ value }">
        {{ fieldValue(value as string | null) }}
      </template>

      <template #cell-priceDiscounted="{ row }">
        <div class="money-cell numeric">
          <strong>{{ formatMoney(row.priceDiscounted) }}</strong>
          <span>regular {{ formatMoney(row.priceRegular) }}</span>
          <span>wallet {{ formatMoney(row.priceWbWallet) }}</span>
        </div>
      </template>

      <template #cell-reviewRating="{ row }">
        <div class="rating-cell numeric">
          <strong>{{ fieldValue(row.reviewRating) }}</strong>
          <span>{{ fieldValue(row.feedbackCount) }} feedback</span>
        </div>
      </template>

      <template #cell-sourceCategory="{ row }">
        <Badge tone="info">{{ fieldValue(row.sourceCategory) }}</Badge>
        <span class="subtle">{{ fieldValue(row.sourceSubcategory) }}</span>
      </template>

      <template #cell-parsedAtUtc="{ value }">
        <span class="date-cell numeric">{{ formatDate(String(value)) }}</span>
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
.parser-products-table {
  overflow: hidden;
}

.thumb {
  display: grid;
  height: 3rem;
  width: 2.5rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  object-fit: cover;
}

.thumb--empty {
  color: var(--color-text-subtle);
}

.name-cell,
.subtle,
.money-cell,
.rating-cell {
  display: block;
}

.name-cell {
  max-width: 24rem;
  overflow: hidden;
  line-height: 1.3;
  text-overflow: ellipsis;
}

.subtle,
.date-cell {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

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

.money-cell,
.rating-cell {
  color: var(--color-text-muted);
  font-size: 0.74rem;
}

.money-cell strong,
.rating-cell strong {
  color: var(--color-text);
  font-size: 0.8125rem;
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
