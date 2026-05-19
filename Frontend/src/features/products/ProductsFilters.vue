<script setup lang="ts">
import { reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import type { ProductQueryState } from './products.types';

const props = defineProps<{
  state: ProductQueryState;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<ProductQueryState>];
  reset: [];
}>();

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
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <Input v-model="form.search" label="Search" placeholder="Name, SKU, barcode" />
    <Input v-model="form.idMp" label="Marketplace ID" placeholder="uuid" />
    <Input v-model="form.idBrand" label="Brand ID" placeholder="uuid" />
    <Input v-model="form.idCategory" label="Category ID" placeholder="uuid" />
    <Input v-model="form.status" label="Status" placeholder="number" />
    <div class="filters__actions">
      <Button type="submit" variant="primary">Apply</Button>
      <Button type="button" variant="secondary" @click="$emit('reset')">Reset</Button>
    </div>
  </form>
</template>

<style scoped>
.filters {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-4);
  padding: var(--space-4);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

@media (min-width: 840px) {
  .filters {
    grid-template-columns: minmax(14rem, 1.4fr) repeat(4, minmax(9rem, 1fr)) auto;
    align-items: end;
  }
}
</style>
