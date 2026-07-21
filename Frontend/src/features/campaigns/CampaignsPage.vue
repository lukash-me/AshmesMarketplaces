<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import CampaignDetailDrawer from './CampaignDetailDrawer.vue';
import { getCampaigns } from './campaigns.api';
import CampaignsFilters from './CampaignsFilters.vue';
import {
  parseCampaignsQuery,
  removeCampaignQueryFilter,
  resetCampaignQueryFilters,
  toCampaignsApiParams,
  toCampaignsRouteQuery,
  type CampaignQueryFilterKey
} from './campaignsQuery';
import CampaignsTable from './CampaignsTable.vue';
import type { CampaignListItem, CampaignQueryState } from './campaigns.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<CampaignQueryState>(parseCampaignsQuery(route.query));
const campaigns = ref<CampaignListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedCampaign = ref<CampaignListItem | null>(null);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseCampaignsQuery(query);
    await loadCampaigns();
  },
  { immediate: true }
);

async function loadCampaigns() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getCampaigns(toCampaignsApiParams(queryState.value));
    campaigns.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    campaigns.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load campaigns.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<CampaignQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toCampaignsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toCampaignsRouteQuery(resetCampaignQueryFilters(queryState.value))
  });
}

function removeFilter(key: CampaignQueryFilterKey) {
  void router.replace({
    query: toCampaignsRouteQuery(removeCampaignQueryFilter(queryState.value, key))
  });
}

function openCampaign(row: CampaignListItem) {
  selectedCampaign.value = row;
}

function closeCampaign() {
  selectedCampaign.value = null;
}
</script>

<template>
  <div class="campaigns-page">
    <PageHeader
      title="Campaigns"
      description="Read-only marketplace advertising campaigns with product links, budget fields and linked metrics."
    />

    <CampaignsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Campaigns could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="campaigns.length === 0"
      class="app-surface"
      title="No campaigns found"
      description="Adjust filters or load campaigns through the existing backend API."
    />

    <CampaignsTable
      v-else
      :rows="campaigns"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedCampaign?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openCampaign"
    />

    <CampaignDetailDrawer
      :open="Boolean(selectedCampaign)"
      :campaign="selectedCampaign"
      @close="closeCampaign"
    />
  </div>
</template>

<style scoped>
.campaigns-page {
  display: grid;
  gap: var(--space-4);
}
</style>
