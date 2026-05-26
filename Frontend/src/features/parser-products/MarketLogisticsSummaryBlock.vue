<script setup lang="ts">
import { computed } from 'vue';

import type {
  ParserProductLogisticsSummaryAggregate,
  ParserProductQuantityBuckets
} from './parserProducts.types';

const props = defineProps<{
  summary: ParserProductLogisticsSummaryAggregate | null;
  loading: boolean;
  error: boolean;
}>();

type BucketItem = {
  key: keyof ParserProductQuantityBuckets;
  label: string;
  count: number;
};

const hasSummary = computed(() => Boolean(props.summary));
const isEmpty = computed(() => props.summary !== null && props.summary.productsTotal === 0);
const coveragePercent = computed(() => {
  if (!props.summary || props.summary.productsTotal === 0) {
    return null;
  }

  return Math.round((props.summary.productsWithLogistics / props.summary.productsTotal) * 100);
});
const knownBucketItems = computed<BucketItem[]>(() => {
  const buckets = props.summary?.quantityBuckets;

  if (!buckets) {
    return [];
  }

  return [
    { key: 'zero', label: '0', count: buckets.zero },
    { key: 'oneToFive', label: '1–5', count: buckets.oneToFive },
    { key: 'sixToTwenty', label: '6–20', count: buckets.sixToTwenty },
    { key: 'twentyOneToThirtyNine', label: '21–39', count: buckets.twentyOneToThirtyNine },
    { key: 'fortyPlusOrHigh', label: '≥40', count: buckets.fortyPlusOrHigh }
  ];
});
const knownBucketTotal = computed(() => knownBucketItems.value.reduce((sum, item) => sum + item.count, 0));
const unknownBucketCount = computed(() => props.summary?.quantityBuckets.unknown ?? 0);
const destinationLabel = computed(() => {
  const destinations = props.summary?.destinations ?? [];

  if (destinations.length === 0) {
    return '';
  }

  if (destinations.length === 1) {
    return `Регион наблюдения: ${destinations[0].destination?.trim() || 'Нет данных'}`;
  }

  return `Регионов наблюдения: ${formatNumber(destinations.length)}`;
});

function bucketWidth(count: number): string {
  const total = knownBucketTotal.value;
  return total > 0 ? `${Math.max(4, Math.round((count / total) * 100))}%` : '0%';
}

function unknownBucketWidth(): string {
  const total = props.summary?.productsTotal ?? 0;
  return total > 0 ? `${Math.max(4, Math.round((unknownBucketCount.value / total) * 100))}%` : '0%';
}

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : new Intl.NumberFormat('ru-RU').format(value);
}

