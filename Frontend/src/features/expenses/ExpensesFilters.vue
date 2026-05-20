<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  compactId,
  getExpenseCategoryLabel,
  getExpenseSortLabel,
  getExpenseStatusLabel,
  type ExpenseCategoryLookup
} from './expenseDisplay';
import type { ExpenseQueryFilterKey } from './expensesQuery';
import type { ExpenseQueryState } from './expenses.types';

const props = defineProps<{
  state: ExpenseQueryState;
  categoriesById: ExpenseCategoryLookup;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ExpenseQueryState>];
  reset: [];
  remove: [key: ExpenseQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idCategory: props.state.idCategory,
  status: props.state.status,
  idWorkspace: props.state.idWorkspace,
  idCreator: props.state.idCreator,
  idResponsible: props.state.idResponsible,
  datePayFrom: props.state.datePayFrom,
  datePayTo: props.state.datePayTo,
  dateCreateFrom: props.state.dateCreateFrom,
  dateCreateTo: props.state.dateCreateTo
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idCategory = state.idCategory;
    form.status = state.status;
    form.idWorkspace = state.idWorkspace;
    form.idCreator = state.idCreator;
    form.idResponsible = state.idResponsible;
    form.datePayFrom = state.datePayFrom;
    form.datePayTo = state.datePayTo;
    form.dateCreateFrom = state.dateCreateFrom;
    form.dateCreateTo = state.dateCreateTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: ExpenseQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Search', value: props.state.search });
  }

  if (props.state.idCategory) {
    chips.push({
      key: 'idCategory',
      label: 'Category',
      value: getExpenseCategoryLabel(props.state.idCategory, props.categoriesById)
    });
  }

  if (props.state.status) {
    chips.push({
      key: 'status',
      label: 'Status',
      value: getExpenseStatusLabel(Number(props.state.status))
    });
  }

  if (props.state.idWorkspace) {
    chips.push({
      key: 'idWorkspace',
      label: 'Workspace',
      value: compactId(props.state.idWorkspace)
    });
  }

  if (props.state.idCreator) {
    chips.push({ key: 'idCreator', label: 'Creator', value: compactId(props.state.idCreator) });
  }

  if (props.state.idResponsible) {
    chips.push({
      key: 'idResponsible',
      label: 'Responsible',
      value: compactId(props.state.idResponsible)
    });
  }

  if (props.state.datePayFrom) {
    chips.push({ key: 'datePayFrom', label: 'Paid from', value: props.state.datePayFrom });
  }

  if (props.state.datePayTo) {
    chips.push({ key: 'datePayTo', label: 'Paid to', value: props.state.datePayTo });
  }

  if (props.state.dateCreateFrom) {
    chips.push({ key: 'dateCreateFrom', label: 'Created from', value: props.state.dateCreateFrom });
  }

  if (props.state.dateCreateTo) {
    chips.push({ key: 'dateCreateTo', label: 'Created to', value: props.state.dateCreateTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getExpenseSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idCategory: form.idCategory.trim(),
    status: form.status.trim(),
    idWorkspace: form.idWorkspace.trim(),
    idCreator: form.idCreator.trim(),
    idResponsible: form.idResponsible.trim(),
    datePayFrom: form.datePayFrom,
    datePayTo: form.datePayTo,
    dateCreateFrom: form.dateCreateFrom,
    dateCreateTo: form.dateCreateTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Name, description or category" />

      <Input v-model="form.status" type="number" label="Status code" placeholder="0+" />

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">
          Reset
        </Button>
      </div>
    </div>

    <div class="filters__ids">
      <Input v-model="form.idCategory" label="Category ID" placeholder="uuid" />
      <Input v-model="form.idWorkspace" label="Workspace ID" placeholder="uuid" />
      <Input v-model="form.idCreator" label="Creator ID" placeholder="uuid" />
      <Input v-model="form.idResponsible" label="Responsible ID" placeholder="uuid" />
    </div>

    <div class="filters__dates">
      <Input v-model="form.datePayFrom" type="date" label="Paid from" />
      <Input v-model="form.datePayTo" type="date" label="Paid to" />
      <Input v-model="form.dateCreateFrom" type="date" label="Created from" />
      <Input v-model="form.dateCreateTo" type="date" label="Created to" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active expense filters">
      <button
        v-for="chip in activeChips"
        :key="chip.key"
        class="filters__chip"
        type="button"
        :title="`Remove ${chip.label}`"
        @click="emit('remove', chip.key)"
      >
        <span>{{ chip.label }}</span>
        <strong>{{ chip.value }}</strong>
        <span aria-hidden="true">x</span>
      </button>
    </div>
  </form>
</template>

<style scoped>
.filters {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-3);
}

.filters__toolbar,
.filters__ids,
.filters__dates {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

.filters__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filters__chip {
  display: inline-flex;
  max-width: 100%;
  align-items: center;
  gap: var(--space-1);
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--color-text-muted);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
  line-height: 1;
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
  color: var(--color-text);
  outline: none;
}

.filters__chip strong {
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
}

@media (min-width: 840px) {
  .filters__toolbar {
    grid-template-columns: minmax(18rem, 1fr) minmax(8rem, 0.2fr) auto;
    align-items: end;
  }

  .filters__ids,
  .filters__dates {
    grid-template-columns: repeat(4, minmax(10rem, 1fr));
  }
}
</style>
