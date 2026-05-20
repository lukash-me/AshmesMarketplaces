<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ExpenseDetailDrawer from './ExpenseDetailDrawer.vue';
import { getExpenseCategories, getExpenses } from './expenses.api';
import ExpensesFilters from './ExpensesFilters.vue';
import {
  parseExpensesQuery,
  removeExpenseQueryFilter,
  resetExpenseQueryFilters,
  toExpensesApiParams,
  toExpensesRouteQuery,
  type ExpenseQueryFilterKey
} from './expensesQuery';
import ExpensesTable from './ExpensesTable.vue';
import type {
  ExpenseCategoryListItem,
  ExpenseListItem,
  ExpenseQueryState
} from './expenses.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<ExpenseQueryState>(parseExpensesQuery(route.query));
const expenses = ref<ExpenseListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedExpense = ref<ExpenseListItem | null>(null);
const categories = ref<ExpenseCategoryListItem[]>([]);
const categoriesLoading = ref(false);
const categoriesError = ref('');

const categoriesById = computed(() =>
  categories.value.reduce<Record<string, ExpenseCategoryListItem>>((lookup, category) => {
    lookup[category.id] = category;
    return lookup;
  }, {})
);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseExpensesQuery(query);
    await loadExpenses();
  },
  { immediate: true }
);

void loadCategories();

async function loadExpenses() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getExpenses(toExpensesApiParams(queryState.value));
    expenses.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    expenses.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load expenses.');
  } finally {
    loading.value = false;
  }
}

async function loadCategories() {
  categoriesLoading.value = true;
  categoriesError.value = '';

  try {
    const response = await getExpenseCategories({
      page: 1,
      pageSize: 200,
      sort: 'name'
    });

    categories.value = response.items;
  } catch (err) {
    categories.value = [];
    categoriesError.value = getProblemMessage(err, 'Unable to load expense categories.');
  } finally {
    categoriesLoading.value = false;
  }
}

async function updateQuery(patch: Partial<ExpenseQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toExpensesRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toExpensesRouteQuery(resetExpenseQueryFilters(queryState.value))
  });
}

function removeFilter(key: ExpenseQueryFilterKey) {
  void router.replace({
    query: toExpensesRouteQuery(removeExpenseQueryFilter(queryState.value, key))
  });
}

function openExpense(row: ExpenseListItem) {
  selectedExpense.value = row;
}

function closeExpense() {
  selectedExpense.value = null;
}
</script>

<template>
  <div class="expenses-page">
    <PageHeader
      title="Expenses"
      description="Read-only operational seller expenses with categories, status codes and payment dates."
    />

    <ExpensesFilters
      :state="queryState"
      :categories-by-id="categoriesById"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <section v-if="categoriesError" class="expenses-page__notice app-surface">
      {{ categoriesError }} Category IDs are still shown from expense records.
    </section>

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Expenses could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="expenses.length === 0"
      class="app-surface"
      title="No expenses found"
      description="Adjust filters or load expenses through the existing backend API."
    />

    <ExpensesTable
      v-else
      :rows="expenses"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedExpense?.id"
      :categories-by-id="categoriesById"
      :categories-loading="categoriesLoading"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openExpense"
    />

    <ExpenseDetailDrawer
      :open="Boolean(selectedExpense)"
      :expense="selectedExpense"
      :categories-by-id="categoriesById"
      @close="closeExpense"
    />
  </div>
</template>

<style scoped>
.expenses-page {
  display: grid;
  gap: var(--space-4);
}

.expenses-page__notice {
  border-color: var(--state-warning-border);
  color: var(--state-warning);
  padding: var(--space-3);
  font-size: 0.8125rem;
}
</style>
