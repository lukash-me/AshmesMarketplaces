<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import SectionSelector from '@/shared/ui/SectionSelector.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import { getObservedMarketEvents } from './orders.api';
import ObservedMarketEventFilters from './ObservedMarketEventFilters.vue';
import ObservedMarketEventTable from './ObservedMarketEventTable.vue';
import { formatObservedNumber } from './orderDisplay';
import {
  parseObservedMarketEventQuery,
  removeObservedMarketEventQueryFilter,
  resetObservedMarketEventQueryFilters,
  toObservedMarketEventApiParams,
  toObservedMarketEventRouteQuery,
  toObservedMarketEventSummaryApiParams,
  type ObservedMarketEventQueryFilterKey
} from './ordersQuery';
import type {
  ObservedMarketEventItem,
  ObservedMarketEventQueryState,
  ObservedMarketEventResponse,
  ObservedMarketEventSummary,
  ObservedMarketEventTab,
  ObservedMarketEventType
} from './orders.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<ObservedMarketEventQueryState>(parseObservedMarketEventQuery(route.query));
const observedMarketEvents = ref<ObservedMarketEventItem[]>([]);
const response = ref<ObservedMarketEventResponse | null>(null);
const summaryResponse = ref<ObservedMarketEventResponse | null>(null);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedProduct = ref<ParserProductListItem | null>(null);

const tabs: Array<{ key: ObservedMarketEventTab; label: string }> = [
  { key: 'assumed-orders', label: 'Уменьшения остатков' },
  { key: 'new-products', label: 'Новые карточки' },
  { key: 'restocks', label: 'Пополнение остатков' }
];

const hasNotEnoughRunsWarning = computed(() =>
  response.value?.warnings.includes('not_enough_logistics_runs')
  || summaryResponse.value?.warnings.includes('not_enough_logistics_runs')
  || false
);

const summary = computed<ObservedMarketEventSummary | null>(() => summaryResponse.value?.summary ?? response.value?.summary ?? null);
const emptyState = computed(() => getEmptyState(queryState.value.tab));

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseObservedMarketEventQuery(query);
    await loadObservedMarketEvents();
  },
  { immediate: true }
);

async function loadObservedMarketEvents() {
  loading.value = true;
  error.value = '';

  try {
    const [summaryResult, listResult] = await Promise.all([
      getObservedMarketEvents(toObservedMarketEventSummaryApiParams(queryState.value)),
      loadTabResponse(queryState.value)
    ]);

    summaryResponse.value = summaryResult;
    response.value = listResult.response;
    observedMarketEvents.value = listResult.items;
    totalCount.value = listResult.totalCount;
  } catch (err) {
    response.value = null;
    summaryResponse.value = null;
    observedMarketEvents.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Не удалось загрузить события рынка.');
  } finally {
    loading.value = false;
  }
}

async function loadTabResponse(state: ObservedMarketEventQueryState): Promise<{
  response: ObservedMarketEventResponse;
  items: ObservedMarketEventItem[];
  totalCount: number;
}> {
  const eventType = tabEventType(state.tab);
  const nextResponse = await getObservedMarketEvents(toObservedMarketEventApiParams(state, eventType));

  return {
    response: nextResponse,
    items: nextResponse.items,
    totalCount: nextResponse.totalCount
  };
}

async function updateQuery(patch: Partial<ObservedMarketEventQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toObservedMarketEventRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toObservedMarketEventRouteQuery(
      resetObservedMarketEventQueryFilters(queryState.value)
    )
  });
}

function removeFilter(key: ObservedMarketEventQueryFilterKey) {
  void router.replace({
    query: toObservedMarketEventRouteQuery(
      removeObservedMarketEventQueryFilter(queryState.value, key)
    )
  });
}

function selectTab(tab: ObservedMarketEventTab) {
  void updateQuery({ tab, page: 1 });
}

function selectTabValue(tab: string) {
  if (tab === 'assumed-orders' || tab === 'new-products' || tab === 'restocks') {
    selectTab(tab);
  }
}

