<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import type { ParserProductQueryFilterKey } from './parserProductsQuery';
import type { ParserProductQueryState } from './parserProducts.types';

const props = defineProps<{
  state: ParserProductQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ParserProductQueryState>];
  reset: [];
  remove: [key: ParserProductQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  parserRunId: props.state.parserRunId,
  sourceCategory: props.state.sourceCategory,
  sourceSubcategory: props.state.sourceSubcategory,
  brandName: props.state.brandName,
  sellerName: props.state.sellerName,
  wbRootId: props.state.wbRootId,
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
    form.parserRunId = state.parserRunId;
    form.sourceCategory = state.sourceCategory;
    form.sourceSubcategory = state.sourceSubcategory;
    form.brandName = state.brandName;
    form.sellerName = state.sellerName;
    form.wbRootId = state.wbRootId;
    form.priceDiscountedFrom = state.priceDiscountedFrom;
    form.priceDiscountedTo = state.priceDiscountedTo;
    form.reviewRatingFrom = state.reviewRatingFrom;
    form.reviewRatingTo = state.reviewRatingTo;
    form.feedbackCountFrom = state.feedbackCountFrom;
    form.feedbackCountTo = state.feedbackCountTo;
  },
  { deep: true }
);

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
        ? [{ key: 'sort' as ParserProductQueryFilterKey, label: 'Sort', value: props.state.sort }]
        : []
    )
);

function apply() {
  emit('apply', {
    page: 1,
    ...Object.fromEntries(
      Object.entries(form).map(([key, value]) => [key, value.trim()])
    )
  });
}

function getLabel(key: string): string {
  const labels: Record<string, string> = {
    search: 'Search',
    parserRunId: 'Run',
    sourceCategory: 'Category',
    sourceSubcategory: 'Subcategory',
    brandName: 'Brand',
    sellerName: 'Seller',
    wbRootId: 'Root',
    priceDiscountedFrom: 'Price from',
    priceDiscountedTo: 'Price to',
    reviewRatingFrom: 'Rating from',
    reviewRatingTo: 'Rating to',
    feedbackCountFrom: 'Feedback from',
    feedbackCountTo: 'Feedback to'
  };

  return labels[key] ?? key;
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Name, WB id, brand, seller" />
      <Input v-model="form.parserRunId" label="Parser run" placeholder="wb_products_..." />
      <Input v-model="form.wbRootId" label="WB root id" placeholder="root id" />
      <div class="filters__actions">
        <Button type="submit">Apply</Button>
        <Button v-if="chips.length" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__grid">
      <Input v-model="form.sourceCategory" label="Source category" />
      <Input v-model="form.sourceSubcategory" label="Source subcategory" />
      <Input v-model="form.brandName" label="Brand" />
      <Input v-model="form.sellerName" label="Seller" />
      <Input v-model="form.priceDiscountedFrom" label="Discounted price from" type="number" />
      <Input v-model="form.priceDiscountedTo" label="Discounted price to" type="number" />
      <Input v-model="form.reviewRatingFrom" label="Review rating from" type="number" />
      <Input v-model="form.reviewRatingTo" label="Review rating to" type="number" />
      <Input v-model="form.feedbackCountFrom" label="Feedback count from" type="number" />
      <Input v-model="form.feedbackCountTo" label="Feedback count to" type="number" />
    </div>

    <div v-if="chips.length" class="filters__chips" aria-label="Active parsed product filters">
      <button
        v-for="chip in chips"
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
.filters__grid {
  display: grid;
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
  .filters__toolbar {
    grid-template-columns: minmax(18rem, 1fr) minmax(13rem, 0.7fr) minmax(10rem, 0.45fr) auto;
    align-items: end;
  }

  .filters__grid {
    grid-template-columns: repeat(5, minmax(8rem, 1fr));
  }
}
</style>
