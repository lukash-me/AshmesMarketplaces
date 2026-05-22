<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ParserProductDetailDrawer from './ParserProductDetailDrawer.vue';
import { getParserProducts } from './parserProducts.api';
import { getParserReviews } from '@/features/parser-reviews/parserReviews.api';
import ParserProductsFilters from './ParserProductsFilters.vue';
import {
  parseParserProductsQuery,
  removeParserProductQueryFilter,
  resetParserProductQueryFilters,
  toParserProductsApiParams,
  toParserProductsRouteQuery,
  type ParserProductQueryFilterKey
} from './parserProductsQuery';
import ParserProductsTable from './ParserProductsTable.vue';
import type {
  ParserProductListItem,
  ParserProductQueryState,
  ParserProductReviewPresence
} from './parserProducts.types';

const route = useRoute();
const router = useRouter();
const queryState = ref<ParserProductQueryState>(parseParserProductsQuery(route.query));
const rows = ref<ParserProductListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selected = ref<ParserProductListItem | null>(null);
const reviewPresence = ref<Record<string, ParserProductReviewPresence>>({});
const reviewPresenceCache = new Map<string, ParserProductReviewPresence>();
const activeReviewPresenceScopes = new Set<string>();

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseParserProductsQuery(query);
    await loadRows();
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
    void loadReviewPresence(response.items);
  } catch (err) {
    rows.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Не удалось загрузить товары маркетплейса.');
  } finally {
    loading.value = false;
  }
}

async function loadReviewPresence(visibleRows: ParserProductListItem[]) {
  const rowsByScope = new Map<string, ParserProductListItem[]>();
  for (const row of visibleRows) {
    const key = reviewPresenceScope(row);
    rowsByScope.set(key, [...(rowsByScope.get(key) ?? []), row]);
  }

  const queue: Array<[string, ParserProductListItem[]]> = [];
  for (const [key, scopeRows] of rowsByScope) {
    const cached = reviewPresenceCache.get(key);
    if (cached) {
      setReviewPresence(scopeRows, cached);
      continue;
    }

    setReviewPresence(scopeRows, { status: 'loading' });
    if (!activeReviewPresenceScopes.has(key)) {
      activeReviewPresenceScopes.add(key);
      queue.push([key, scopeRows]);
    }
  }

  await Promise.all(
    Array.from({ length: Math.min(4, queue.length) }, async () => {
      while (queue.length) {
        const next = queue.shift();
        if (!next) {
          return;
        }

        const [key, scopeRows] = next;
        const result = await probeReviewPresence(scopeRows[0]);
        reviewPresenceCache.set(key, result);
        activeReviewPresenceScopes.delete(key);
        setReviewPresence(scopeRows, result);
      }
    })
  );
}

async function probeReviewPresence(row: ParserProductListItem): Promise<ParserProductReviewPresence> {
  try {
    const response = await getParserReviews({
      page: 1,
      pageSize: 1,
      sort: '-createdAtOnMp',
      ...(row.wbRootId ? { sourceWbRootId: row.wbRootId } : { wbProductId: row.wbProductId })
    });
    return {
      status: response.totalCount > 0 ? 'present' : 'absent'
    };
  } catch {
    return { status: 'error' };
  }
}

function reviewPresenceScope(row: ParserProductListItem): string {
  return row.wbRootId ? `root:${row.wbRootId}` : `product:${row.wbProductId}`;
}

function setReviewPresence(
  scopeRows: ParserProductListItem[],
  presence: ParserProductReviewPresence
) {
  reviewPresence.value = {
    ...reviewPresence.value,
    ...Object.fromEntries(scopeRows.map((row) => [row.id, presence]))
  };
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
  <div class="parser-products-page">
    <PageHeader
      title="Товары маркетплейса"
      description="Наблюдаемые товары маркетплейса для анализа рынка. Это не каталог раздела «Мои товары»."
    />

    <ParserProductsFilters
      :state="queryState"
      :rows="rows"
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
      description="Измените фильтры или загрузите данные о товарах маркетплейса."
    />
    <ParserProductsTable
      v-else
      :rows="rows"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selected?.id"
      :review-presence="reviewPresence"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="selected = $event"
    />

    <ParserProductDetailDrawer :open="Boolean(selected)" :product="selected" @close="selected = null" />
  </div>
</template>

<style scoped>
.parser-products-page {
  display: grid;
  gap: var(--space-4);
}
</style>
