<script setup lang="ts" generic="T extends Record<string, unknown>">
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-vue-next';

export type DataTableColumn<T> = {
  key: string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'right';
  className?: string;
  value?: (row: T) => string | number | null | undefined;
};

type RowInteractive<T> = boolean | ((row: T) => boolean);

const props = defineProps<{
  rows: T[];
  columns: DataTableColumn<T>[];
  sort?: string | null;
  rowKey: (row: T) => string;
  rowClass?: (row: T) => string;
  rowInteractive?: RowInteractive<T>;
  rowAriaLabel?: (row: T) => string;
  selectedRowKey?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  rowClick: [row: T];
}>();

function getCellValue(row: T, column: DataTableColumn<T>) {
  return column.value ? column.value(row) : row[column.key];
}

function sortValue(column: DataTableColumn<T>): string {
  return props.sort === column.key ? `-${column.key}` : column.key;
}

function sortState(column: DataTableColumn<T>): 'asc' | 'desc' | 'none' {
  if (props.sort === column.key) {
    return 'asc';
  }

  if (props.sort === `-${column.key}`) {
    return 'desc';
  }

  return 'none';
}

function rowClasses(row: T): Array<string | undefined> {
  const key = props.rowKey(row);

  return [
    props.rowClass?.(row),
    isRowInteractive(row) ? 'table__row--interactive' : undefined,
    props.selectedRowKey === key ? 'table__row--selected' : undefined
  ];
}

function isRowInteractive(row: T): boolean {
  return typeof props.rowInteractive === 'function'
    ? props.rowInteractive(row)
    : Boolean(props.rowInteractive);
}
</script>

<template>
  <div class="table-wrap">
    <table class="table">
      <thead>
        <tr>
          <th
            v-for="column in columns"
            :key="column.key"
            :class="[column.align === 'right' ? 'table__cell--right' : '', column.className]"
          >
            <button
              v-if="column.sortable"
              class="table__sort"
              type="button"
              @click="emit('sort', sortValue(column))"
            >
              {{ column.label }}
              <ArrowUp v-if="sortState(column) === 'asc'" :size="14" />
              <ArrowDown v-else-if="sortState(column) === 'desc'" :size="14" />
              <ChevronsUpDown v-else :size="14" />
            </button>
            <span v-else>{{ column.label }}</span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="rowKey(row)"
          :class="rowClasses(row)"
          :tabindex="isRowInteractive(row) ? 0 : undefined"
          :role="isRowInteractive(row) ? 'button' : undefined"
          :aria-label="isRowInteractive(row) ? rowAriaLabel?.(row) : undefined"
          @click="isRowInteractive(row) && emit('rowClick', row)"
          @keydown.enter.prevent="isRowInteractive(row) && emit('rowClick', row)"
          @keydown.space.prevent="isRowInteractive(row) && emit('rowClick', row)"
        >
          <td
            v-for="column in columns"
            :key="column.key"
            :class="[column.align === 'right' ? 'table__cell--right' : '', column.className]"
          >
            <slot :name="`cell-${column.key}`" :row="row" :value="getCellValue(row, column)">
              {{ getCellValue(row, column) ?? '-' }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.table-wrap {
  width: 100%;
  overflow-x: auto;
}

.table {
  min-width: 980px;
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  font-size: 0.8125rem;
}

th {
  position: sticky;
  top: 0;
  z-index: 1;
  border-bottom: 1px solid var(--border-table-header);
  background: var(--surface-table-header-strong);
  color: var(--text-table-header);
  font-size: 0.6875rem;
  font-weight: 820;
  padding: 0.625rem 0.75rem;
  text-align: left;
  text-transform: uppercase;
  backdrop-filter: blur(14px);
  box-shadow: inset 0 -1px 0 var(--surface-highlight-overlay);
}

td {
  border-bottom: 1px solid var(--color-border);
  padding: 0.625rem 0.75rem;
  vertical-align: middle;
}

tbody tr:hover {
  background: var(--color-surface-hover);
}

tbody tr {
  transition: background-color 120ms ease, box-shadow 120ms ease;
}

tbody tr.table__row--interactive {
  cursor: pointer;
}

tbody tr.table__row--interactive:focus-visible {
  outline: none;
  background: var(--surface-active-overlay);
  box-shadow: inset 0 0 0 1px var(--color-border-strong);
}

tbody tr.table__row--selected {
  background: var(--surface-active-overlay);
}

tbody tr.table__row--selected td:first-child {
  box-shadow: inset 2px 0 0 var(--color-primary);
}

tbody tr.table__row--hot td:first-child {
  box-shadow: inset 2px 0 0 var(--color-heat-hot);
}

tbody tr.table__row--rising td:first-child {
  box-shadow: inset 2px 0 0 var(--color-heat-rising);
}

tbody tr.table__row--warm td:first-child {
  box-shadow: inset 2px 0 0 var(--heat-warm);
}

.table__cell--right {
  text-align: right;
}

.table__sort {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  color: inherit;
  border: 0;
  background: transparent;
  padding: 0;
  font-weight: inherit;
}

.table__sort svg {
  color: currentColor;
}
</style>
