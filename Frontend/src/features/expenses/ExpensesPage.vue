<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import { useActiveWorkspace } from '@/features/workspace-market-products/useActiveWorkspace';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import KpiGrid from '@/widgets/KpiGrid.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ExpenseDetailDrawer from './ExpenseDetailDrawer.vue';
import ExpenseFormDrawer from './ExpenseFormDrawer.vue';
import {
  createExpense,
  getExpense,
  getExpenseCategories,
  getExpenses,
  getExpenseSummary,
  updateExpense
} from './expenses.api';
import { formatAmount, formatDateShort, getExpenseStatusNumber } from './expenseDisplay';
import ExpensesFilters from './ExpensesFilters.vue';
import {
  parseExpensesQuery,
  removeExpenseQueryFilter,
  resetExpenseQueryFilters,
  toExpensesApiParams,
  toExpensesRouteQuery,
  toExpensesSummaryParams,
  type ExpenseQueryFilterKey
} from './expensesQuery';
import ExpensesTable from './ExpensesTable.vue';
import type {
  ExpenseCategoryListItem,
  ExpenseDetail,
  ExpenseListItem,
  ExpenseQueryState,
  ExpenseStatusKey,
  ExpenseSummary,
  ExpenseUserListItem,
  SaveExpenseRequest
} from './expenses.types';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const workspace = useActiveWorkspace();

const queryState = ref<ExpenseQueryState>(parseExpensesQuery(route.query));
const expenses = ref<ExpenseListItem[]>([]);
const totalCount = ref(0);
const summary = ref<ExpenseSummary | null>(null);
const loading = ref(false);
const summaryLoading = ref(false);
const error = ref('');
const summaryError = ref('');
const selectedExpense = ref<ExpenseListItem | null>(null);
const categories = ref<ExpenseCategoryListItem[]>([]);
const users = ref<ExpenseUserListItem[]>([]);
const lookupError = ref('');
const formOpen = ref(false);
const formMode = ref<'create' | 'edit'>('create');
const formExpense = ref<ExpenseDetail | null>(null);
const formSaving = ref(false);
const formError = ref('');
const actionLoading = ref(false);
const toastMessage = ref('');
let toastTimer: number | undefined;

const categoriesById = computed(() =>
  categories.value.reduce<Record<string, ExpenseCategoryListItem>>((lookup, category) => {
    lookup[category.id] = category;
    return lookup;
  }, {})
);

const usersById = computed(() =>
  users.value.reduce<Record<string, ExpenseUserListItem>>((lookup, user) => {
    lookup[user.id] = user;
    return lookup;
  }, {})
);

const currentWorkspaceId = computed(() => workspace.activeWorkspaceId.value ?? '');
const currentUserId = computed(() => auth.user?.id ?? '');
const isGuest = computed(() => !auth.isAuthenticated);

const kpiItems = computed(() => [
  {
    label: 'Всего расходов',
    value: summary.value ? summary.value.totalCount : '...',
    caption: summary.value ? formatAmount(summary.value.totalAmount) : 'Загрузка'
  },
  {
    label: 'К оплате',
    value: summary.value ? summary.value.pendingPaymentCount : '...',
    caption: summary.value ? formatAmount(summary.value.pendingPaymentAmount) : 'Загрузка'
  },
  {
    label: 'Оплачено',
    value: summary.value ? summary.value.paidCount : '...',
    caption: summary.value ? formatAmount(summary.value.paidAmount) : 'Загрузка'
  },
  {
    label: 'Последняя оплата',
    value: summary.value ? formatDateShort(summary.value.latestPaymentDate) : '...',
    caption: summary.value?.latestPaymentDate ? 'По дате оплаты' : 'Нет данных'
  },
  {
    label: 'Без даты оплаты',
    value: summary.value ? summary.value.withoutPaymentDateCount : '...',
    caption: 'Требуют уточнения'
  }
]);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseExpensesQuery(query);
    await loadExpensesAndSummary();
  },
  { immediate: true }
);

watch(isGuest, (guest) => {
  if (!guest) {
    void loadLookups();
    void loadExpensesAndSummary();
  }
});

watch(currentWorkspaceId, () => {
  if (!isGuest.value) {
    void loadExpensesAndSummary();
  }
});

if (!isGuest.value) {
  void loadLookups();
}

