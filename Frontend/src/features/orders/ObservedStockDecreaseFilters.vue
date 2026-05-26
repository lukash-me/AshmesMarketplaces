<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import { getObservedStockDecreaseSortLabel } from './orderDisplay';
import type { ObservedStockDecreaseQueryFilterKey } from './ordersQuery';
import type { ObservedStockDecreaseQueryState } from './orders.types';

const props = defineProps<{
  state: ObservedStockDecreaseQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ObservedStockDecreaseQueryState>];
  reset: [];
  remove: [key: ObservedStockDecreaseQueryFilterKey];
}>();

const defaultSort = '-decrease';
const defaultMinDecrease = '1';
const sortOptions = [
  { value: '-decrease', label: 'Больше всего снизился' },
  { value: 'decrease', label: 'Меньше всего снизился' },
  { value: '-currentQuantity', label: 'Остаток сейчас: больше' },
  { value: 'currentQuantity', label: 'Остаток сейчас: меньше' },
  { value: '-previousQuantity', label: 'Остаток раньше: больше' },
  { value: 'previousQuantity', label: 'Остаток раньше: меньше' },
  { value: '-observedAtUtc', label: 'Последнее наблюдение: новое' },
  { value: 'observedAtUtc', label: 'Последнее наблюдение: старое' }
];

const form = reactive({
  search: props.state.search,
  sourceCategory: props.state.sourceCategory,
  sourceSubcategory: props.state.sourceSubcategory,
  brandName: props.state.brandName,
  sellerName: props.state.sellerName,
  minDecrease: props.state.minDecrease,
  sort: props.state.sort
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.sourceCategory = state.sourceCategory;
    form.sourceSubcategory = state.sourceSubcategory;
    form.brandName = state.brandName;
    form.sellerName = state.sellerName;
    form.minDecrease = state.minDecrease;
    form.sort = state.sort;
  },
  { deep: true }
);

const chips = computed(() => {
  const values: Array<{ key: ObservedStockDecreaseQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    values.push({ key: 'search', label: 'Поиск', value: props.state.search });
  }

  if (props.state.sourceCategory) {
    values.push({ key: 'sourceCategory', label: 'Категория', value: props.state.sourceCategory });
  }

  if (props.state.sourceSubcategory) {
    values.push({
      key: 'sourceSubcategory',
      label: 'Подкатегория',
      value: props.state.sourceSubcategory
    });
  }

  if (props.state.brandName) {
    values.push({ key: 'brandName', label: 'Бренд', value: props.state.brandName });
  }

  if (props.state.sellerName) {
    values.push({ key: 'sellerName', label: 'Продавец', value: props.state.sellerName });
  }

  if (props.state.minDecrease !== defaultMinDecrease) {
    values.push({ key: 'minDecrease', label: 'Мин. снижение', value: props.state.minDecrease });
  }

  if (props.state.sort !== defaultSort) {
    values.push({
      key: 'sort',
      label: 'Сортировка',
      value: getObservedStockDecreaseSortLabel(props.state.sort)
    });
  }

  return values;
});

const hasActiveState = computed(() => chips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    sourceCategory: form.sourceCategory.trim(),
    sourceSubcategory: form.sourceSubcategory.trim(),
    brandName: form.brandName.trim(),
    sellerName: form.sellerName.trim(),
    minDecrease: normalizeMinDecrease(form.minDecrease),
    sort: form.sort
  });
}

function applySort() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    sourceCategory: form.sourceCategory.trim(),
    sourceSubcategory: form.sourceSubcategory.trim(),
    brandName: form.brandName.trim(),
    sellerName: form.sellerName.trim(),
    minDecrease: normalizeMinDecrease(form.minDecrease),
    sort: form.sort
  });
}

function normalizeMinDecrease(value: string): string {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 1 ? value : defaultMinDecrease;
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
        placeholder="Название, WB id, бренд или продавец"
      />

      <label class="select-field">
        <span>Сортировка</span>
        <select v-model="form.sort" @change="applySort">
          <option v-for="option in sortOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </label>

      <div class="filters__actions">
        <Button class="filters__apply" type="submit" variant="primary">Применить</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">
          Сбросить
        </Button>
      </div>
    </div>

    <div class="filters__grid">
      <Input
        v-model="form.sourceCategory"
        class="filter-control"
        :class="{ 'filter-control--active': form.sourceCategory.trim() }"
        label="Категория"
        placeholder="Все категории"
      />
      <Input
        v-model="form.sourceSubcategory"
        class="filter-control"
        :class="{ 'filter-control--active': form.sourceSubcategory.trim() }"
        label="Подкатегория"
        placeholder="Все подкатегории"
      />
      <Input
        v-model="form.brandName"
        class="filter-control"
        :class="{ 'filter-control--active': form.brandName.trim() }"
        label="Бренд"
        placeholder="Все бренды"
      />
      <Input
        v-model="form.sellerName"
        class="filter-control"
        :class="{ 'filter-control--active': form.sellerName.trim() }"
        label="Продавец"
        placeholder="Все продавцы"
      />
      <Input
        v-model="form.minDecrease"
        class="filter-control"
        :class="{ 'filter-control--active': form.minDecrease !== defaultMinDecrease }"
        type="number"
        label="Минимальное снижение"
        placeholder="1"
      />
    </div>

    <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры">
      <button
        v-for="chip in chips"
        :key="chip.key"
        class="filters__chip"
        :class="{ 'filters__chip--sort': chip.key === 'sort' }"
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
  z-index: 3;
  display: grid;
  gap: var(--space-3);
  border-color: rgb(249 115 22 / 0.18);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.035), transparent 42%),
    var(--surface-panel);
  padding: var(--space-3);
}

.filters__top,
.filters__grid {
  display: grid;
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

.filters__apply {
  min-height: 2.35rem;
  font-weight: 760;
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
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
  outline: none;
}

.select-field select:focus {
  border-color: var(--accent-primary-hover-border);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
}

.filter-control--active :deep(.field__control) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--surface-control-focus);
  box-shadow: inset 0 0 0 1px rgb(249 115 22 / 0.07), 0 0 0 1px rgb(249 115 22 / 0.04);
}

.filter-control--active :deep(.field__label) {
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
  border: 1px solid rgb(249 115 22 / 0.22);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.07), transparent),
    var(--surface-control-raised);
  color: var(--color-text-muted);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
}

.filters__chip--sort {
  border-color: var(--accent-ember-border);
  color: var(--accent-ember-text);
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--accent-primary-hover-border);
  background: var(--color-surface-hover);
  color: var(--color-text);
  outline: none;
}

.filters__chip strong {
  max-width: 16rem;
  overflow: hidden;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
  text-overflow: ellipsis;
}

@media (min-width: 860px) {
  .filters__top {
    grid-template-columns: minmax(18rem, 1fr) minmax(15rem, 0.55fr) auto;
    align-items: end;
  }

  .filters__grid {
    grid-template-columns: repeat(5, minmax(0, 1fr));
  }
}
</style>
