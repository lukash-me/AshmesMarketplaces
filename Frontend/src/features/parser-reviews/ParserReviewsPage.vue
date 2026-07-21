<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ParserReviewDetailDrawer from './ParserReviewDetailDrawer.vue';
import { getParserReviews } from './parserReviews.api';
import {
  parseParserReviewsQuery,
  removeParserReviewQueryFilter,
  resetParserReviewQueryFilters,
  toParserReviewsApiParams,
  toParserReviewsRouteQuery,
  type ParserReviewQueryFilterKey
} from './parserReviewsQuery';
import type { ParserReviewListItem, ParserReviewQueryState } from './parserReviews.types';
import ParserReviewsFilters from './ParserReviewsFilters.vue';
import ParserReviewsTable from './ParserReviewsTable.vue';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const queryState = ref<ParserReviewQueryState>(parseParserReviewsQuery(route.query));
const rows = ref<ParserReviewListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selected = ref<ParserReviewListItem | null>(null);
const isGuest = computed(() => !auth.isAuthenticated);

watch(
  () => [route.query, isGuest.value] as const,
  async () => {
    queryState.value = parseParserReviewsQuery(route.query);
    if (isGuest.value) {
      rows.value = [];
      totalCount.value = 0;
      loading.value = false;
      error.value = '';
      return;
    }

    await loadRows();
  },
  { immediate: true }
);

async function loadRows() {
  if (isGuest.value) {
    return;
  }

  loading.value = true;
  error.value = '';

  try {
    const response = await getParserReviews(toParserReviewsApiParams(queryState.value));
    rows.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    rows.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load staged parser reviews.');
  } finally {
    loading.value = false;
  }
}

function updateQuery(patch: Partial<ParserReviewQueryState>) {
  void router.replace({
    query: toParserReviewsRouteQuery({ ...queryState.value, ...patch })
  });
}

function resetFilters() {
  void router.replace({
    query: toParserReviewsRouteQuery(resetParserReviewQueryFilters(queryState.value))
  });
}

function removeFilter(key: ParserReviewQueryFilterKey) {
  void router.replace({
    query: toParserReviewsRouteQuery(removeParserReviewQueryFilter(queryState.value, key))
  });
}
</script>

<template>
  <div class="parser-reviews-page">
    <PageHeader
      title="Parsed Reviews"
      description="Read-only parser review evidence. Root payload snapshots can be partial or capped and do not prove full review history."
    />

    <AuthRequiredState
      v-if="isGuest"
      description="Отзывы парсера относятся к внутренним данным выгрузки. Войдите, чтобы просматривать служебные evidence-строки."
    />

    <template v-else>
    <ParserReviewsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />
    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Parsed reviews could not be loaded"
      :description="error"
    />
    <EmptyState
      v-else-if="rows.length === 0"
      class="app-surface"
      title="No staged parser reviews found"
      description="Stage parser review rows or adjust the current filters."
    />
    <ParserReviewsTable
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

    <ParserReviewDetailDrawer :open="Boolean(selected)" :review="selected" @close="selected = null" />
    </template>
  </div>
</template>

<style scoped>
.parser-reviews-page {
  display: grid;
  gap: var(--space-4);
}
</style>
