<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import LogisticDetailDrawer from './LogisticDetailDrawer.vue';
import { getLogistics } from './logistics.api';
import LogisticsFilters from './LogisticsFilters.vue';
import {
  parseLogisticsQuery,
  removeLogisticQueryFilter,
  resetLogisticQueryFilters,
  toLogisticsApiParams,
  toLogisticsRouteQuery,
  type LogisticQueryFilterKey
} from './logisticsQuery';
import LogisticsTable from './LogisticsTable.vue';
import type { LogisticListItem, LogisticQueryState } from './logistics.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<LogisticQueryState>(parseLogisticsQuery(route.query));
const logistics = ref<LogisticListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedLogistic = ref<LogisticListItem | null>(null);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseLogisticsQuery(query);
    await loadLogistics();
  },
  { immediate: true }
);

async function loadLogistics() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getLogistics(toLogisticsApiParams(queryState.value));
    logistics.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    logistics.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load logistics.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<LogisticQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toLogisticsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toLogisticsRouteQuery(resetLogisticQueryFilters(queryState.value))
  });
}

function removeFilter(key: LogisticQueryFilterKey) {
  void router.replace({
    query: toLogisticsRouteQuery(removeLogisticQueryFilter(queryState.value, key))
  });
}

function openLogistic(row: LogisticListItem) {
  selectedLogistic.value = row;
}

function closeLogistic() {
  selectedLogistic.value = null;
}
</script>

<template>
  <div class="logistics-page">
    <PageHeader
      title="Logistics"
      description="Read-only stock, warehouse linkage, storage costs and logistics cost records."
    />

    <LogisticsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Logistics could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="logistics.length === 0"
      class="app-surface"
      title="No logistics records found"
      description="Adjust filters or load logistics through the existing backend API."
    />

    <LogisticsTable
      v-else
      :rows="logistics"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedLogistic?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openLogistic"
    />

    <LogisticDetailDrawer
      :open="Boolean(selectedLogistic)"
      :logistic="selectedLogistic"
      @close="closeLogistic"
    />
  </div>
</template>

<style scoped>
.logistics-page {
  display: grid;
  gap: var(--space-4);
}
</style>
