<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import { compactId, getLogisticSortLabel, getLogisticTypeLabel } from './logisticDisplay';
import type { LogisticQueryFilterKey } from './logisticsQuery';
import type { LogisticQueryState } from './logistics.types';

const props = defineProps<{
  state: LogisticQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<LogisticQueryState>];
  reset: [];
  remove: [key: LogisticQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idProduct: props.state.idProduct,
  idWarehouse: props.state.idWarehouse,
  type: props.state.type,
  dateFrom: props.state.dateFrom,
  dateTo: props.state.dateTo
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idProduct = state.idProduct;
    form.idWarehouse = state.idWarehouse;
    form.type = state.type;
    form.dateFrom = state.dateFrom;
    form.dateTo = state.dateTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: LogisticQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Warehouse search', value: props.state.search });
  }

  if (props.state.idProduct) {
    chips.push({ key: 'idProduct', label: 'Product', value: compactId(props.state.idProduct) });
  }

  if (props.state.idWarehouse) {
    chips.push({
      key: 'idWarehouse',
      label: 'Warehouse',
      value: compactId(props.state.idWarehouse)
    });
  }

  if (props.state.type) {
    chips.push({
      key: 'type',
      label: 'Type',
      value: getLogisticTypeLabel(Number(props.state.type))
    });
  }

  if (props.state.dateFrom) {
    chips.push({ key: 'dateFrom', label: 'Date from', value: props.state.dateFrom });
  }

  if (props.state.dateTo) {
    chips.push({ key: 'dateTo', label: 'Date to', value: props.state.dateTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getLogisticSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idProduct: form.idProduct.trim(),
    idWarehouse: form.idWarehouse.trim(),
    type: form.type.trim(),
    dateFrom: form.dateFrom,
    dateTo: form.dateTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Warehouse search" placeholder="Name, code, region or city" />

      <Input v-model="form.type" type="number" label="Type code" placeholder="0+" />

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__secondary">
      <Input v-model="form.idProduct" label="Product ID" placeholder="uuid" />
      <Input v-model="form.idWarehouse" label="Warehouse ID" placeholder="uuid" />
      <Input v-model="form.dateFrom" type="date" label="Date from" />
      <Input v-model="form.dateTo" type="date" label="Date to" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active logistics filters">
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
    grid-template-columns: minmax(18rem, 1fr) minmax(8rem, 0.25fr) auto;
    align-items: end;
  }

  .filters__secondary {
    grid-template-columns: repeat(2, minmax(14rem, 1fr)) repeat(2, minmax(10rem, 0.6fr));
  }
}
</style>
