<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import MarketLogisticsSummaryBlock from '@/features/parser-products/MarketLogisticsSummaryBlock.vue';
import { getParserProductLogisticsSummary } from '@/features/parser-products/parserProducts.api';
import type {
  ParserProductLogisticsSummaryAggregate,
  ParserProductLogisticsSummaryParams
} from '@/features/parser-products/parserProducts.types';
import PageHeader from '@/widgets/PageHeader.vue';

const route = useRoute();
const summary = ref<ParserProductLogisticsSummaryAggregate | null>(null);
const loading = ref(false);
const error = ref(false);
let loadVersion = 0;

watch(
  () => route.query,
  async () => {
    await loadSummary();
  },
  { immediate: true }
);

async function loadSummary() {
  const version = ++loadVersion;
  loading.value = true;
  error.value = false;

  try {
    const response = await getParserProductLogisticsSummary(readSummaryParams());

    if (version !== loadVersion) {
      return;
    }

    summary.value = response;
  } catch {
    if (version !== loadVersion) {
      return;
    }

    summary.value = null;
    error.value = true;
  } finally {
    if (version === loadVersion) {
      loading.value = false;
    }
  }
}

function readSummaryParams(): ParserProductLogisticsSummaryParams {
  return {
    ...stringParam('parserRunId'),
    ...stringParam('search'),
    ...stringParam('sourceCategory'),
    ...stringParam('sourceSubcategory'),
    ...stringParam('brandName'),
    ...stringParam('sellerName'),
    ...stringParam('wbRootId')
  };
}

function stringParam(key: keyof ParserProductLogisticsSummaryParams) {
  const value = route.query[key];
  const text = Array.isArray(value) ? value[0] : value;
  return typeof text === 'string' && text.trim() ? { [key]: text } : {};
}
</script>

<template>
  <div class="logistics-page">
    <PageHeader
      title="Логистика"
      description="Наблюдаемые остатки WB и покрытие логистических данных по текущей выборке."
    />

    <MarketLogisticsSummaryBlock
      :summary="summary"
      :loading="loading"
      :error="error"
    />
  </div>
</template>

<style scoped>
.logistics-page {
  position: relative;
  display: grid;
  gap: var(--space-4);
}

.logistics-page::before {
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
