<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import MarketProductDetailDrawer from './ParserProductDetailDrawer.vue';
import { getParserProductFilterOptions, getParserProducts } from './parserProducts.api';
import MarketProductsFilters from './ParserProductsFilters.vue';
import {
  parseParserProductsQuery,
  removeParserProductQueryFilter,
  resetParserProductQueryFilters,
  toParserProductFilterOptionsParams,
  toParserProductsApiParams,
  toParserProductsRouteQuery,
  type ParserProductQueryFilterKey
} from './parserProductsQuery';
import MarketProductsTable from './ParserProductsTable.vue';
import type {
  ParserProductFilterOptions,
  ParserProductListItem,
  ParserProductQueryState
} from './parserProducts.types';

const route = useRoute();
const router = useRouter();
const queryState = ref<ParserProductQueryState>(parseParserProductsQuery(route.query));
const rows = ref<ParserProductListItem[]>([]);
const totalCount = ref(0);
const emptyFilterOptions: ParserProductFilterOptions = {
  categories: [],
  subcategories: [],
  brands: [],
  sellers: []
};
const filterOptions = ref<ParserProductFilterOptions>(emptyFilterOptions);
const filterOptionsLoading = ref(false);
const filterOptionsError = ref('');
const loading = ref(false);
const error = ref('');
const selected = ref<ParserProductListItem | null>(null);
let filterOptionsVersion = 0;

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseParserProductsQuery(query);
    await Promise.all([loadRows(), loadFilterOptions()]);
  },
  { immediate: true }
);

async function loadRows() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getParserProducts(toParserProductsApiParams(queryState.value));
    rows.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    rows.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Не удалось загрузить товары маркетплейса.');
  } finally {
    loading.value = false;
  }
}

async function loadFilterOptions() {
  const version = ++filterOptionsVersion;
  filterOptionsLoading.value = true;
  filterOptionsError.value = '';

  try {
    const response = await getParserProductFilterOptions(
      toParserProductFilterOptionsParams(queryState.value)
    );
    if (version !== filterOptionsVersion) {
      return;
    }

    filterOptions.value = response;
  } catch (err) {
    if (version !== filterOptionsVersion) {
      return;
    }

    filterOptionsError.value = getProblemMessage(
      err,
      'Не удалось загрузить варианты фильтров. Таблица доступна.'
    );
  } finally {
    if (version === filterOptionsVersion) {
      filterOptionsLoading.value = false;
    }
  }
}

function updateQuery(patch: Partial<ParserProductQueryState>) {
  void router.replace({
    query: toParserProductsRouteQuery({ ...queryState.value, ...patch })
  });
}

function resetFilters() {
  void router.replace({
    query: toParserProductsRouteQuery(resetParserProductQueryFilters(queryState.value))
  });
}

function removeFilter(key: ParserProductQueryFilterKey) {
  void router.replace({
    query: toParserProductsRouteQuery(removeParserProductQueryFilter(queryState.value, key))
  });
}
</script>

<template>
  <div class="market-products-page">
    <PageHeader
      title="Товары маркетплейса"
      description="Следите за товарами в выбранных категориях: позиции, цены, рейтинг и отзывы WB в одном экране."
    />

    <MarketProductsFilters
      :state="queryState"
      :filter-options="filterOptions"
      :filter-options-loading="filterOptionsLoading"
      :filter-options-error="filterOptionsError"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />
    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Не удалось загрузить товары маркетплейса"
      :description="error"
    />
    <EmptyState
      v-else-if="rows.length === 0"
      class="app-surface"
      title="Товары маркетплейса не найдены"
      description="Измените фильтры или дождитесь обновления актуальной базы товаров."
    />
    <MarketProductsTable
      v-else
      :rows="rows"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selected?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="selected = $event"
    />

    <MarketProductDetailDrawer :open="Boolean(selected)" :product="selected" @close="selected = null" />
  </div>
</template>

<style scoped>
.market-products-page {
  position: relative;
  display: grid;
  gap: var(--space-4);
}

.market-products-page::before {
  position: absolute;
  z-index: -1;
  inset: -1.5rem -1rem auto;
  height: 18rem;
  background:
    radial-gradient(circle at 78% 0%, rgb(249 115 22 / 0.09), transparent 18rem),
    linear-gradient(180deg, rgb(249 115 22 / 0.025), transparent);
  content: '';
  pointer-events: none;
}
</style>
