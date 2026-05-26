<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { Pencil, X } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import {
  fieldValue,
  formatAmount,
  formatDateTime,
  getExpenseCategoryLabel,
  getExpenseStatusLabel,
  getExpenseStatusTone,
  getUserLabel,
  type ExpenseCategoryLookup,
  type ExpenseUserLookup
} from './expenseDisplay';
import { getExpense } from './expenses.api';
import type { ExpenseDetail, ExpenseListItem, ExpenseStatusKey } from './expenses.types';

const props = defineProps<{
  open: boolean;
  expense: ExpenseListItem | null;
  categoriesById: ExpenseCategoryLookup;
  usersById: ExpenseUserLookup;
  actionLoading: boolean;
}>();

const emit = defineEmits<{
  close: [];
  edit: [expense: ExpenseDetail | ExpenseListItem];
  status: [expense: ExpenseDetail | ExpenseListItem, statusKey: ExpenseStatusKey];
}>();

const detail = ref<ExpenseDetail | null>(null);
const detailLoading = ref(false);
const detailError = ref('');
let detailLoadVersion = 0;

const displayExpense = computed(() => detail.value ?? props.expense);
const title = computed(() => displayExpense.value?.name ?? 'Расход');

watch(
  () => [props.open, props.expense?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      detailError.value = '';
      return;
    }

    await loadDetail(id);
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
      detailError.value = getProblemMessage(err, 'Не удалось загрузить расход.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
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
      <button class="drawer-shell__backdrop" type="button" aria-label="Закрыть расход" @click="close" />

      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="expense-detail-title">
        <header class="drawer__header">
          <div v-if="displayExpense" class="drawer__title">
            <Badge :tone="getExpenseStatusTone(displayExpense.statusKey)">
              {{ getExpenseStatusLabel(displayExpense.statusKey, displayExpense.statusLabel) }}
            </Badge>
            <h2 id="expense-detail-title">{{ title }}</h2>
            <p>{{ getExpenseCategoryLabel(displayExpense.idCategory, categoriesById, displayExpense.categoryName) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Закрыть расход" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayExpense" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Сумма</span>
              <strong class="numeric">{{ formatAmount(displayExpense.cost) }}</strong>
            </div>
            <div>
              <span>Дата оплаты</span>
              <strong class="numeric">{{ formatDateTime(displayExpense.datePay) }}</strong>
            </div>
            <div>
              <span>Ответственный</span>
              <strong>{{ getUserLabel(displayExpense.idResponsible, usersById, displayExpense.responsibleLogin, displayExpense.responsibleEmail) }}</strong>
            </div>
          </section>

          <section class="drawer__actions">
            <Button variant="primary" :disabled="actionLoading" @click="emit('edit', detail ?? displayExpense)">
              <Pencil :size="15" />
              Редактировать
            </Button>
            <Button
              variant="secondary"
              :loading="actionLoading && displayExpense.statusKey !== 'paid'"
              :disabled="actionLoading || displayExpense.statusKey === 'paid'"
              @click="emit('status', detail ?? displayExpense, 'paid')"
            >
              Отметить как оплачено
            </Button>
            <Button
              variant="secondary"
              :disabled="actionLoading || displayExpense.statusKey === 'pending_payment'"
              @click="emit('status', detail ?? displayExpense, 'pending_payment')"
            >
              К оплате
            </Button>
            <Button
              variant="ghost"
              :disabled="actionLoading || displayExpense.statusKey === 'cancelled'"
              @click="emit('status', detail ?? displayExpense, 'cancelled')"
            >
              Отменить
            </Button>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Описание</h3>
            <p class="drawer__text">{{ fieldValue(detail?.description) }}</p>
          </section>

          <section class="drawer__section">
            <h3>Детали</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div>
                <dt>Категория</dt>
                <dd>{{ getExpenseCategoryLabel(displayExpense.idCategory, categoriesById, displayExpense.categoryName) }}</dd>
              </div>
              <div>
                <dt>Статус</dt>
                <dd>
                  <Badge :tone="getExpenseStatusTone(displayExpense.statusKey)">
                    {{ getExpenseStatusLabel(displayExpense.statusKey, displayExpense.statusLabel) }}
                  </Badge>
                </dd>
              </div>
              <div><dt>Рабочее пространство</dt><dd>{{ displayExpense.workspaceName }}</dd></div>
              <div><dt>Создал</dt><dd>{{ getUserLabel(displayExpense.idCreator, usersById, displayExpense.creatorLogin, displayExpense.creatorEmail) }}</dd></div>
              <div><dt>Ответственный</dt><dd>{{ getUserLabel(displayExpense.idResponsible, usersById, displayExpense.responsibleLogin, displayExpense.responsibleEmail) }}</dd></div>
              <div><dt>Сумма</dt><dd>{{ formatAmount(displayExpense.cost) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>История</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Создан</dt><dd>{{ formatDateTime(displayExpense.dateCreate) }}</dd></div>
              <div><dt>Обновлен</dt><dd>{{ formatDateTime(displayExpense.dateUpdate) }}</dd></div>
              <div><dt>Дата оплаты</dt><dd>{{ formatDateTime(displayExpense.datePay) }}</dd></div>
            </dl>
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
  font-size: 0.8125rem;
}

.drawer__body {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section,
.drawer__actions {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.drawer__actions {
  grid-template-columns: repeat(1, minmax(0, 1fr));
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
  font-size: 0.8125rem;
}

.drawer__text {
  margin: 0;
  color: var(--color-text);
  line-height: 1.55;
  white-space: pre-wrap;
}

.drawer__notice {
  border: 1px solid var(--state-danger-border);
  border-radius: var(--radius-md);
  background: var(--state-danger-soft);
  color: var(--state-danger);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__loading {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
}

@media (min-width: 680px) {
  .drawer__section--summary,
  .drawer__actions {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
