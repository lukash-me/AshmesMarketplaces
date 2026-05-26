<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  expenseStatusOptions,
  getExpenseCategoryLabel,
  getExpenseSortLabel,
  getExpenseStatusLabel,
  getUserLabel,
  type ExpenseCategoryLookup,
  type ExpenseUserLookup
} from './expenseDisplay';
import type { ExpenseQueryFilterKey } from './expensesQuery';
import type { ExpenseQueryState } from './expenses.types';

const props = defineProps<{
  state: ExpenseQueryState;
  categoriesById: ExpenseCategoryLookup;
  usersById: ExpenseUserLookup;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ExpenseQueryState>];
  reset: [];
  remove: [key: ExpenseQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idCategory: props.state.idCategory,
  statusKey: props.state.statusKey,
  idResponsible: props.state.idResponsible,
  datePayFrom: props.state.datePayFrom,
  datePayTo: props.state.datePayTo,
  amountFrom: props.state.amountFrom,
  amountTo: props.state.amountTo
});
const advancedOpen = ref(false);

const categories = computed(() => Object.values(props.categoriesById).sort((a, b) => a.name.localeCompare(b.name, 'ru-RU')));
const users = computed(() => Object.values(props.usersById).sort((a, b) => a.login.localeCompare(b.login, 'ru-RU')));

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idCategory = state.idCategory;
    form.statusKey = state.statusKey;
    form.idResponsible = state.idResponsible;
    form.datePayFrom = state.datePayFrom;
    form.datePayTo = state.datePayTo;
    form.amountFrom = state.amountFrom;
    form.amountTo = state.amountTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: ExpenseQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Поиск', value: props.state.search });
  }

  if (props.state.idCategory) {
    chips.push({
      key: 'idCategory',
      label: 'Категория',
      value: getExpenseCategoryLabel(props.state.idCategory, props.categoriesById)
    });
  }

  if (props.state.statusKey) {
    chips.push({
      key: 'statusKey',
      label: 'Статус',
      value: getExpenseStatusLabel(props.state.statusKey)
    });
  }

  if (props.state.idResponsible) {
    chips.push({
      key: 'idResponsible',
      label: 'Ответственный',
      value: getUserLabel(props.state.idResponsible, props.usersById)
    });
  }

  if (props.state.datePayFrom) {
    chips.push({ key: 'datePayFrom', label: 'Дата оплаты с', value: props.state.datePayFrom });
  }

  if (props.state.datePayTo) {
    chips.push({ key: 'datePayTo', label: 'Дата оплаты по', value: props.state.datePayTo });
  }

  if (props.state.amountFrom) {
    chips.push({ key: 'amountFrom', label: 'Сумма от', value: props.state.amountFrom });
  }

  if (props.state.amountTo) {
    chips.push({ key: 'amountTo', label: 'Сумма до', value: props.state.amountTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Сортировка', value: getExpenseSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);
const advancedActiveCount = computed(() =>
  [
    props.state.idCategory,
    props.state.statusKey,
    props.state.idResponsible,
    props.state.datePayFrom,
    props.state.datePayTo,
    props.state.amountFrom,
    props.state.amountTo
  ].filter(Boolean).length
);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idCategory: form.idCategory,
    statusKey: form.statusKey,
    idResponsible: form.idResponsible,
    datePayFrom: form.datePayFrom,
    datePayTo: form.datePayTo,
    amountFrom: form.amountFrom.trim(),
    amountTo: form.amountTo.trim()
  });
  advancedOpen.value = false;
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__top">
      <Input
        v-model="form.search"
        class="filter-control"
        :class="{ 'filter-control--active': form.search.trim() }"
        label="Поиск"
        placeholder="Найти расход"
      />

      <div class="filters__actions">
        <Button
          type="button"
          variant="secondary"
          :aria-expanded="advancedOpen"
          aria-controls="expenses-more-filters"
          @click="advancedOpen = !advancedOpen"
        >
          {{ advancedActiveCount ? `Больше фильтров (${advancedActiveCount})` : 'Больше фильтров' }}
        </Button>
        <Button type="submit" variant="primary">Применить</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">
          Сбросить
        </Button>
      </div>
    </div>

    <div
      v-if="advancedOpen"
      id="expenses-more-filters"
      class="filters__popover app-surface"
      role="region"
      aria-label="Дополнительные фильтры расходов"
    >
      <div class="filters__grid">
        <label class="select-field filter-control" :class="{ 'filter-control--active': form.idCategory }">
          <span>Категория</span>
          <select v-model="form.idCategory">
            <option value="">Все категории</option>
            <option v-for="category in categories" :key="category.id" :value="category.id">
              {{ category.name }}
            </option>
          </select>
        </label>

        <label class="select-field filter-control" :class="{ 'filter-control--active': form.statusKey }">
          <span>Статус</span>
          <select v-model="form.statusKey">
            <option value="">Все статусы</option>
            <option v-for="status in expenseStatusOptions" :key="status.key" :value="status.key">
              {{ status.label }}
            </option>
          </select>
        </label>

        <label class="select-field filter-control" :class="{ 'filter-control--active': form.idResponsible }">
          <span>Ответственный</span>
          <select v-model="form.idResponsible">
            <option value="">Все ответственные</option>
            <option v-for="user in users" :key="user.id" :value="user.id">
              {{ user.login || user.email || 'Пользователь' }}
            </option>
          </select>
        </label>

        <Input
          v-model="form.datePayFrom"
          class="filter-control"
          :class="{ 'filter-control--active': form.datePayFrom }"
          type="date"
          label="Дата оплаты с"
        />
        <Input
          v-model="form.datePayTo"
          class="filter-control"
          :class="{ 'filter-control--active': form.datePayTo }"
          type="date"
          label="Дата оплаты по"
        />
        <Input
          v-model="form.amountFrom"
          class="filter-control"
          :class="{ 'filter-control--active': form.amountFrom }"
          type="number"
          label="Сумма от"
        />
        <Input
          v-model="form.amountTo"
          class="filter-control"
          :class="{ 'filter-control--active': form.amountTo }"
          type="number"
          label="Сумма до"
        />
      </div>
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Активные фильтры расходов">
      <button
        v-for="chip in activeChips"
        :key="chip.key"
        class="filters__chip"
        type="button"
        :title="`Убрать ${chip.label}`"
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
  position: relative;
  display: grid;
  gap: var(--space-3);
  padding: var(--space-3);
}

.filters__top,
.filters__grid {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filters__popover {
  padding: var(--space-3);
  border-color: var(--color-border);
  background: rgb(17 21 29);
  box-shadow: var(--shadow-panel);
  backdrop-filter: none;
}

.select-field {
  display: grid;
  gap: var(--space-1);
}

.select-field span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.select-field select {
  height: 2.25rem;
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
  outline: none;
}

.select-field select:focus {
  border-color: var(--color-primary);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
}

.filter-control--active :deep(.field__control),
.filter-control--active select {
  border-color: var(--accent-ember-border);
  background: var(--surface-control-focus);
}

.filter-control--active :deep(.field__label),
.filter-control--active span {
  color: var(--accent-ember-text);
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
  border: 1px solid var(--accent-ember-border);
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--accent-ember-text);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
  line-height: 1;
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--accent-primary-hover-border);
  background: var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
  outline: none;
}

.filters__chip strong {
  color: var(--color-text);
  font-weight: 650;
}

@media (min-width: 840px) {
  .filters__top {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__grid {
    grid-template-columns: repeat(4, minmax(10rem, 1fr));
  }
}
</style>
