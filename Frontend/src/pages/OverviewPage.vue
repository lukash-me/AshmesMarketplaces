<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import {
  loadOverviewData,
  loadOverviewFilterOptions,
  OVERVIEW_DEFAULT_SUBCATEGORY
} from '@/features/overview/overview.api';
import OverviewMarketSnapshotSection from '@/features/overview/OverviewMarketSnapshotSection.vue';
import type { OverviewData, OverviewFilterOptions } from '@/features/overview/overview.types';
import PageHeader from '@/widgets/PageHeader.vue';

const overviewData = ref<OverviewData | null>(null);
const filterOptions = ref<OverviewFilterOptions | null>(null);
const filterUnavailable = ref(false);
const selectedNiche = ref('');
const loading = ref(true);

const niches = computed(() => filterOptions.value?.subcategories.filter(Boolean) ?? []);

let filterOptionsRequestId = 0;
let overviewDataRequestId = 0;

onMounted(() => {
  void bootstrapOverview();
});

async function bootstrapOverview(): Promise<void> {
  const filterRequestId = ++filterOptionsRequestId;
  const dataRequestId = ++overviewDataRequestId;

  loading.value = true;

  try {
    const options = await loadOverviewFilterOptions();

    if (!isCurrentFilterRequest(filterRequestId) || !isCurrentDataRequest(dataRequestId)) {
      return;
    }

    if (options.status === 'unavailable' || options.data.subcategories.length === 0) {
      const data = await loadOverviewData();

      if (!isCurrentFilterRequest(filterRequestId) || !isCurrentDataRequest(dataRequestId)) {
        return;
      }

      filterOptions.value = null;
      selectedNiche.value = '';
      filterUnavailable.value = true;
      overviewData.value = data;
      return;
    }

    const initialNiche = chooseInitialNiche(options.data.subcategories);
    const data = await loadOverviewData({ sourceSubcategory: initialNiche });

    if (!isCurrentFilterRequest(filterRequestId) || !isCurrentDataRequest(dataRequestId)) {
      return;
    }

    filterOptions.value = options.data;
    selectedNiche.value = initialNiche;
    filterUnavailable.value = false;
    overviewData.value = data;
  } finally {
    if (isCurrentFilterRequest(filterRequestId) && isCurrentDataRequest(dataRequestId)) {
      loading.value = false;
    }
  }
}

async function selectNiche(value: string): Promise<void> {
  if (!value || value === selectedNiche.value) {
    return;
  }

  filterOptionsRequestId += 1;
  const dataRequestId = ++overviewDataRequestId;

  selectedNiche.value = value;
  loading.value = true;

  try {
    const data = await loadOverviewData({ sourceSubcategory: value });

    if (!isCurrentDataRequest(dataRequestId)) {
      return;
    }

    overviewData.value = data;
  } finally {
    if (isCurrentDataRequest(dataRequestId)) {
      loading.value = false;
    }
  }
}

function isCurrentFilterRequest(requestId: number): boolean {
  return requestId === filterOptionsRequestId;
}

function isCurrentDataRequest(requestId: number): boolean {
  return requestId === overviewDataRequestId;
}

function chooseInitialNiche(values: string[]): string {
  return values.includes(OVERVIEW_DEFAULT_SUBCATEGORY)
    ? OVERVIEW_DEFAULT_SUBCATEGORY
    : values[0] ?? '';
}
</script>

<template>
  <div class="overview-page">
    <PageHeader
      title="Обзор"
      description="Краткая сводка по рынку, товарам и событиям, чтобы быстрее понять, куда перейти дальше."
    />

    <OverviewMarketSnapshotSection
      :data="overviewData"
      :loading="loading"
      :filter-unavailable="filterUnavailable"
      :niches="niches"
      :selected-niche="selectedNiche"
      @niche="selectNiche"
    />
  </div>
</template>

<style scoped>
.overview-page {
  display: grid;
  gap: var(--space-5);
}
</style>
