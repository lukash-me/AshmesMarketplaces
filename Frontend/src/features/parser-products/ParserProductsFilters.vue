<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import { SlidersHorizontal } from 'lucide-vue-next';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import MarketFilterSelect from './MarketFilterSelect.vue';
import type { ParserProductQueryFilterKey } from './parserProductsQuery';
import type {
  ParserProductCoveredNiche,
  ParserProductFilterOptions,
  ParserProductQueryState
} from './parserProducts.types';

const props = defineProps<{
  state: ParserProductQueryState;
  filterOptions: ParserProductFilterOptions;
  coveredNiches: ParserProductCoveredNiche[];
  filterOptionsLoading: boolean;
  filterOptionsError: string;
  coveredNichesLoading: boolean;
  coveredNichesError: string;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ParserProductQueryState>];
  reset: [];
  remove: [key: ParserProductQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  sourcePath: resolveNichePath(props.state),
  brandName: props.state.brandName,
  sellerName: props.state.sellerName,
  priceDiscountedFrom: props.state.priceDiscountedFrom,
  priceDiscountedTo: props.state.priceDiscountedTo,
  reviewRatingFrom: props.state.reviewRatingFrom,
  reviewRatingTo: props.state.reviewRatingTo,
  feedbackCountFrom: props.state.feedbackCountFrom,
  feedbackCountTo: props.state.feedbackCountTo
});

watch(
  () => [props.state, props.coveredNiches] as const,
  ([state]) => {
    form.search = state.search;
    form.sourcePath = resolveNichePath(state);
    form.brandName = state.brandName;
    form.sellerName = state.sellerName;
    form.priceDiscountedFrom = state.priceDiscountedFrom;
    form.priceDiscountedTo = state.priceDiscountedTo;
    form.reviewRatingFrom = state.reviewRatingFrom;
    form.reviewRatingTo = state.reviewRatingTo;
    form.feedbackCountFrom = state.feedbackCountFrom;
    form.feedbackCountTo = state.feedbackCountTo;
  },
  { deep: true }
);

const nicheOptions = computed(() => props.coveredNiches.map((niche) => niche.sourcePath));
const brandOptions = computed(() => props.filterOptions.brands);
const sellerOptions = computed(() => props.filterOptions.sellers);
const rangeKeys = [
  'priceDiscountedFrom',
  'priceDiscountedTo',
  'reviewRatingFrom',
  'reviewRatingTo',
  'feedbackCountFrom',
  'feedbackCountTo'
] as const;

const chips = computed(() => {
  const items = (Object.keys(form) as Array<keyof typeof form>)
    .filter((key) => props.state[key])
    .map((key) => ({
      key: key as ParserProductQueryFilterKey,
      label: getLabel(key),
      value: key === 'sourcePath' ? resolveNichePath(props.state) : props.state[key]
    }));

  if (!props.state.sourcePath && (props.state.sourceCategory || props.state.sourceSubcategory)) {
    items.push({
      key: 'sourcePath',
      label: 'Ниша',
      value: resolveNichePath(props.state)
    });
  }

  return items.concat(
    props.state.sort
      ? [{ key: 'sort' as ParserProductQueryFilterKey, label: 'Сортировка', value: getSortLabel(props.state.sort) }]
      : []
  );
});

function apply() {
  const selectedNiche = props.coveredNiches.find((niche) => niche.sourcePath === form.sourcePath);

  emit('apply', {
    page: 1,
    search: form.search.trim(),
    sourcePath: selectedNiche ? selectedNiche.sourcePath : '',
    wbCategoryId: selectedNiche?.wbCategoryId ? String(selectedNiche.wbCategoryId) : '',
    sourceCategory: selectedNiche?.sourceCategory?.trim() ?? '',
    sourceSubcategory: selectedNiche?.sourceSubcategory?.trim() ?? '',
    brandName: form.brandName.trim(),
    sellerName: form.sellerName.trim(),
    priceDiscountedFrom: form.priceDiscountedFrom.trim(),
    priceDiscountedTo: form.priceDiscountedTo.trim(),
    reviewRatingFrom: form.reviewRatingFrom.trim(),
    reviewRatingTo: form.reviewRatingTo.trim(),
    feedbackCountFrom: form.feedbackCountFrom.trim(),
    feedbackCountTo: form.feedbackCountTo.trim()
  });
}

