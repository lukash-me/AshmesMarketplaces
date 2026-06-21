<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import PageHeader from '@/widgets/PageHeader.vue';

import MarketConcentrationSection from './MarketConcentrationSection.vue';
import { getPublicMarketIntelligence } from './marketIntelligence.api';
import {
  buildMarketIntelligenceParams,
  isMarketIntelligenceSubcategory,
  marketIntelligenceContexts,
  marketIntelligenceDefaultRegionDest,
  marketIntelligenceDefaultSort,
  resolveMarketIntelligenceContext
} from './marketIntelligence.contexts';
import type { PublicMarketIntelligence, PublicMarketIntelligenceParams } from './marketIntelligence.types';

const route = useRoute();
const router = useRouter();

const selectedSubcategory = ref(readInitialSubcategory());
const intelligence = ref<PublicMarketIntelligence | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

const selectedContext = computed(() => resolveMarketIntelligenceContext(selectedSubcategory.value));
const marketConcentration = computed(() => intelligence.value?.marketConcentration ?? null);
const requestParams = computed<PublicMarketIntelligenceParams>(() => buildMarketIntelligenceParams(
  selectedContext.value,
  {
    sourceRegionDest: readStringQuery('sourceRegionDest') ?? marketIntelligenceDefaultRegionDest,
    sort: readStringQuery('sort') ?? marketIntelligenceDefaultSort,
    latestRankRunId: readStringQuery('latestRankRunId') ?? undefined,
    baselineRankRunId: readStringQuery('baselineRankRunId') ?? undefined,
    latestProductRunId: readStringQuery('latestProductRunId') ?? undefined,
    baselineProductRunId: readStringQuery('baselineProductRunId') ?? undefined
  }
));

onMounted(() => {
  void refresh();
});

async function refresh(): Promise<void> {
  loading.value = true;
  error.value = null;

  try {
    intelligence.value = await getPublicMarketIntelligence(requestParams.value);
  } catch (requestError) {
    intelligence.value = null;
    error.value = getProblemMessage(requestError, 'Не удалось загрузить концентрацию рынка.');
  } finally {
    loading.value = false;
  }
}

function applySubcategorySelection(): void {
  void router.replace({
    query: {
      ...route.query,
      sourceSubcategory: selectedSubcategory.value
    }
  });
  void refresh();
}

function readInitialSubcategory(): string {
  const raw = readStringQuery('sourceSubcategory');
  return isMarketIntelligenceSubcategory(raw)
    ? raw
    : marketIntelligenceContexts[0].sourceSubcategory;
}

function readStringQuery(key: string): string | undefined {
  const raw = route.query[key];
  return typeof raw === 'string' && raw.length > 0 ? raw : undefined;
}
</script>

<template>
  <div class="market-concentration-page">
    <PageHeader
      title="Концентрация рынка"
      subtitle="Показывает, насколько топ выбранной ниши занят крупными продавцами, брендами и повторяющимися root-группами."
    />

    <section class="mi-controls app-surface">
      <div class="mi-controls__fields">
        <label class="mi-field app-select-field">
          <span>Ниша</span>
          <select v-model="selectedSubcategory" class="app-select" @change="applySubcategorySelection">
            <option v-for="context in marketIntelligenceContexts" :key="context.sourceSubcategory" :value="context.sourceSubcategory">
              {{ context.label }}
            </option>
          </select>
        </label>

        <Button class="mi-refresh" variant="primary" :loading="loading" @click="refresh">
          Обновить концентрацию
        </Button>
      </div>
    </section>

    <LoadingState v-if="loading && !intelligence" label="Считаем концентрацию рынка..." />

    <EmptyState v-else-if="error" title="Концентрация не загружена" :description="error" />

    <MarketConcentrationSection
      v-else
      :concentration="marketConcentration"
      :loading="loading"
    />
  </div>
</template>

<style scoped>
.market-concentration-page {
  display: grid;
  gap: var(--space-5);
}

.mi-controls {
  padding: var(--space-4);
}

.mi-controls__fields {
  display: grid;
  grid-template-columns: minmax(16rem, 1fr) auto;
  gap: var(--space-3);
  align-items: end;
}

.mi-refresh {
  min-height: 2.45rem;
}

@media (max-width: 760px) {
  .mi-controls__fields {
    grid-template-columns: 1fr;
  }

  .mi-refresh {
    width: 100%;
  }
}
</style>
