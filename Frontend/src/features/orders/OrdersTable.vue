<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  fieldValue,
  formatDateShort,
  formatNumber,
  getOrderStatusLabel,
  getOrderStatusTone
} from './orderDisplay';
import type { OrderListItem } from './orders.types';

const props = defineProps<{
  rows: OrderListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: OrderListItem];
}>();

const columns = [
  { key: 'dateOpened', label: 'Opened', sortable: true },
  { key: 'status', label: 'Status' },
  { key: 'idProduct', label: 'Product ID' },
  { key: 'route', label: 'Route' },
  { key: 'price', label: 'Price', sortable: true, align: 'right' },
  { key: 'discount', label: 'Discount', align: 'right' },
  { key: 'amount', label: 'Amount', sortable: true, align: 'right' },
  { key: 'dateUpdate', label: 'Updated', sortable: true },
  { key: 'id', label: 'Order ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="orders-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open order ${compactId(row.id)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-dateOpened="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
      </template>

      <template #cell-status="{ value }">
        <Badge :tone="getOrderStatusTone()">
          {{ getOrderStatusLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-idProduct="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-route="{ row }">
        <span class="route-cell">
          <strong>{{ fieldValue(row.locationSource) }}</strong>
          <span>{{ fieldValue(row.locationDestination) }}</span>
        </span>
      </template>

      <template #cell-price="{ value }">
        <span class="numeric">{{ formatNumber(Number(value)) }}</span>
      </template>

      <template #cell-discount="{ value }">
        <span class="numeric">{{ formatNumber(Number(value)) }}</span>
      </template>

      <template #cell-amount="{ value }">
        <span class="numeric">{{ formatNumber(Number(value)) }}</span>
      </template>

      <template #cell-dateUpdate="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
      </template>

      <template #cell-id="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>
    </DataTable>

    <footer class="orders-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="orders-table__pager">
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
.orders-table {
  overflow: hidden;
}

.code-cell {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
}

.route-cell {
  display: grid;
  min-width: 16rem;
  gap: 0.125rem;
}

.route-cell strong {
  color: var(--color-text);
  font-weight: 680;
}

.route-cell span,
.date-cell {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.orders-table__footer {
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

.orders-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