function isActive(value: string): boolean {
  return value.trim() !== '';
}

function hasActiveRanges(): boolean {
  return rangeKeys.some((key) => isActive(form[key]));
}

function getLabel(key: string): string {
  const labels: Record<string, string> = {
    search: 'Поиск',
    sourcePath: 'Ниша',
    brandName: 'Бренд',
    sellerName: 'Продавец',
    priceDiscountedFrom: 'Цена от',
    priceDiscountedTo: 'Цена до',
    reviewRatingFrom: 'Рейтинг от',
    reviewRatingTo: 'Рейтинг до',
    feedbackCountFrom: 'Отзывы от',
    feedbackCountTo: 'Отзывы до'
  };

  return labels[key] ?? key;
}

function getSortLabel(value: string): string {
  const labels: Record<string, string> = {
    position: 'Позиция: сначала лучшие',
    '-position': 'Позиция: обратный порядок',
    reviewRating: 'Рейтинг: по возрастанию',
    '-reviewRating': 'Рейтинг: по убыванию',
    feedbackCount: 'Отзывы: по возрастанию',
    '-feedbackCount': 'Отзывы: по убыванию',
    price: 'Цена: по возрастанию',
    '-price': 'Цена: по убыванию',
    priceDiscounted: 'Цена: по возрастанию',
    '-priceDiscounted': 'Цена: по убыванию',
    name: 'Товар: А-Я',
    '-name': 'Товар: Я-А',
    wbProductId: 'WB id: по возрастанию',
    '-wbProductId': 'WB id: по убыванию',
    parsedAtUtc: 'Обновление: сначала старые',
    '-parsedAtUtc': 'Обновление: сначала новые'
  };

  return labels[value] ?? value;
}

