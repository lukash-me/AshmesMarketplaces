<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import { getProductStatusLabel, PRODUCT_STATUS_OPTIONS } from './productSignals';
import type { ProductQueryState } from './products.types';

const props = defineProps<{
  state: ProductQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ProductQueryState>];
  reset: [];
  remove: [key: ProductFilterKey];
}>();

type ProductFilterKey = 'search' | 'idMp' | 'idBrand' | 'idCategory' | 'status' | 'sort';

const form = reactive({
  search: props.state.search,
  idMp: props.state.idMp,
  idBrand: props.state.idBrand,
  idCategory: props.state.idCategory,
  status: props.state.status
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idMp = state.idMp;
    form.idBrand = state.idBrand;
    form.idCategory = state.idCategory;
    form.status = state.status;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: ProductFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Search', value: props.state.search });
  }

  if (props.state.idMp) {
    chips.push({ key: 'idMp', label: 'Marketplace', value: compactValue(props.state.idMp) });
  }

  if (props.state.idBrand) {
    chips.push({ key: 'idBrand', label: 'Brand', value: compactValue(props.state.idBrand) });
  }

  if (props.state.idCategory) {
    chips.push({ key: 'idCategory', label: 'Category', value: compactValue(props.state.idCategory) });
  }

  if (props.state.status) {
    chips.push({
      key: 'status',
      label: 'Status',
      value: getProductStatusLabel(Number(props.state.status))
    });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idMp: form.idMp.trim(),
    idBrand: form.idBrand.trim(),
    idCategory: form.idCategory.trim(),
    status: form.status
  });
}

function compactValue(value: string): string {
  return value.length > 16 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

function getSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'dateUpdated') {
    return `Updated ${direction}`;
  }

  if (field === 'skuSeller') {
    return `Seller SKU ${direction}`;
  }

  return `${field} ${direction}`;
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="Name, seller SKU, marketplace SKU, barcode" />

      <label class="filters__select-field app-select-field">
        <span>Status</span>
        <select v-model="form.status" class="filters__select app-select">
          <option value="">Any status</option>
          <option v-for="option in PRODUCT_STATUS_OPTIONS" :key="option.value" :value="String(option.value)">
            {{ option.label }}
          </option>
        </select>
      </label>

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">Reset</Button>
      </div>
    </div>

    <div class="filters__ids">
      <Input v-model="form.idMp" label="Marketplace ID" placeholder="uuid" />
      <Input v-model="form.idBrand" label="Brand ID" placeholder="uuid" />
      <Input v-model="form.idCategory" label="Category ID" placeholder="uuid" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active product filters">
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
.filters__ids {
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
    grid-template-columns: minmax(18rem, 1fr) minmax(10rem, 0.28fr) auto;
    align-items: end;
  }

  .filters__ids {
    grid-template-columns: repeat(3, minmax(10rem, 1fr));
  }
}
</style>