function openProduct(row: ObservedMarketEventItem) {
  if (!row.parserProductRowId) {
    return;
  }

  selectedProduct.value = {
    id: row.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: row.currentObservedAtUtc ?? row.previousObservedAtUtc ?? '',
    wbProductId: row.wbProductId,
    wbRootId: row.wbRootId,
    name: row.name?.trim() || `WB ${row.wbProductId}`,
    brandName: row.brandName,
    sellerName: row.sellerName,
    priceRegular: row.priceRegular,
    priceDiscounted: row.priceDiscounted,
    priceWbWallet: row.priceWbWallet,
    discountPercent: null,
    totalQuantity: row.currentQuantity,
    ratingRounded: null,
    reviewRating: row.rating,
    feedbackCount: row.feedbackCount,
    sourceCategory: row.sourceCategory,
    sourceSubcategory: row.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: row.imageUrl,
    rank: null,
    position: null,
    logistics: null
  };
}

function tabEventType(tab: ObservedMarketEventTab): ObservedMarketEventType | undefined {
  if (tab === 'assumed-orders') {
    return 'stock_decreased';
  }

  if (tab === 'new-products') {
    return 'new_product_observed';
  }

  if (tab === 'restocks') {
    return 'stock_increased';
  }

  return undefined;
}

function getEmptyState(tab: ObservedMarketEventTab): { title: string; description: string } {
  if (tab === 'assumed-orders') {
    return {
      title: 'Снижения остатков не найдено',
      description: 'В последних наблюдениях не найдено товаров, у которых остаток WB уменьшился.'
    };
  }

  if (tab === 'new-products') {
    return {
      title: 'Новые карточки не найдены',
      description: 'В текущем наблюдении нет карточек, которых не было в предыдущем.'
    };
  }

  if (tab === 'restocks') {
    return {
      title: 'Пополнение остатков не найдено',
      description: 'В пересекающихся товарах не найдено положительных изменений наблюдаемого остатка WB.'
    };
  }

  return {
    title: 'События не найдены',
    description: 'Между последними наблюдениями нет событий для выбранных фильтров.'
  };
}
</script>

<template>
  <div class="orders-page">
    <PageHeader
      title="Логистика и спрос"
      description="Отслеживаем изменения остатков, новые карточки и операционные сигналы рынка между последними наблюдениями."
    />

    <ObservedMarketEventFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <SectionSelector
      class="orders-tabs app-operator-panel"
      :items="tabs"
      :model-value="queryState.tab"
      aria-label="Разделы логистики и спроса"
      @update:model-value="selectTabValue"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Не удалось загрузить данные"
      :description="error"
    />

    <template v-else>
      <section
        v-if="summary && !hasNotEnoughRunsWarning"
        class="orders-summary app-surface app-operator-panel"
        aria-label="Сводка по наблюдаемым событиям"
      >
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.newProductObservedCount) }}</strong>
          Новые карточки
        </span>
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.stockDecreasedCount) }}</strong>
          Снижение остатков
        </span>
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.stockIncreasedCount) }}</strong>
          Пополнение остатков
        </span>
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.comparedPairsCount) }}</strong>
          Сравнено товаров
        </span>
      </section>

      <EmptyState
        v-if="hasNotEnoughRunsWarning"
        class="app-surface"
        title="Недостаточно наблюдений"
        description="Чтобы сравнить изменения, нужно минимум два успешных сбора логистики."
      />

      <EmptyState
        v-else-if="observedMarketEvents.length === 0"
        class="app-surface"
        :title="emptyState.title"
        :description="emptyState.description"
      />

      <ObservedMarketEventTable
        v-else
        :rows="observedMarketEvents"
        :page="queryState.page"
        :page-size="queryState.pageSize"
        :total-count="totalCount"
        :sort="queryState.sort"
        @sort="updateQuery({ page: 1, sort: $event })"
        @page="updateQuery({ page: $event })"
        @open="openProduct"
      />
    </template>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
  </div>
</template>

<style scoped>
.orders-page {
  display: grid;
  gap: var(--space-4);
}

.orders-summary {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
  border-color: var(--operator-border-muted);
  background: var(--operator-panel-bg);
  padding: var(--space-3);
}

.orders-tabs {
  padding: var(--space-3);
}

.orders-summary span {
  display: grid;
  gap: 0.1rem;
  min-width: 0;
  padding: var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.orders-summary strong {
  color: var(--accent-ember-text-strong);
  font-size: 1rem;
  font-weight: 820;
}

@media (max-width: 860px) {
  .orders-summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 520px) {
  .orders-summary {
    grid-template-columns: 1fr;
  }
}
</style>