function resolveNichePath(state: ParserProductQueryState): string {
  if (state.sourcePath) {
    return state.sourcePath;
  }

  if (!state.sourceCategory && !state.sourceSubcategory) {
    return '';
  }

  const exactMatch = props.coveredNiches.find((niche) =>
    niche.sourceCategory === state.sourceCategory && niche.sourceSubcategory === state.sourceSubcategory
  );

  if (exactMatch) {
    return exactMatch.sourcePath;
  }

  return [state.sourceCategory, state.sourceSubcategory].filter(Boolean).join(' / ');
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__search">
      <Input
        v-model="form.search"
        class="filter-control"
        :class="{ 'filter-control--active': isActive(form.search) }"
        label="Поиск"
        placeholder="Название или WB id"
      />
      <div class="filters__actions">
        <Button class="filters__apply" type="submit">Применить</Button>
        <Button v-if="chips.length" type="button" variant="ghost" @click="$emit('reset')">Сбросить</Button>
      </div>
    </div>

    <div class="filters__selects">
      <MarketFilterSelect
        v-model="form.sourcePath"
        class="filter-control filters__niche"
        :class="{ 'filter-control--active': isActive(form.sourcePath) }"
        label="Ниша"
        placeholder="Все покрытые ниши"
        search-placeholder="Найти нишу"
        :options="nicheOptions"
      />
      <MarketFilterSelect
        v-model="form.brandName"
        class="filter-control"
        :class="{ 'filter-control--active': isActive(form.brandName) }"
        label="Бренд"
        placeholder="Все бренды"
        search-placeholder="Найти бренд"
        :options="brandOptions"
      />
      <MarketFilterSelect
        v-model="form.sellerName"
        class="filter-control"
        :class="{ 'filter-control--active': isActive(form.sellerName) }"
        label="Продавец"
        placeholder="Все продавцы"
        search-placeholder="Найти продавца"
        :options="sellerOptions"
      />
    </div>

    <p v-if="coveredNichesError" class="filters__notice">{{ coveredNichesError }}</p>
    <p v-else-if="coveredNichesLoading" class="filters__notice">Загружаем покрытые ниши...</p>
    <p v-if="filterOptionsError" class="filters__notice">{{ filterOptionsError }}</p>
    <p v-else-if="filterOptionsLoading" class="filters__notice">Загружаем варианты фильтров...</p>

    <details class="filters__ranges" :class="{ 'filters__ranges--active': hasActiveRanges() }">
      <summary>
        <SlidersHorizontal :size="15" />
        Диапазоны
      </summary>
      <div class="filters__range-grid">
        <Input v-model="form.priceDiscountedFrom" class="filter-control" :class="{ 'filter-control--active': isActive(form.priceDiscountedFrom) }" label="Цена от" type="number" />
        <Input v-model="form.priceDiscountedTo" class="filter-control" :class="{ 'filter-control--active': isActive(form.priceDiscountedTo) }" label="Цена до" type="number" />
        <Input v-model="form.reviewRatingFrom" class="filter-control" :class="{ 'filter-control--active': isActive(form.reviewRatingFrom) }" label="Рейтинг от" type="number" />
        <Input v-model="form.reviewRatingTo" class="filter-control" :class="{ 'filter-control--active': isActive(form.reviewRatingTo) }" label="Рейтинг до" type="number" />
        <Input v-model="form.feedbackCountFrom" class="filter-control" :class="{ 'filter-control--active': isActive(form.feedbackCountFrom) }" label="Отзывы от" type="number" />
        <Input v-model="form.feedbackCountTo" class="filter-control" :class="{ 'filter-control--active': isActive(form.feedbackCountTo) }" label="Отзывы до" type="number" />
      </div>
    </details>

    <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры товаров маркетплейса">
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
  border-color: var(--color-border-strong);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.035), transparent 42%),
    var(--surface-panel);
  overflow: visible;
  padding: var(--space-3);
}

.filters__search,
.filters__selects,
.filters__range-grid {
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
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.24), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 10px 24px rgb(249 115 22 / 0.08);
  font-weight: 760;
}

.filters__apply:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.28), rgb(249 115 22 / 0.1)),
    var(--color-surface-hover);
}

.filters__apply:focus-visible {
  box-shadow: var(--focus-ring), 0 10px 24px rgb(249 115 22 / 0.12);
}

.filters__ranges {
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  padding: var(--space-2) var(--space-3);
}

.filters__ranges--active {
  border-color: var(--accent-ember-border);
  box-shadow: inset 0 0 0 1px rgb(249 115 22 / 0.08), 0 0 0 1px rgb(249 115 22 / 0.06);
}

.filter-control--active :deep(.field__control),
.filter-control--active :deep(.filter-select__trigger) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--surface-control-focus);
  box-shadow: inset 0 0 0 1px rgb(249 115 22 / 0.07), 0 0 0 1px rgb(249 115 22 / 0.04);
}

.filter-control--active :deep(.field__label),
.filter-control--active :deep(.filter-select__label),
.filter-control--active :deep(.filter-select__chevrons) {
  color: var(--accent-ember-text);
}

.filters__notice {
  margin: calc(var(--space-2) * -1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.filters__ranges summary {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  font-weight: 680;
}

.filters__range-grid {
  margin-top: var(--space-3);
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
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.12), transparent),
    var(--surface-control-raised);
  color: var(--accent-ember-text);
}

.filters__chip strong {
  max-width: 24rem;
  overflow: hidden;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
  text-overflow: ellipsis;
}

@media (min-width: 860px) {
  .filters__search {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__selects {
    grid-template-columns: minmax(18rem, 1.4fr) minmax(0, 1fr) minmax(0, 1fr);
  }

  .filters__range-grid {
    grid-template-columns: repeat(6, minmax(7rem, 1fr));
  }
}
</style>
