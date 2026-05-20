<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  compactId,
  getCampaignSortLabel,
  getCampaignStatusLabel,
  getCampaignTypeLabel
} from './campaignDisplay';
import type { CampaignQueryFilterKey } from './campaignsQuery';
import type { CampaignQueryState } from './campaigns.types';

const props = defineProps<{
  state: CampaignQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<CampaignQueryState>];
  reset: [];
  remove: [key: CampaignQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idProduct: props.state.idProduct,
  idSetCampaign: props.state.idSetCampaign,
  status: props.state.status,
  type: props.state.type,
  dateStartFrom: props.state.dateStartFrom,
  dateStartTo: props.state.dateStartTo,
  dateEndFrom: props.state.dateEndFrom,
  dateEndTo: props.state.dateEndTo
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idProduct = state.idProduct;
    form.idSetCampaign = state.idSetCampaign;
    form.status = state.status;
    form.type = state.type;
    form.dateStartFrom = state.dateStartFrom;
    form.dateStartTo = state.dateStartTo;
    form.dateEndFrom = state.dateEndFrom;
    form.dateEndTo = state.dateEndTo;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: CampaignQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Search', value: props.state.search });
  }

  if (props.state.idProduct) {
    chips.push({ key: 'idProduct', label: 'Product', value: compactId(props.state.idProduct) });
  }

  if (props.state.idSetCampaign) {
    chips.push({
      key: 'idSetCampaign',
      label: 'Rule set',
      value: compactId(props.state.idSetCampaign)
    });
  }

  if (props.state.status) {
    chips.push({
      key: 'status',
      label: 'Status',
      value: getCampaignStatusLabel(Number(props.state.status))
    });
  }

  if (props.state.type) {
    chips.push({
      key: 'type',
      label: 'Type',
      value: getCampaignTypeLabel(Number(props.state.type))
    });
  }

  if (props.state.dateStartFrom) {
    chips.push({ key: 'dateStartFrom', label: 'Start from', value: props.state.dateStartFrom });
  }

  if (props.state.dateStartTo) {
    chips.push({ key: 'dateStartTo', label: 'Start to', value: props.state.dateStartTo });
  }

  if (props.state.dateEndFrom) {
    chips.push({ key: 'dateEndFrom', label: 'End from', value: props.state.dateEndFrom });
  }

  if (props.state.dateEndTo) {
    chips.push({ key: 'dateEndTo', label: 'End to', value: props.state.dateEndTo });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getCampaignSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idProduct: form.idProduct.trim(),
    idSetCampaign: form.idSetCampaign.trim(),
    status: form.status.trim(),
    type: form.type.trim(),
    dateStartFrom: form.dateStartFrom,
    dateStartTo: form.dateStartTo,
    dateEndFrom: form.dateEndFrom,
    dateEndTo: form.dateEndTo
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Name, region or description" />

      <Input v-model="form.status" type="number" label="Status code" placeholder="0+" />

      <Input v-model="form.type" type="number" label="Type code" placeholder="0+" />

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__secondary">
      <Input v-model="form.idProduct" label="Product ID" placeholder="uuid" />
      <Input v-model="form.idSetCampaign" label="Rule set ID" placeholder="uuid" />
      <Input v-model="form.dateStartFrom" type="date" label="Start from" />
      <Input v-model="form.dateStartTo" type="date" label="Start to" />
      <Input v-model="form.dateEndFrom" type="date" label="End from" />
      <Input v-model="form.dateEndTo" type="date" label="End to" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active campaign filters">
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
    grid-template-columns: minmax(18rem, 1fr) minmax(8rem, 0.2fr) minmax(8rem, 0.2fr) auto;
    align-items: end;
  }

  .filters__secondary {
    grid-template-columns: repeat(2, minmax(12rem, 1fr)) repeat(4, minmax(9rem, 0.6fr));
  }
}
</style>
