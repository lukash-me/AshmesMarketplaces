<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  formatDateShort,
  formatNumber,
  getLogisticTypeLabel,
  getNeutralTone
} from './logisticDisplay';
import type { LogisticListItem } from './logistics.types';

const props = defineProps<{
  rows: LogisticListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: LogisticListItem];
}>();

const columns = [
  { key: 'date', label: 'Date', sortable: true },
  { key: 'type', label: 'Type' },
  { key: 'idProduct', label: 'Product ID' },
  { key: 'idWarehouse', label: 'Warehouse ID' },
  { key: 'stockAmount', label: 'Stock', align: 'right' },
  { key: 'stockAmountStatistic', label: 'Stock statistic', sortable: true, align: 'right' },
  { key: 'stockInTransit', label: 'In transit', align: 'right' },
  { key: 'costStorage', label: 'Storage cost', align: 'right' },
  { key: 'costLogistic', label: 'Logistic cost', align: 'right' },
  { key: 'id', label: 'Logistic ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="logistics-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open logistic ${compactId(row.id)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-date="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
      </template>

      <template #cell-type="{ value }">
        <Badge :tone="getNeutralTone()">
          {{ getLogisticTypeLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-idProduct="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-idWarehouse="{ value }">
        <code class="code-cell" :title="value ? String(value) : ''">{{ compactId(value as string | null) }}</code>
      </template>

      <template #cell-stockAmount="{ value }">
        <span class="numeric">{{ formatNumber(value as number | null) }}</span>
      </template>

      <template #cell-stockAmountStatistic="{ value }">
        <span class="numeric">{{ formatNumber(Number(value)) }}</span>
      </template>

      <template #cell-stockInTransit="{ value }">
        <span class="numeric">{{ formatNumber(value as number | null) }}</span>
      </template>

      <template #cell-costStorage="{ value }">
        <span class="numeric">{{ formatNumber(value as number | null) }}</span>
      </template>

      <template #cell-costLogistic="{ value }">
        <span class="numeric">{{ formatNumber(value as number | null) }}</span>
      </template>

      <template #cell-id="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>
    </DataTable>

    <footer class="logistics-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="logistics-table__pager">
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
.logistics-table {
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

.logistics-table__footer {
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

.logistics-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