onBeforeUnmount(() => {
  clearToast();
});

function clearToast() {
  if (toastTimer !== undefined) {
    window.clearTimeout(toastTimer);
    toastTimer = undefined;
  }

  toastMessage.value = '';
}

function showToast(message: string) {
  clearToast();
  toastMessage.value = message;
  toastTimer = window.setTimeout(() => {
    toastMessage.value = '';
    toastTimer = undefined;
  }, 3000);
}

function goToWorkspaces(): void {
  void router.push('/management/workspaces');
}

async function loadExpensesAndSummary() {
  if (isGuest.value) {
    expenses.value = [];
    totalCount.value = 0;
    summary.value = null;
    error.value = '';
    summaryError.value = '';
    loading.value = false;
    summaryLoading.value = false;
    return;
  }

  if (!currentWorkspaceId.value) {
    expenses.value = [];
    totalCount.value = 0;
    summary.value = null;
    error.value = '';
    summaryError.value = '';
    loading.value = false;
    summaryLoading.value = false;
    return;
  }

  loading.value = true;
  summaryLoading.value = true;
  error.value = '';
  summaryError.value = '';

  try {
    const [expenseResponse, summaryResponse] = await Promise.all([
      getExpenses(toExpensesApiParams(queryState.value, currentWorkspaceId.value)),
      getExpenseSummary(toExpensesSummaryParams(queryState.value, currentWorkspaceId.value))
    ]);

    expenses.value = expenseResponse.items;
    totalCount.value = expenseResponse.totalCount;
    summary.value = summaryResponse;
  } catch (err) {
    expenses.value = [];
    totalCount.value = 0;
    summary.value = null;
    const message = getProblemMessage(err, 'Не удалось загрузить расходы.');
    error.value = message;
    summaryError.value = message;
  } finally {
    loading.value = false;
    summaryLoading.value = false;
  }
}

