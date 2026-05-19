<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import type { ProductListItem } from './products.types';

const props = defineProps<{
  rows: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
}>();

const columns = [
  { key: 'signal', label: 'Signal' },
  { key: 'name', label: 'Product', sortable: true },
  { key: 'skuSeller', label: 'Seller SKU', sortable: true },
  { key: 'idMp', label: 'Marketplace' },
  { key: 'status', label: 'Status' },
  { key: 'commission', label: 'Commission', align: 'right' },
  { key: 'dateUpdated', label: 'Updated', sortable: true }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));

type HeatTier = 'hot' | 'rising' | 'warm' | 'dormant';

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric'
  }).format(new Date(value));
}

function daysSince(value: string): number {
  const updated = new Date(value).getTime();

  if (!Number.isFinite(updated)) {
    return Number.POSITIVE_INFINITY;
  }

  return Math.max(0, Math.floor((Date.now() - updated) / 86_400_000));
}

function heatTier(row: ProductListItem): HeatTier {
  const age = daysSince(row.dateUpdated);

  if (row.status === 1 && age <= 7) {
    return 'hot';
  }

  if (row.status === 1 && age <= 21) {
    return 'rising';
  }

  if (row.status === 1 || row.status === 2) {
    return 'warm';
  }

  return 'dormant';
}

function heatLabel(row: ProductListItem): string {
  const tier = heatTier(row);

  if (tier === 'hot') {
    return 'Active';
  }

  if (tier === 'rising') {
    return 'Recent';
  }

  if (tier === 'warm') {
    return 'Watch';
  }

  return 'Quiet';
}

function heatTone(row: ProductListItem): 'neutral' | 'warning' | 'ember' | 'hot' {
  const tier = heatTier(row);

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

function rowClass(row: ProductListItem): string {
  return `table__row--${heatTier(row)}`;
}

function statusTone(status: number): 'success' | 'warning' | 'neutral' {
  if (status === 1) {
    return 'success';
  }

  if (status === 2) {
    return 'warning';
  }

  return 'neutral';
}
</script>

<template>
  <div class="products-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-class="rowClass"
      @sort="emit('sort', $event)"
    >
      <template #cell-signal="{ row }">
        <Badge
          :tone="heatTone(row)"
          :title="`Presentation signal derived from status and update recency: ${heatLabel(row)}`"
        >
          {{ heatLabel(row) }}
        </Badge>
      </template>

      <template #cell-name="{ row }">
        <div class="product-cell">
          <strong>{{ row.name }}</strong>
          <span>{{ row.skuProduct || row.idOnMp || 'No marketplace SKU' }}</span>
        </div>
      </template>

      <template #cell-skuSeller="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-idMp="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-status="{ value }">
        <Badge :tone="statusTone(Number(value))">Status {{ value }}</Badge>
      </template>

      <template #cell-commission="{ value }">
        <span class="numeric">{{ value ?? '-' }}</span>
      </template>

      <template #cell-dateUpdated="{ value }">
        <span class="numeric">{{ formatDate(String(value)) }}</span>
      </template>
    </DataTable>

    <footer class="products-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="products-table__pager">
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
.products-table {
  overflow: hidden;
}

.product-cell {
  display: grid;
  gap: 0.125rem;
  min-width: 18rem;
}

.product-cell strong {
  font-weight: 700;
  line-height: 1.25;
}

.product-cell span,
.code-cell {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.code-cell {
  font-family: var(--font-mono);
  word-break: break-all;
}

.products-table__footer {
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

.products-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
