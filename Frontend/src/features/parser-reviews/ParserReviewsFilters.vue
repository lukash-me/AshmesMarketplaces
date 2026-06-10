<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import type { ParserReviewQueryFilterKey } from './parserReviewsQuery';
import type { ParserReviewQueryState } from './parserReviews.types';

const props = defineProps<{
  state: ParserReviewQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ParserReviewQueryState>];
  reset: [];
  remove: [key: ParserReviewQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  parserRunId: props.state.parserRunId,
  wbProductId: props.state.wbProductId,
  sourceWbRootId: props.state.sourceWbRootId,
  rating: props.state.rating,
  hasObservedReply: props.state.hasObservedReply,
  cappedRootPayload: props.state.cappedRootPayload,
  reviewAttributionMode: props.state.reviewAttributionMode,
  createdAtOnMpFrom: props.state.createdAtOnMpFrom,
  createdAtOnMpTo: props.state.createdAtOnMpTo
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.parserRunId = state.parserRunId;
    form.wbProductId = state.wbProductId;
    form.sourceWbRootId = state.sourceWbRootId;
    form.rating = state.rating;
    form.hasObservedReply = state.hasObservedReply;
    form.cappedRootPayload = state.cappedRootPayload;
    form.reviewAttributionMode = state.reviewAttributionMode;
    form.createdAtOnMpFrom = state.createdAtOnMpFrom;
    form.createdAtOnMpTo = state.createdAtOnMpTo;
  },
  { deep: true }
);

const chips = computed(() => {
  const active = Object.entries(form)
    .filter(([, value]) => value)
    .map(([key, value]) => ({
      key: key as ParserReviewQueryFilterKey,
      label: labels[key] ?? key,
      value
    }));

  return props.state.sort
    ? active.concat([{ key: 'sort', label: 'Sort', value: props.state.sort }])
    : active;
});

const labels: Record<string, string> = {
  search: 'Search',
  parserRunId: 'Run',
  wbProductId: 'WB product',
  sourceWbRootId: 'WB root',
  rating: 'Rating',
  hasObservedReply: 'Observed reply',
  cappedRootPayload: 'Capped payload',
  reviewAttributionMode: 'Attribution',
  createdAtOnMpFrom: 'Created from',
  createdAtOnMpTo: 'Created to'
};

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    parserRunId: form.parserRunId.trim(),
    wbProductId: form.wbProductId.trim(),
    sourceWbRootId: form.sourceWbRootId.trim(),
    rating: form.rating,
    hasObservedReply: form.hasObservedReply,
    cappedRootPayload: form.cappedRootPayload,
    reviewAttributionMode: form.reviewAttributionMode.trim(),
    createdAtOnMpFrom: form.createdAtOnMpFrom,
    createdAtOnMpTo: form.createdAtOnMpTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Text, pros, cons, review id, WB product" />
      <Input v-model="form.parserRunId" label="Parser run" placeholder="wb_reviews_..." />
      <Input v-model="form.wbProductId" label="WB product id" />
      <div class="filters__actions">
        <Button type="submit">Apply</Button>
        <Button v-if="chips.length" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__grid">
      <Input v-model="form.sourceWbRootId" label="Source WB root id" />
      <Input v-model="form.rating" label="Rating" type="number" />
      <label class="select-field app-select-field">
        <span>Observed reply</span>
        <select v-model="form.hasObservedReply" class="select app-select">
          <option value="">Any</option>
          <option value="true">Observed</option>
          <option value="false">Not observed</option>
        </select>
      </label>
      <label class="select-field app-select-field">
        <span>Capped root payload</span>
        <select v-model="form.cappedRootPayload" class="select app-select">
          <option value="">Any</option>
          <option value="true">Capped</option>
          <option value="false">Not flagged</option>
        </select>
      </label>
      <Input v-model="form.reviewAttributionMode" label="Attribution mode" placeholder="root_payload" />
      <Input v-model="form.createdAtOnMpFrom" label="Created from" type="date" />
      <Input v-model="form.createdAtOnMpTo" label="Created to" type="date" />
    </div>

    <div v-if="chips.length" class="filters__chips" aria-label="Active parsed review filters">
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

.select-field {
  display: grid;
  gap: var(--space-1);
}

.select-field span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  text-transform: uppercase;
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
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
}

@media (min-width: 920px) {
  .filters__toolbar {
    grid-template-columns: minmax(18rem, 1fr) minmax(13rem, 0.6fr) minmax(10rem, 0.45fr) auto;
    align-items: end;
  }

  .filters__grid {
    grid-template-columns: repeat(4, minmax(9rem, 1fr));
  }
}
</style>
