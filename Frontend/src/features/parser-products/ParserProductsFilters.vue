<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import { SlidersHorizontal } from 'lucide-vue-next';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import MarketFilterSelect from './MarketFilterSelect.vue';
import type { ParserProductQueryFilterKey } from './parserProductsQuery';
import type { ParserProductListItem, ParserProductQueryState } from './parserProducts.types';

const props = defineProps<{
  state: ParserProductQueryState;
  rows: ParserProductListItem[];
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ParserProductQueryState>];
  reset: [];
  remove: [key: ParserProductQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  sourceCategory: props.state.sourceCategory,
  sourceSubcategory: props.state.sourceSubcategory,
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
  () => props.state,
  (state) => {
    form.search = state.search;
    form.sourceCategory = state.sourceCategory;
    form.sourceSubcategory = state.sourceSubcategory;
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

const categoryOptions = computed(() => getOptions('sourceCategory'));
const subcategoryOptions = computed(() => getOptions('sourceSubcategory'));
const brandOptions = computed(() => getOptions('brandName'));
const sellerOptions = computed(() => getOptions('sellerName'));

const chips = computed(() =>
  (Object.keys(form) as Array<keyof typeof form>)
    .filter((key) => props.state[key])
    .map((key) => ({
      key: key as ParserProductQueryFilterKey,
      label: getLabel(key),
      value: props.state[key]
    }))
    .concat(
      props.state.sort
        ? [{ key: 'sort' as ParserProductQueryFilterKey, label: 'Сортировка', value: props.state.sort }]
        : []
    )
);

function getOptions(key: 'sourceCategory' | 'sourceSubcategory' | 'brandName' | 'sellerName') {
  return [...new Set(props.rows.map((row) => row[key]).filter((value): value is string => Boolean(value?.trim())))]
    .sort((left, right) => left.localeCompare(right, 'ru-RU'));
}

function apply() {
  emit('apply', {
    page: 1,
    ...Object.fromEntries(Object.entries(form).map(([key, value]) => [key, value.trim()]))
  });
}

function getLabel(key: string): string {
  const labels: Record<string, string> = {
    search: 'Поиск',
    sourceCategory: 'Категория',
    sourceSubcategory: 'Подкатегория',
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
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__search">
      <Input v-model="form.search" label="Поиск" placeholder="Название или WB id" />
      <div class="filters__actions">
        <Button type="submit">Применить</Button>
        <Button v-if="chips.length" type="button" variant="ghost" @click="$emit('reset')">Сбросить</Button>
      </div>
    </div>

    <div class="filters__selects">
      <MarketFilterSelect v-model="form.sourceCategory" label="Категория" placeholder="Все категории" :options="categoryOptions" />
      <MarketFilterSelect v-model="form.sourceSubcategory" label="Подкатегория" placeholder="Все подкатегории" :options="subcategoryOptions" />
      <MarketFilterSelect v-model="form.brandName" label="Бренд" placeholder="Все бренды" :options="brandOptions" />
      <MarketFilterSelect v-model="form.sellerName" label="Продавец" placeholder="Все продавцы" :options="sellerOptions" />
    </div>

    <details class="filters__ranges">
      <summary>
        <SlidersHorizontal :size="15" />
        Диапазоны
      </summary>
      <div class="filters__range-grid">
        <Input v-model="form.priceDiscountedFrom" label="Цена от" type="number" />
        <Input v-model="form.priceDiscountedTo" label="Цена до" type="number" />
        <Input v-model="form.reviewRatingFrom" label="Рейтинг от" type="number" />
        <Input v-model="form.reviewRatingTo" label="Рейтинг до" type="number" />
        <Input v-model="form.feedbackCountFrom" label="Отзывы от" type="number" />
        <Input v-model="form.feedbackCountTo" label="Отзывы до" type="number" />
      </div>
    </details>

    <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры товаров маркетплейса">
      <button
        v-for="chip in chips"
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
  z-index: 3;
  display: grid;
  gap: var(--space-3);
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

.filters__ranges {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  padding: var(--space-2) var(--space-3);
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
  background: var(--surface-control-raised);
  color: var(--color-text-muted);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
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
  .filters__search {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__selects {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }

  .filters__range-grid {
    grid-template-columns: repeat(6, minmax(7rem, 1fr));
  }
}
</style>
