<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import {
  compactId,
  fieldValue,
  formatAmount,
  formatDateTime,
  getExpenseCategoryLabel,
  getExpenseNeutralTone,
  getExpenseStatusLabel,
  type ExpenseCategoryLookup
} from './expenseDisplay';
import { getExpense, getExpenseCategory } from './expenses.api';
import type { ExpenseCategoryDetail, ExpenseDetail, ExpenseListItem } from './expenses.types';

const props = defineProps<{
  open: boolean;
  expense: ExpenseListItem | null;
  categoriesById: ExpenseCategoryLookup;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<ExpenseDetail | null>(null);
const category = ref<ExpenseCategoryDetail | null>(null);
const detailLoading = ref(false);
const categoryLoading = ref(false);
const detailError = ref('');
const categoryError = ref('');
let detailLoadVersion = 0;
let categoryLoadVersion = 0;

const displayExpense = computed(() => detail.value ?? props.expense);
const linkedCategoryId = computed(() => displayExpense.value?.idCategory ?? null);
const categoryFromLookup = computed(() =>
  linkedCategoryId.value ? props.categoriesById[linkedCategoryId.value] ?? null : null
);
const displayCategory = computed(() => categoryFromLookup.value ?? category.value);

watch(
  () => [props.open, props.expense?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      category.value = null;
      detailError.value = '';
      categoryError.value = '';
      return;
    }

    await loadDetail(id);
  },
  { immediate: true }
);

watch(
  () => [props.open, linkedCategoryId.value] as const,
  async ([open, idCategory]) => {
    if (!open || !idCategory) {
      category.value = null;
      categoryError.value = '';
      categoryLoading.value = false;
      return;
    }

    if (props.categoriesById[idCategory]) {
      category.value = null;
      categoryError.value = '';
      categoryLoading.value = false;
      return;
    }

    await loadCategory(idCategory);
  },
  { immediate: true }
);

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});

