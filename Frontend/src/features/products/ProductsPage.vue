<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import KpiGrid from '@/widgets/KpiGrid.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ProductDetailDrawer from './ProductDetailDrawer.vue';
import { getProductHeatTier } from './productSignals';
import { getProducts } from './products.api';
import ProductsFilters from './ProductsFilters.vue';
import ProductsTable from './ProductsTable.vue';
import {
  parseProductsQuery,
  removeProductQueryFilter,
  resetProductQueryFilters,
  toProductsApiParams,
  toProductsRouteQuery,
  type ProductQueryFilterKey
} from './productsQuery';
import type { ProductListItem, ProductQueryState } from './products.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<ProductQueryState>(parseProductsQuery(route.query));
const products = ref<ProductListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedProduct = ref<ProductListItem | null>(null);

const kpis = computed(() => {
  const activeCount = products.value.filter((product) => product.status === 2).length;
  const hotCount = products.value.filter((product) => getProductHeatTier(product) === 'hot').length;
  const latestUpdate = products.value
    .map((product) => new Date(product.dateUpdated).getTime())
    .filter(Number.isFinite)
    .sort((a, b) => b - a)[0];

  return [
    { label: 'Products loaded', value: products.value.length, caption: `${totalCount.value} total` },
    { label: 'Active on page', value: activeCount, caption: 'Status 2 records' },
    { label: 'Hot signals', value: hotCount, caption: 'Status/update scaffold' },
    {
      label: 'Latest update',
      value: latestUpdate ? new Intl.DateTimeFormat('en', { month: 'short', day: '2-digit' }).format(latestUpdate) : '-',
      caption: 'Loaded page only'
    }
  ];
});

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseProductsQuery(query);
    await loadProducts();
  },
  { immediate: true }
);

async function loadProducts() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getProducts(toProductsApiParams(queryState.value));
    products.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    products.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load products.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<ProductQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toProductsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toProductsRouteQuery(resetProductQueryFilters(queryState.value))
  });
}

function removeFilter(key: ProductQueryFilterKey) {
  void router.replace({
    query: toProductsRouteQuery(removeProductQueryFilter(queryState.value, key))
  });
}

function openProduct(row: ProductListItem) {
  selectedProduct.value = row;
}

function closeProduct() {
  selectedProduct.value = null;
}
</script>

<template>
  <div class="products-page">
    <PageHeader
      title="Products"
      description="Operational product catalog with marketplace identifiers, status, commission and update recency signals."
    />

    <KpiGrid :items="kpis" />

    <ProductsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Products could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="products.length === 0"
      class="app-surface"
      title="No products found"
      description="Adjust filters or add products through the backend API."
    />

    <ProductsTable
      v-else
      :rows="products"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedProduct?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openProduct"
    />

    <ProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="closeProduct"
    />
  </div>
</template>

<style scoped>
.products-page {
  display: grid;
  gap: var(--space-4);
}
</style>