async function loadLookups() {
  if (isGuest.value) {
    categories.value = [];
    users.value = [];
    lookupError.value = '';
    return;
  }

  lookupError.value = '';

  try {
    const categoryResponse = await getExpenseCategories({ page: 1, pageSize: 200, sort: 'name' });

    categories.value = categoryResponse.items;
    users.value = auth.user
      ? [{
          id: auth.user.id,
          login: auth.user.login,
          email: auth.user.email
        }]
      : [];
  } catch (err) {
    lookupError.value = getProblemMessage(err, 'Не удалось загрузить справочники расходов.');
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

function openCreateForm() {
  formMode.value = 'create';
  formExpense.value = null;
  formError.value = '';
  formOpen.value = true;
}

async function openEditForm(expense: ExpenseListItem | ExpenseDetail) {
  formMode.value = 'edit';
  formError.value = '';
  formOpen.value = true;

  if ('description' in expense) {
    formExpense.value = expense;
    return;
  }

  try {
    formExpense.value = await getExpense(expense.id);
  } catch (err) {
    formError.value = getProblemMessage(err, 'Не удалось открыть расход для редактирования.');
    formExpense.value = null;
  }
}

function closeForm() {
  formOpen.value = false;
  formError.value = '';
  formExpense.value = null;
}

async function saveExpense(request: SaveExpenseRequest) {
  formSaving.value = true;
  formError.value = '';
  clearToast();

  try {
    const saved = formMode.value === 'edit' && formExpense.value
      ? await updateExpense(formExpense.value.id, request)
      : await createExpense(request);

    showToast(formMode.value === 'edit' ? 'Расход обновлён.' : 'Расход добавлен.');
    closeForm();
    await loadExpensesAndSummary();

    if (selectedExpense.value?.id === saved.id) {
      selectedExpense.value = saved;
    }
  } catch (err) {
    formError.value = getProblemMessage(err, 'Не удалось сохранить расход.');
  } finally {
    formSaving.value = false;
  }
}

async function changeStatus(expense: ExpenseListItem | ExpenseDetail, statusKey: ExpenseStatusKey) {
  actionLoading.value = true;
  clearToast();

  try {
    const detail = 'description' in expense ? expense : await getExpense(expense.id);
    const now = new Date().toISOString();
    const updated = await updateExpense(detail.id, {
      idWorkspace: detail.idWorkspace,
      idCategory: detail.idCategory,
      idCreator: detail.idCreator,
      idResponsible: detail.idResponsible,
      name: detail.name,
      description: detail.description,
      cost: detail.cost ?? 0,
      status: getExpenseStatusNumber(statusKey),
      statusKey,
      datePay: statusKey === 'paid' && !detail.datePay ? now : detail.datePay,
      dateCreate: detail.dateCreate,
      dateUpdate: now
    });

    showToast('Статус расхода обновлён.');
    selectedExpense.value = updated;
    await loadExpensesAndSummary();
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось обновить статус расхода.');
  } finally {
    actionLoading.value = false;
  }
}
</script>

<template>
  <div class="expenses-page">
    <PageHeader
      title="Расходы"
      description="Учитывайте операционные траты, платежи и статусы расходов, связанных с товарным бизнесом."
    />

    <AuthRequiredState
      v-if="isGuest"
      description="Раздел расходов помогает учитывать закупки, логистику, услуги и платежные статусы по рабочей области. Войдите, чтобы открыть финансовые данные."
    />
    <template v-else>
    <EmptyState
      v-if="!currentWorkspaceId"
      class="app-surface"
      title="Вне рабочей области"
      description="Создайте или выберите рабочую область, чтобы открыть расходы."
    >
      <Button variant="primary" @click="goToWorkspaces">Рабочие области</Button>
    </EmptyState>

    <template v-else>
    <ExpensesFilters
      :state="queryState"
      :categories-by-id="categoriesById"
      :users-by-id="usersById"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <KpiGrid :items="kpiItems" variant="market" />

    <section v-if="summaryError && !summaryLoading" class="expenses-page__notice app-surface">
      {{ summaryError }}
    </section>

    <section v-if="lookupError" class="expenses-page__notice app-surface">
      {{ lookupError }} Список расходов продолжит использовать данные из записей.
    </section>

    <div v-if="!loading && !error && expenses.length > 0" class="expenses-page__table-actions">
      <Button variant="primary" @click="openCreateForm">Добавить расход</Button>
    </div>

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Не удалось загрузить расходы"
      :description="error"
    />

    <EmptyState
      v-else-if="expenses.length === 0"
      class="app-surface"
      title="Расходов пока нет"
      description="Добавьте первый расход, чтобы отслеживать операционные траты и платежи."
    >
      <Button variant="primary" @click="openCreateForm">Добавить расход</Button>
    </EmptyState>

    <ExpensesTable
      v-else
      :rows="expenses"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedExpense?.id"
      :categories-by-id="categoriesById"
      :users-by-id="usersById"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openExpense"
      @edit="openEditForm"
    />

    <ExpenseDetailDrawer
      :open="Boolean(selectedExpense)"
      :expense="selectedExpense"
      :categories-by-id="categoriesById"
      :users-by-id="usersById"
      :action-loading="actionLoading"
      @close="closeExpense"
      @edit="openEditForm"
      @status="changeStatus"
    />

    <ExpenseFormDrawer
      :open="formOpen"
      :mode="formMode"
      :expense="formExpense"
      :categories="categories"
      :users="users"
      :workspace-id="currentWorkspaceId"
      :creator-id="currentUserId"
      :loading="formSaving"
      :error="formError"
      @close="closeForm"
      @save="saveExpense"
    />

    <Teleport to="body">
      <div
        v-if="toastMessage"
        class="expenses-toast app-surface"
        role="status"
        aria-live="polite"
      >
        {{ toastMessage }}
      </div>
    </Teleport>
    </template>
    </template>
  </div>
</template>

<style scoped>
.expenses-page {
  display: grid;
  gap: var(--space-4);
}

.expenses-page__notice {
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.expenses-page__notice {
  border-color: var(--state-warning-border);
  color: var(--state-warning);
}

.expenses-page__table-actions {
  display: flex;
  justify-content: flex-start;
}

.expenses-toast {
  position: fixed;
  right: var(--space-4);
  bottom: var(--space-4);
  z-index: 70;
  max-width: min(24rem, calc(100vw - 2rem));
  padding: var(--space-3) var(--space-4);
  border-color: var(--state-success-border);
  background:
    linear-gradient(135deg, rgb(52 211 153 / 0.12), transparent 58%),
    var(--color-surface);
  color: var(--state-success-text);
  font-size: 0.875rem;
  font-weight: 700;
  box-shadow: var(--shadow-panel);
}
</style>
