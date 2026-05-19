<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import ProductSignalBadge from './ProductSignalBadge.vue';
import {
  getDaysSince,
  getProductHeatTier,
  getProductStatusLabel,
  getProductStatusTone
} from './productSignals';
import type { ProductListItem } from './products.types';

const props = defineProps<{
  rows: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ProductListItem];
}>();

const columns = [
  { key: 'signal', label: 'Signal', className: 'table__cell--signal' },
  { key: 'name', label: 'Product', sortable: true, className: 'table__cell--product' },
  { key: 'skuSeller', label: 'Seller SKU', sortable: true },
  { key: 'status', label: 'Status' },
  { key: 'commission', label: 'Commission', align: 'right' },
  { key: 'dateUpdated', label: 'Updated', sortable: true },
  { key: 'ids', label: 'IDs' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric'
  }).format(new Date(value));
}

function rowClass(row: ProductListItem): string {
  return `table__row--${getProductHeatTier(row)}`;
}

function formatAge(value: string): string {
  const days = getDaysSince(value);

  if (!Number.isFinite(days)) {
    return 'Unknown age';
  }

  if (days === 0) {
    return 'Updated today';
  }

  return `${days}d ago`;
}

function compactId(value: string | null): string {
  if (!value) {
    return '-';
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
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
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open product ${row.name}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-signal="{ row }">
        <ProductSignalBadge :product="row" />
      </template>

      <template #cell-name="{ row }">
        <div class="product-cell">
          <strong>{{ row.name }}</strong>
          <span>
            {{ row.skuProduct || row.idOnMp || 'No marketplace SKU' }}
            <template v-if="row.barcode"> / {{ row.barcode }}</template>
          </span>
        </div>
      </template>

      <template #cell-skuSeller="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-status="{ value }">
        <Badge :tone="getProductStatusTone(Number(value))">
          {{ getProductStatusLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-commission="{ value }">
        <span class="numeric">{{ value ?? '-' }}</span>
      </template>

      <template #cell-dateUpdated="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDate(String(value)) }}</span>
          <small>{{ formatAge(String(value)) }}</small>
        </span>
      </template>

      <template #cell-ids="{ row }">
        <span class="ids-cell">
          <code :title="row.idMp">MP {{ compactId(row.idMp) }}</code>
          <code v-if="row.idBrand" :title="row.idBrand">BR {{ compactId(row.idBrand) }}</code>
          <code v-if="row.idCategory" :title="row.idCategory">CT {{ compactId(row.idCategory) }}</code>
        </span>
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
  min-width: 20rem;
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
  white-space: nowrap;
}

.date-cell,
.ids-cell {
  display: grid;
  gap: 0.125rem;
}

.date-cell small {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.ids-cell code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
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
