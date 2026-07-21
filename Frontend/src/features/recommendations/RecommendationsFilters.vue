<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  compactId,
  getRecommendationObjectTypeLabel,
  getRecommendationSortLabel,
  getRecommendationTypeLabel
} from './recommendationDisplay';
import type { RecommendationQueryFilterKey } from './recommendationsQuery';
import type { RecommendationQueryState } from './recommendations.types';

const props = defineProps<{
  state: RecommendationQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<RecommendationQueryState>];
  reset: [];
  remove: [key: RecommendationQueryFilterKey];
}>();

const form = reactive({
  idModel: props.state.idModel,
  type: props.state.type,
  typeObject: props.state.typeObject,
  dateCreateFrom: props.state.dateCreateFrom,
  dateCreateTo: props.state.dateCreateTo
});

watch(
  () => props.state,
  (state) => {
    form.idModel = state.idModel;
    form.type = state.type;
    form.typeObject = state.typeObject;
    form.dateCreateFrom = state.dateCreateFrom;
    form.dateCreateTo = state.dateCreateTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: RecommendationQueryFilterKey; label: string; value: string }> = [];

  if (props.state.idModel) {
    chips.push({ key: 'idModel', label: 'Model', value: compactId(props.state.idModel) });
  }

  if (props.state.type) {
    chips.push({
      key: 'type',
      label: 'Type',
      value: getRecommendationTypeLabel(Number(props.state.type))
    });
  }

  if (props.state.typeObject) {
    chips.push({
      key: 'typeObject',
      label: 'Object type',
      value: getRecommendationObjectTypeLabel(Number(props.state.typeObject))
    });
  }

  if (props.state.dateCreateFrom) {
    chips.push({ key: 'dateCreateFrom', label: 'Created from', value: props.state.dateCreateFrom });
  }

  if (props.state.dateCreateTo) {
    chips.push({ key: 'dateCreateTo', label: 'Created to', value: props.state.dateCreateTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getRecommendationSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    idModel: form.idModel.trim(),
    type: form.type.trim(),
    typeObject: form.typeObject.trim(),
    dateCreateFrom: form.dateCreateFrom,
    dateCreateTo: form.dateCreateTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.idModel" label="Model ID" placeholder="uuid" />

      <Input v-model="form.type" type="number" label="Type code" placeholder="0+" />

      <Input v-model="form.typeObject" type="number" label="Object type code" placeholder="0+" />

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">
          Reset
        </Button>
      </div>
    </div>

    <div class="filters__dates">
      <Input v-model="form.dateCreateFrom" type="date" label="Created from" />
      <Input v-model="form.dateCreateTo" type="date" label="Created to" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active recommendation filters">
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
.filters__dates {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
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

@media (min-width: 840px) {
  .filters__toolbar {
    grid-template-columns: minmax(16rem, 1fr) minmax(8rem, 0.24fr) minmax(10rem, 0.28fr) auto;
    align-items: end;
  }

  .filters__dates {
    grid-template-columns: repeat(2, minmax(10rem, 0.5fr));
  }
}
</style>