async function loadDetail(id: string): Promise<void> {
  const version = ++detailLoadVersion;

  detailLoading.value = true;
  detailError.value = '';
  detail.value = null;

  try {
    const response = await getExpense(id);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load expense details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadCategory(id: string): Promise<void> {
  const version = ++categoryLoadVersion;

  categoryLoading.value = true;
  categoryError.value = '';
  category.value = null;

  try {
    const response = await getExpenseCategory(id);

    if (version === categoryLoadVersion) {
      category.value = response;
    }
  } catch (err) {
    if (version === categoryLoadVersion) {
      categoryError.value = getProblemMessage(err, 'Unable to load linked expense category.');
    }
  } finally {
    if (version === categoryLoadVersion) {
      categoryLoading.value = false;
    }
  }
}

function close(): void {
  emit('close');
}

function onKeydown(event: KeyboardEvent): void {
  if (props.open && event.key === 'Escape') {
    close();
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Close expense detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="expense-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayExpense" class="drawer__title">
            <Badge :tone="getExpenseNeutralTone()">
              {{ getExpenseStatusLabel(displayExpense.status) }}
            </Badge>
            <h2 id="expense-detail-title">{{ displayExpense.name }}</h2>
            <p>
              Expense {{ compactId(displayExpense.id) }} / Category
              {{ getExpenseCategoryLabel(displayExpense.idCategory, categoriesById) }}
            </p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close expense detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayExpense" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Cost</span>
              <strong class="numeric">{{ formatAmount(displayExpense.cost) }}</strong>
            </div>
            <div>
              <span>Status</span>
              <strong>{{ getExpenseStatusLabel(displayExpense.status) }}</strong>
            </div>
            <div>
              <span>Paid</span>
              <strong class="numeric">{{ formatDateTime(displayExpense.datePay) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Description</h3>
            <p class="drawer__text">{{ fieldValue(detail?.description) }}</p>
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Expense ID</dt><dd>{{ displayExpense.id }}</dd></div>
              <div><dt>Workspace ID</dt><dd>{{ displayExpense.idWorkspace }}</dd></div>
              <div><dt>Category ID</dt><dd>{{ fieldValue(displayExpense.idCategory) }}</dd></div>
              <div><dt>Creator ID</dt><dd>{{ displayExpense.idCreator }}</dd></div>
              <div><dt>Responsible ID</dt><dd>{{ fieldValue(displayExpense.idResponsible) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Expense fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Name</dt><dd>{{ displayExpense.name }}</dd></div>
              <div>
                <dt>Status</dt>
                <dd>
                  <Badge :tone="getExpenseNeutralTone()">
                    {{ getExpenseStatusLabel(displayExpense.status) }}
                  </Badge>
                </dd>
              </div>
              <div><dt>Cost</dt><dd>{{ formatAmount(displayExpense.cost) }}</dd></div>
              <div><dt>Paid</dt><dd>{{ formatDateTime(displayExpense.datePay) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Timeline</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Created</dt><dd>{{ formatDateTime(displayExpense.dateCreate) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayExpense.dateUpdate) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <header class="category-header">
              <h3>Linked category</h3>
              <code v-if="linkedCategoryId" :title="linkedCategoryId">{{ compactId(linkedCategoryId) }}</code>
            </header>

            <div v-if="!linkedCategoryId" class="drawer__placeholder">
              No expense category is linked by the current expense record.
            </div>

            <LoadingState v-else-if="categoryLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="categoryError" class="drawer__notice">
              {{ categoryError }} Raw category ID remains visible above.
            </div>

            <div v-else-if="displayCategory" class="category-detail">
              <strong>{{ displayCategory.name }}</strong>
              <p>{{ fieldValue(displayCategory.description) }}</p>
              <dl class="drawer__fields drawer__fields--two">
                <div><dt>Category ID</dt><dd>{{ displayCategory.id }}</dd></div>
                <div><dt>Created</dt><dd>{{ formatDateTime(displayCategory.dateCreate) }}</dd></div>
                <div><dt>Updated</dt><dd>{{ formatDateTime(displayCategory.dateUpdate) }}</dd></div>
              </dl>
            </div>
          </section>
        </div>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.drawer-shell {
  position: fixed;
  inset: 0;
  z-index: 50;
}

.drawer-shell__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: var(--theme-backdrop);
}

.drawer {
  position: absolute;
  top: var(--space-3);
  right: var(--space-3);
  bottom: var(--space-3);
  display: grid;
  width: min(40rem, calc(100vw - 1.5rem));
  grid-template-rows: auto 1fr;
  overflow: hidden;
}

.drawer__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
  border-bottom: 1px solid var(--color-border);
  background: var(--background-panel-highlight);
  padding: var(--space-4);
}

.drawer__title {
  display: grid;
  min-width: 0;
  gap: var(--space-2);
}

.drawer__title h2 {
  margin: 0;
  overflow-wrap: anywhere;
  font-size: 1rem;
  font-weight: 760;
}

.drawer__title p {
  margin: 0;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__body {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.drawer__section--summary {
  grid-template-columns: repeat(1, minmax(0, 1fr));
}

.drawer__section--summary div {
  display: grid;
  gap: var(--space-1);
}

.drawer__section--summary span,
.drawer__fields dt {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.drawer__section--summary strong {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: 0.9rem;
}

.drawer__section h3 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__text,
.category-detail p {
  margin: 0;
  color: var(--color-text);
  line-height: 1.55;
  white-space: pre-wrap;
}

.drawer__notice,
.drawer__placeholder {
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.drawer__loading {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
}

.category-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.category-header code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

.category-detail {
  display: grid;
  gap: var(--space-3);
}

.category-detail strong {
  color: var(--color-text);
  font-size: 0.9rem;
}

@media (min-width: 680px) {
  .drawer__section--summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
