<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import RecommendationDetailDrawer from './RecommendationDetailDrawer.vue';
import { getRecommendations } from './recommendations.api';
import RecommendationsFilters from './RecommendationsFilters.vue';
import {
  parseRecommendationsQuery,
  removeRecommendationQueryFilter,
  resetRecommendationQueryFilters,
  toRecommendationsApiParams,
  toRecommendationsRouteQuery,
  type RecommendationQueryFilterKey
} from './recommendationsQuery';
import RecommendationsTable from './RecommendationsTable.vue';
import type { RecommendationListItem, RecommendationQueryState } from './recommendations.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<RecommendationQueryState>(parseRecommendationsQuery(route.query));
const recommendations = ref<RecommendationListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedRecommendation = ref<RecommendationListItem | null>(null);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseRecommendationsQuery(query);
    await loadRecommendations();
  },
  { immediate: true }
);

async function loadRecommendations() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getRecommendations(toRecommendationsApiParams(queryState.value));
    recommendations.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    recommendations.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load recommendations.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<RecommendationQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toRecommendationsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toRecommendationsRouteQuery(resetRecommendationQueryFilters(queryState.value))
  });
}

function removeFilter(key: RecommendationQueryFilterKey) {
  void router.replace({
    query: toRecommendationsRouteQuery(removeRecommendationQueryFilter(queryState.value, key))
  });
}

function openRecommendation(row: RecommendationListItem) {
  selectedRecommendation.value = row;
}

function closeRecommendation() {
  selectedRecommendation.value = null;
}
</script>

<template>
  <div class="recommendations-page">
    <PageHeader
      title="Recommendations"
      description="Read-only stored recommendation records, persisted scores, JSON payloads and linked target IDs."
    />

    <RecommendationsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Recommendations could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="recommendations.length === 0"
      class="app-surface"
      title="No recommendations found"
      description="Adjust filters or load stored recommendation records through the existing backend API."
    />

    <RecommendationsTable
      v-else
      :rows="recommendations"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedRecommendation?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openRecommendation"
    />

    <RecommendationDetailDrawer
      :open="Boolean(selectedRecommendation)"
      :recommendation="selectedRecommendation"
      @close="closeRecommendation"
    />
  </div>
</template>

<style scoped>
.recommendations-page {
  display: grid;
  gap: var(--space-4);
}
</style>
