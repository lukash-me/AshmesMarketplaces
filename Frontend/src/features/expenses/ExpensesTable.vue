<script setup lang="ts">
import { computed } from 'vue';
import { Eye, Pencil } from 'lucide-vue-next';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  formatAmount,
  formatDateShort,
  getExpenseCategoryLabel,
  getExpenseStatusLabel,
  getExpenseStatusTone,
  getUserLabel,
  type ExpenseCategoryLookup,
  type ExpenseUserLookup
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
  usersById: ExpenseUserLookup;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ExpenseListItem];
  edit: [row: ExpenseListItem];
}>();

const columns = [
  { key: 'name', label: 'Расход', sortable: true },
  { key: 'category', label: 'Категория' },
  { key: 'cost', label: 'Сумма', sortable: true, align: 'right' },
  { key: 'status', label: 'Статус' },
  { key: 'datePay', label: 'Дата оплаты', sortable: true },
  { key: 'responsible', label: 'Ответственный' },
  { key: 'creator', label: 'Создал' },
  { key: 'dateUpdate', label: 'Обновлено', sortable: true },
  { key: 'actions', label: '' }
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
      :row-aria-label="(row) => `Открыть расход ${row.name}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-name="{ row }">
        <span class="name-cell">
          <strong>{{ row.name }}</strong>
          <small>{{ row.workspaceName || 'Операционный расход' }}</small>
        </span>
      </template>

      <template #cell-category="{ row }">
        {{ getExpenseCategoryLabel(row.idCategory, categoriesById, row.categoryName) }}
      </template>

      <template #cell-cost="{ row }">
        <span class="numeric amount-cell">{{ formatAmount(row.cost) }}</span>
      </template>

      <template #cell-status="{ row }">
        <Badge class="status-badge" :tone="getExpenseStatusTone(row.statusKey)">
          {{ getExpenseStatusLabel(row.statusKey, row.statusLabel) }}
        </Badge>
      </template>

      <template #cell-datePay="{ row }">
        <span class="numeric">{{ formatDateShort(row.datePay) }}</span>
      </template>

      <template #cell-responsible="{ row }">
        {{ getUserLabel(row.idResponsible, usersById, row.responsibleLogin, row.responsibleEmail) }}
      </template>

      <template #cell-creator="{ row }">
        {{ getUserLabel(row.idCreator, usersById, row.creatorLogin, row.creatorEmail) }}
      </template>

      <template #cell-dateUpdate="{ row }">
        <span class="numeric">{{ formatDateShort(row.dateUpdate) }}</span>
      </template>

      <template #cell-actions="{ row }">
        <div class="row-actions" @click.stop>
          <button class="app-icon-button" type="button" title="Открыть" @click="emit('open', row)">
            <Eye :size="15" />
          </button>
          <button class="app-icon-button" type="button" title="Редактировать" @click="emit('edit', row)">
            <Pencil :size="15" />
          </button>
        </div>
      </template>
    </DataTable>

    <footer class="expenses-table__footer">
      <span class="numeric">Показано {{ pageStart }}-{{ pageEnd }} из {{ totalCount }}</span>
      <div class="expenses-table__pager">
        <Button variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">
          Назад
        </Button>
        <span class="numeric">Страница {{ page }} / {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">
          Вперед
        </Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.expenses-table {
  overflow: hidden;
}

.name-cell {
  display: grid;
  min-width: 13rem;
  gap: 0.125rem;
}

.name-cell strong {
  color: var(--color-text);
  font-weight: 700;
}

.name-cell small {
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.amount-cell {
  font-weight: 720;
}

.status-badge {
  white-space: nowrap;
}

.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-1);
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
