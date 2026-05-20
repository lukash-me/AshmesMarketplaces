<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  formatAmount,
  formatDateShort,
  getExpenseCategoryLabel,
  getExpenseNeutralTone,
  getExpenseStatusLabel,
  type ExpenseCategoryLookup
} from './expenseDisplay';
import type { ExpenseListItem } from './expenses.types';

const props = defineProps<{
  rows: ExpenseListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
  categoriesById: ExpenseCategoryLookup;
  categoriesLoading: boolean;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ExpenseListItem];
}>();

const columns = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'status', label: 'Status' },
  { key: 'category', label: 'Category' },
  { key: 'cost', label: 'Cost', sortable: true, align: 'right' },
  { key: 'datePay', label: 'Paid', sortable: true },
  { key: 'dateCreate', label: 'Created', sortable: true },
  { key: 'dateUpdate', label: 'Updated', sortable: true },
  { key: 'id', label: 'Expense ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="expenses-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open expense ${row.name}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-name="{ row }">
        <span class="name-cell">
          <strong>{{ row.name }}</strong>
          <code :title="row.idWorkspace">WS {{ compactId(row.idWorkspace) }}</code>
        </span>
      </template>

      <template #cell-status="{ value }">
        <Badge class="status-badge" :tone="getExpenseNeutralTone()">
          {{ getExpenseStatusLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-category="{ row }">
        <span class="category-cell">
          <span>{{ categoriesLoading ? 'Loading categories' : getExpenseCategoryLabel(row.idCategory, categoriesById) }}</span>
          <code v-if="row.idCategory" :title="row.idCategory">{{ compactId(row.idCategory) }}</code>
        </span>
      </template>

      <template #cell-cost="{ value }">
        <span class="numeric">{{ formatAmount(value as number | null) }}</span>
      </template>

      <template #cell-datePay="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(value as string | null) }}</span>
        </span>
      </template>

      <template #cell-dateCreate="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
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

    <footer class="expenses-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="expenses-table__pager">
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
.expenses-table {
  overflow: hidden;
}

.name-cell,
.category-cell {
  display: grid;
  min-width: 12rem;
  gap: 0.125rem;
}

.name-cell strong {
  color: var(--color-text);
  font-weight: 680;
}

.name-cell code,
.category-cell code,
.code-cell {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
}

.category-cell span,
.date-cell {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.status-badge {
  white-space: nowrap;
}

.expenses-table__footer {
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

.expenses-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