function formatDecimal(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return 'Нет данных';
  }

  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('ru-RU', {
        month: 'short',
        day: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : 'Нет данных';
}
</script>

<template>
  <section class="mi-section app-surface logistics-summary" aria-labelledby="market-logistics-summary-title">
    <header class="mi-section__header logistics-summary__header">
      <div>
        <h2 id="market-logistics-summary-title">Логистика выборки</h2>
        <p>Покрытие и наблюдаемые остатки WB по текущим фильтрам.</p>
      </div>
      <span v-if="destinationLabel" class="delta-chip delta-chip--neutral">{{ destinationLabel }}</span>
    </header>

    <div v-if="loading && !hasSummary" class="logistics-summary__state">
      <span class="logistics-summary__pulse" aria-hidden="true" />
      <span>Загружаем логистику WB.</span>
    </div>

    <div v-else-if="error" class="logistics-summary__state logistics-summary__state--error">
      Логистическая сводка временно недоступна.
    </div>

    <div v-else-if="isEmpty" class="logistics-summary__state logistics-summary__state--empty">
      В выборке нет товаров для расчета логистики WB.
    </div>

    <template v-else-if="summary">
      <div class="mi-summary-grid logistics-summary__metrics">
        <article class="mi-summary app-surface">
          <span>С логистикой WB</span>
          <strong>{{ formatNumber(summary.productsWithLogistics) }} из {{ formatNumber(summary.productsTotal) }}</strong>
          <p v-if="coveragePercent !== null">{{ coveragePercent }}% выборки</p>
        </article>
        <article class="mi-summary app-surface">
          <span>Товаров в выборке</span>
          <strong>{{ formatNumber(summary.productsTotal) }}</strong>
          <p>{{ formatNumber(summary.productsWithoutLogistics) }} без данных WB</p>
        </article>
        <article class="mi-summary app-surface">
          <span>Типичный остаток WB</span>
          <strong>{{ formatDecimal(summary.quantityMedian) }}</strong>
          <p v-if="summary.quantityAverage !== null">среднее: {{ formatDecimal(summary.quantityAverage) }}</p>
          <p v-else>среднее: Нет данных</p>
        </article>
        <article class="mi-summary app-surface">
          <span>Складов WB</span>
          <strong>{{ formatNumber(summary.distinctWarehouseIds) }}</strong>
          <p>в среднем: {{ formatDecimal(summary.averageWarehousesPerProduct) }} на товар</p>
        </article>
        <article class="mi-summary app-surface">
          <span>Обновлено</span>
          <strong>{{ formatDateTime(summary.latestObservedAtUtc) }}</strong>
          <p>{{ formatNumber(summary.warehouseRowsTotal) }} строк по складам WB</p>
        </article>
      </div>

      <section class="logistics-summary__distribution" aria-label="Распределение остатков WB">
        <div class="logistics-summary__distribution-head">
          <h3>Распределение остатков WB</h3>
          <span>В колонке «Нет данных» — товары, по которым пока нет остатков WB.</span>
        </div>
        <div class="logistics-summary__bucket-grid">
          <div v-for="bucket in knownBucketItems" :key="bucket.key" class="mi-metric logistics-summary__bucket">
            <div class="logistics-summary__bucket-topline">
              <span>{{ bucket.label }}</span>
              <strong>{{ formatNumber(bucket.count) }}</strong>
            </div>
            <div class="logistics-summary__bar" aria-hidden="true">
              <span :style="{ width: bucketWidth(bucket.count) }" />
            </div>
          </div>
          <div class="mi-metric logistics-summary__bucket">
            <div class="logistics-summary__bucket-topline">
              <span>Нет данных</span>
              <strong>{{ formatNumber(unknownBucketCount) }}</strong>
            </div>
            <div class="logistics-summary__bar logistics-summary__bar--muted" aria-hidden="true">
              <span :style="{ width: unknownBucketWidth() }" />
            </div>
          </div>
        </div>
      </section>
    </template>
  </section>
</template>

<style scoped>
.logistics-summary {
  overflow: hidden;
}

.logistics-summary__state {
  display: flex;
  min-height: 5.25rem;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  text-align: center;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  line-height: 1.45;
}

.logistics-summary__state--empty {
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  min-height: 4.25rem;
  background: var(--surface-control);
}

.logistics-summary__state--error {
  color: var(--color-text-muted);
}

.logistics-summary__pulse {
  width: 0.65rem;
  height: 0.65rem;
  border-radius: 999px;
  background: var(--accent-ember);
  box-shadow: 0 0 0 0 rgb(249 115 22 / 0.32);
  animation: logistics-pulse 1.2s ease-out infinite;
}

.logistics-summary__metrics {
  grid-template-columns: repeat(5, minmax(0, 1fr));
}

.logistics-summary__distribution {
  display: grid;
  gap: var(--space-2);
}

.logistics-summary__distribution-head {
  display: grid;
  gap: var(--space-2);
}

.logistics-summary__distribution h3 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.82rem;
  font-weight: 780;
}

.logistics-summary__distribution-head span {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.logistics-summary__bucket-grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: var(--space-2);
}

.logistics-summary__bucket {
  gap: 0.42rem;
}

.logistics-summary__bucket-topline {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  min-width: 0;
  color: var(--color-text-muted);
  font-size: 0.76rem;
}

.logistics-summary__bucket-topline strong {
  color: var(--color-text);
  font-size: 0.84rem;
}

.logistics-summary__bar {
  height: 0.38rem;
  overflow: hidden;
  border-radius: 999px;
  background: rgb(255 255 255 / 0.055);
}

.logistics-summary__bar span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, rgb(249 115 22 / 0.78), rgb(251 191 36 / 0.7));
}

.logistics-summary__bar--muted span {
  background: rgb(154 166 184 / 0.44);
}

@media (max-width: 1180px) {
  .logistics-summary__metrics {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .logistics-summary__bucket-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .logistics-summary__metrics,
  .logistics-summary__bucket-grid {
    grid-template-columns: 1fr;
  }
}

@media (prefers-reduced-motion: reduce) {
  .logistics-summary__pulse {
    animation: none;
  }
}

@keyframes logistics-pulse {
  to {
    box-shadow: 0 0 0 0.55rem rgb(249 115 22 / 0);
  }
}
</style>
