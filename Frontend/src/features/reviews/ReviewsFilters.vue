<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  compactId,
  getReplyStateLabel,
  getReviewSortLabel
} from './reviewDisplay';
import type { ReviewQueryFilterKey } from './reviewsQuery';
import type { ReviewQueryState } from './reviews.types';

const props = defineProps<{
  state: ReviewQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ReviewQueryState>];
  reset: [];
  remove: [key: ReviewQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idProduct: props.state.idProduct,
  rating: props.state.rating,
  isReplied: props.state.isReplied,
  dateCreateFrom: props.state.dateCreateFrom,
  dateCreateTo: props.state.dateCreateTo
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idProduct = state.idProduct;
    form.rating = state.rating;
    form.isReplied = state.isReplied;
    form.dateCreateFrom = state.dateCreateFrom;
    form.dateCreateTo = state.dateCreateTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: ReviewQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Search', value: props.state.search });
  }

  if (props.state.idProduct) {
    chips.push({ key: 'idProduct', label: 'Product', value: compactId(props.state.idProduct) });
  }

  if (props.state.rating) {
    chips.push({ key: 'rating', label: 'Rating', value: props.state.rating });
  }

  if (props.state.isReplied) {
    chips.push({
      key: 'isReplied',
      label: 'Reply state',
      value: getReplyStateLabel(props.state.isReplied === 'true')
    });
  }

  if (props.state.dateCreateFrom) {
    chips.push({ key: 'dateCreateFrom', label: 'Created from', value: props.state.dateCreateFrom });
  }

  if (props.state.dateCreateTo) {
    chips.push({ key: 'dateCreateTo', label: 'Created to', value: props.state.dateCreateTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getReviewSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idProduct: form.idProduct.trim(),
    rating: form.rating.trim(),
    isReplied: form.isReplied,
    dateCreateFrom: form.dateCreateFrom,
    dateCreateTo: form.dateCreateTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Marketplace ID or review text" />

      <Input v-model="form.rating" type="number" label="Rating" placeholder="0+" />

      <label class="filters__select-field">
        <span>Reply state</span>
        <select v-model="form.isReplied" class="filters__select">
          <option value="">Any</option>
          <option value="true">Replied</option>
          <option value="false">Unreplied</option>
        </select>
      </label>

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__secondary">
      <Input v-model="form.idProduct" label="Product ID" placeholder="uuid" />
      <Input v-model="form.dateCreateFrom" type="date" label="Created from" />
      <Input v-model="form.dateCreateTo" type="date" label="Created to" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active review filters">
      <button
        v-for="chip in activeChips"
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
.filters__secondary {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

.filters__select-field {
  display: grid;
  gap: var(--space-1);
}

.filters__select-field span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.filters__select {
  height: 2.25rem;
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
  outline: none;
}

.filters__select:focus {
  border-color: var(--color-primary);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
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
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--color-text-muted);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
  line-height: 1;
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
  color: var(--color-text);
  outline: none;
}

.filters__chip strong {
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
}

@media (min-width: 980px) {
  .filters__toolbar {
    grid-template-columns: minmax(18rem, 1fr) minmax(8rem, 0.18fr) minmax(10rem, 0.24fr) auto;
    align-items: end;
  }

  .filters__secondary {
    grid-template-columns: minmax(16rem, 1fr) minmax(10rem, 0.35fr) minmax(10rem, 0.35fr);
  }
}
</style>
