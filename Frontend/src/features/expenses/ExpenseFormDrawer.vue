<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, watch } from 'vue';
import { X } from 'lucide-vue-next';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  expenseStatusOptions,
  getExpenseStatusNumber
} from './expenseDisplay';
import type {
  ExpenseCategoryListItem,
  ExpenseDetail,
  ExpenseStatusKey,
  ExpenseUserListItem,
  SaveExpenseRequest
} from './expenses.types';

const props = defineProps<{
  open: boolean;
  mode: 'create' | 'edit';
  expense: ExpenseDetail | null;
  categories: ExpenseCategoryListItem[];
  users: ExpenseUserListItem[];
  workspaceId: string;
  creatorId: string;
  loading: boolean;
  error: string;
}>();

const emit = defineEmits<{
  close: [];
  save: [request: SaveExpenseRequest];
}>();

const form = reactive({
  name: '',
  cost: '',
  idCategory: '',
  statusKey: 'pending_payment' as ExpenseStatusKey,
  datePay: '',
  idResponsible: '',
  description: ''
});

const title = computed(() => (props.mode === 'create' ? 'Добавить расход' : 'Редактировать расход'));
const submitLabel = computed(() => (props.mode === 'create' ? 'Сохранить' : 'Сохранить изменения'));

const nameError = computed(() => (form.name.trim() ? '' : 'Укажите название'));
const costError = computed(() => {
  if (!form.cost.trim()) {
    return 'Укажите сумму';
  }

  const value = Number(form.cost);
  return Number.isFinite(value) && value >= 0 ? '' : 'Сумма должна быть 0 или больше';
});
const contextError = computed(() => {
  if (!props.workspaceId) {
    return 'Не удалось определить рабочее пространство для расхода.';
  }

  if (!props.creatorId) {
    return 'Не удалось определить текущего пользователя.';
  }

  return '';
});
const canSubmit = computed(() => !nameError.value && !costError.value && !contextError.value && !props.loading);

watch(
  () => [props.open, props.expense?.id, props.mode] as const,
  () => {
    if (!props.open) {
      return;
    }

    if (props.mode === 'edit' && props.expense) {
      form.name = props.expense.name;
      form.cost = String(props.expense.cost ?? 0);
      form.idCategory = props.expense.idCategory ?? '';
      form.statusKey = props.expense.statusKey ?? 'planned';
      form.datePay = toDateInput(props.expense.datePay);
      form.idResponsible = props.expense.idResponsible ?? '';
      form.description = props.expense.description ?? '';
      return;
    }

    form.name = '';
    form.cost = '';
    form.idCategory = props.categories[0]?.id ?? '';
    form.statusKey = 'pending_payment';
    form.datePay = '';
    form.idResponsible = '';
    form.description = '';
  },
  { immediate: true }
);

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});

function submit(): void {
  if (!canSubmit.value) {
    return;
  }

  const now = new Date().toISOString();
  const dateCreate = props.mode === 'edit' && props.expense ? props.expense.dateCreate : now;

  emit('save', {
    idWorkspace: props.expense?.idWorkspace ?? props.workspaceId,
    idCategory: form.idCategory || null,
    idCreator: props.expense?.idCreator ?? props.creatorId,
    idResponsible: form.idResponsible || null,
    name: form.name.trim(),
    description: form.description.trim() || null,
    cost: Number(form.cost),
    status: getExpenseStatusNumber(form.statusKey),
    statusKey: form.statusKey,
    datePay: form.datePay ? `${form.datePay}T12:00:00.000Z` : null,
    dateCreate,
    dateUpdate: now
  });
}

function close(): void {
  if (!props.loading) {
    emit('close');
  }
}

function onKeydown(event: KeyboardEvent): void {
  if (props.open && event.key === 'Escape') {
    close();
  }
}

function toDateInput(value: string | null): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) {
    return '';
  }

  return date.toISOString().slice(0, 10);
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Закрыть форму расхода" @click="close" />

      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="expense-form-title">
        <header class="drawer__header">
          <div>
            <h2 id="expense-form-title">{{ title }}</h2>
            <p>Заполните основные поля расхода для ежедневного учета.</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Закрыть форму расхода" @click="close">
            <X :size="18" />
          </button>
        </header>

        <form class="drawer__body" @submit.prevent="submit">
          <section v-if="contextError || error" class="drawer__notice">
            {{ contextError || error }}
          </section>

          <Input v-model="form.name" label="Название" placeholder="Например, упаковка для партии" :error="nameError" />
          <Input v-model="form.cost" label="Сумма" type="number" placeholder="0" :error="costError" />

          <label class="field">
            <span>Категория</span>
            <select v-model="form.idCategory" class="field__control app-select">
              <option value="">Без категории</option>
              <option v-for="category in categories" :key="category.id" :value="category.id">
                {{ category.name }}
              </option>
            </select>
          </label>

          <label class="field">
            <span>Статус</span>
            <select v-model="form.statusKey" class="field__control app-select">
              <option v-for="status in expenseStatusOptions" :key="status.key" :value="status.key">
                {{ status.label }}
              </option>
            </select>
          </label>

          <Input v-model="form.datePay" label="Дата оплаты" type="date" />

          <label class="field">
            <span>Ответственный</span>
            <select v-model="form.idResponsible" class="field__control app-select">
              <option value="">Не назначен</option>
              <option v-for="user in users" :key="user.id" :value="user.id">
                {{ user.login || user.email || 'Пользователь' }}
              </option>
            </select>
          </label>

          <label class="field field--wide">
            <span>Описание</span>
            <textarea v-model="form.description" class="field__control field__control--textarea" rows="5" />
          </label>

          <footer class="drawer__actions">
            <Button type="submit" variant="primary" :loading="loading" :disabled="!canSubmit">
              {{ submitLabel }}
            </Button>
            <Button type="button" variant="ghost" :disabled="loading" @click="close">Отмена</Button>
          </footer>
        </form>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.drawer-shell {
  position: fixed;
  inset: 0;
  z-index: 60;
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
  width: min(34rem, calc(100vw - 1.5rem));
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

.drawer__header h2 {
  margin: 0;
  font-size: 1rem;
  font-weight: 760;
}

.drawer__header p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.drawer__body {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  overflow-y: auto;
  padding: var(--space-4);
}

.field {
  display: grid;
  gap: var(--space-1);
}

.field span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.field__control {
  min-height: 2.25rem;
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
  outline: none;
}

.field__control:focus {
  border-color: var(--color-primary);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
}

.field__control--textarea {
  min-height: 7rem;
  padding: var(--space-3);
  resize: vertical;
}

.drawer__notice {
  border: 1px solid var(--state-danger-border);
  border-radius: var(--radius-md);
  background: var(--state-danger-soft);
  color: var(--state-danger);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  justify-content: flex-end;
  border-top: 1px solid var(--color-border);
  padding-top: var(--space-3);
}
</style>
