<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch, type Component } from 'vue';
import type { RouteLocationRaw } from 'vue-router';
import { ArrowRight, BarChart3, Boxes, ListFilter, Radar } from 'lucide-vue-next';

import AnimatedMetricValue from './AnimatedMetricValue.vue';
import { marketIntelligenceLink } from './overview.api';
import type { OverviewData, OverviewMetric, OverviewObservedEvents } from './overview.types';
import type { ParserProductQuantityBuckets } from '@/features/parser-products/parserProducts.types';

const props = defineProps<{
  data: OverviewData | null;
  loading: boolean;
  filterUnavailable: boolean;
  niches: string[];
  selectedNiche: string;
}>();

const emit = defineEmits<{
  niche: [value: string];
}>();

type SnapshotCard = {
  id: string;
  title: string;
  icon: Component;
  to: RouteLocationRaw;
  metrics: OverviewMetric[];
  unavailable: boolean;
};

type BarItem = {
  key: string;
  label: string;
  value: number;
  metricKey: string;
};

type MetricSnapshotValue = number | string | null;
type MetricSnapshot = Record<string, MetricSnapshotValue>;

const initialMarketLoading = computed(() =>
  props.loading && !props.data && !props.filterUnavailable && props.niches.length === 0
);
const marketUnavailable = computed(() => props.filterUnavailable || (!props.loading && props.niches.length === 0));
const changedMetricKeys = ref<Set<string>>(new Set());
const highlightCycle = ref(0);
const previousMetricSnapshot = ref<MetricSnapshot | null>(null);

let highlightTimerId: number | null = null;

const logisticsSummary = computed(() =>
  props.data?.logisticsSummary.status === 'ready' ? props.data.logisticsSummary.data : null
);
const observedEvents = computed(() =>
  props.data?.observedEvents.status === 'ready' ? props.data.observedEvents.data : null
);
const marketIntelligence = computed(() =>
  props.data?.marketIntelligence.status === 'ready' ? props.data.marketIntelligence.data : null
);

const snapshotCards = computed<SnapshotCard[]>(() => {
  if (marketUnavailable.value) {
    return [];
  }

  return [
    productsCard(),
    eventsCard(),
    intelligenceCard()
  ];
});

const stockBucketItems = computed<BarItem[]>(() => {
  const buckets = logisticsSummary.value?.quantityBuckets;

  if (!buckets) {
    return [];
  }

  return bucketItems(buckets);
});

const eventBarItems = computed<BarItem[]>(() => {
  const events = observedEvents.value;

  if (!events) {
    return [];
  }

  if (events.mode === 'stock-decreases') {
    return [
      {
        key: 'stock-decreased',
        label: 'Снижение остатков',
        value: events.summary.stockDecreased,
        metricKey: 'stockDecreased'
      }
    ];
  }

  return [
    { key: 'new-products', label: 'Новые карточки', value: events.summary.newProducts ?? 0, metricKey: 'newProducts' },
    { key: 'stock-decreased', label: 'Снижение остатков', value: events.summary.stockDecreased, metricKey: 'stockDecreased' },
    { key: 'stock-increased', label: 'Рост остатков', value: events.summary.stockIncreased ?? 0, metricKey: 'stockIncreased' },
    { key: 'missing', label: 'Исчезли из наблюдения', value: events.summary.missingProducts ?? 0, metricKey: 'missingProducts' }
  ];
});

const intelligencePanelMetrics = computed<OverviewMetric[]>(() => {
  const intelligence = marketIntelligence.value;

  if (!intelligence) {
    return [];
  }

  return [
    {
      key: 'intelligenceEvents',
      label: 'Событий',
      value: formatNumber(intelligence.events.length),
      numericValue: intelligence.events.length
    },
    {
      key: 'intelligenceZones',
      label: 'Зон для проверки',
      value: formatNumber(intelligence.competitorWeaknesses.length),
      numericValue: intelligence.competitorWeaknesses.length
    },
    {
      key: 'intelligenceUpdatedAt',
      label: 'Обновлено',
      value: formatDateTime(intelligence.observationWindow.latestObservedAtUtc),
      animate: false
    }
  ];
});

const currentMetricSnapshot = computed<MetricSnapshot>(() => buildMetricSnapshot());

watch(
  currentMetricSnapshot,
  (snapshot) => {
    const previous = previousMetricSnapshot.value;

    if (!previous) {
      previousMetricSnapshot.value = snapshot;
      return;
    }

    const changed = Object.entries(snapshot)
      .filter(([key, value]) => Object.prototype.hasOwnProperty.call(previous, key) && previous[key] !== value)
      .map(([key]) => key);

    previousMetricSnapshot.value = snapshot;

    if (changed.length === 0) {
      return;
    }

    setChangedMetrics(changed);
  },
  { flush: 'post', immediate: true }
);

onBeforeUnmount(() => {
  clearHighlightTimer();
});

function productsCard(): SnapshotCard {
  const resource = props.data?.marketProducts;
  const unavailable = !resource || resource.status === 'unavailable';
  const total = resource?.status === 'ready' ? resource.data.totalCount : null;

  return {
    id: 'products',
    title: 'Товаров в выборке',
    icon: BarChart3,
    to: '/market/products',
    unavailable,
    metrics: unavailable
      ? []
      : [
          {
            key: 'productsTotal',
            label: 'Товаров',
            value: formatNumber(total),
            numericValue: total
          },
          {
            key: 'productsUpdatedAt',
            label: 'Обновлено',
            value: formatDateTime(resource.data.latestProduct?.parsedAtUtc),
            animate: false
          }
        ]
  };
}

function eventsCard(): SnapshotCard {
  const events = observedEvents.value;

  return {
    id: 'events',
    title: 'События рынка',
    icon: Boxes,
    to: '/orders',
    unavailable: !events,
    metrics: events
      ? eventMetrics(events)
      : []
  };
}

function intelligenceCard(): SnapshotCard {
  const intelligence = marketIntelligence.value;

  return {
    id: 'intelligence',
    title: 'Изменения в выдаче',
    icon: Radar,
    to: marketIntelligenceLink(props.selectedNiche),
    unavailable: !intelligence,
    metrics: intelligence
      ? [
          {
            key: 'intelligenceEvents',
            label: 'Событий',
            value: formatNumber(intelligence.events.length),
            numericValue: intelligence.events.length
          },
          {
            key: 'intelligenceZones',
            label: 'Зон для проверки',
            value: formatNumber(intelligence.competitorWeaknesses.length),
            numericValue: intelligence.competitorWeaknesses.length
          },
          {
            key: 'intelligenceUpdatedAt',
            label: 'Обновлено',
            value: formatDateTime(intelligence.observationWindow.latestObservedAtUtc),
            animate: false
          }
        ]
      : []
  };
}

function eventMetrics(events: OverviewObservedEvents): OverviewMetric[] {
  if (events.mode === 'stock-decreases') {
    return [
      {
        key: 'stockDecreased',
        label: 'Снижение остатков',
        value: formatNumber(events.summary.stockDecreased),
        numericValue: events.summary.stockDecreased
      },
      {
        key: 'eventsUpdatedAt',
        label: 'Обновлено',
        value: formatDateTime(events.currentObservedAtUtc),
        animate: false
      }
    ];
  }

  return [
    {
      key: 'newProducts',
      label: 'Новые карточки',
      value: formatNumber(events.summary.newProducts),
      numericValue: events.summary.newProducts
    },
    {
      key: 'stockDecreased',
      label: 'Снижение остатков',
      value: formatNumber(events.summary.stockDecreased),
      numericValue: events.summary.stockDecreased
    },
    {
      key: 'stockIncreased',
      label: 'Рост остатков',
      value: formatNumber(events.summary.stockIncreased),
      numericValue: events.summary.stockIncreased
    }
  ];
}

function bucketItems(buckets: ParserProductQuantityBuckets): BarItem[] {
  return [
    { key: 'zero', label: '0', value: buckets.zero, metricKey: 'quantityBucket.zero' },
    { key: 'oneToFive', label: '1–5', value: buckets.oneToFive, metricKey: 'quantityBucket.oneToFive' },
    { key: 'sixToTwenty', label: '6–20', value: buckets.sixToTwenty, metricKey: 'quantityBucket.sixToTwenty' },
    {
      key: 'twentyOneToThirtyNine',
      label: '21–39',
      value: buckets.twentyOneToThirtyNine,
      metricKey: 'quantityBucket.twentyOneToThirtyNine'
    },
    { key: 'fortyPlusOrHigh', label: '≥40', value: buckets.fortyPlusOrHigh, metricKey: 'quantityBucket.fortyPlusOrHigh' },
    { key: 'unknown', label: 'Нет данных', value: buckets.unknown, metricKey: 'quantityBucket.unknown' }
  ];
}

function barWidth(item: BarItem, items: BarItem[]): string {
  const max = Math.max(...items.map((value) => value.value), 0);
  return max > 0 ? `${Math.max(4, Math.round((item.value / max) * 100))}%` : '0%';
}

function onNicheChange(event: Event): void {
  const value = (event.target as HTMLSelectElement).value;
  emit('niche', value);
}

function metricChanged(key: string | undefined): boolean {
  return key !== undefined && changedMetricKeys.value.has(key);
}

function highlightAnimationClass(key: string | undefined): string {
  if (!metricChanged(key)) {
    return '';
  }

  return highlightCycle.value % 2 === 0
    ? 'overview-highlight-cycle-a'
    : 'overview-highlight-cycle-b';
}

function metricIsAnimated(metric: OverviewMetric): boolean {
  return metric.animate !== false && typeof metric.numericValue === 'number' && Number.isFinite(metric.numericValue);
}

function setChangedMetrics(keys: string[]): void {
  clearHighlightTimer();
  changedMetricKeys.value = new Set(keys);
  highlightCycle.value += 1;
  highlightTimerId = window.setTimeout(() => {
    changedMetricKeys.value = new Set();
    highlightTimerId = null;
  }, 6400);
}

function clearHighlightTimer(): void {
  if (highlightTimerId !== null) {
    window.clearTimeout(highlightTimerId);
    highlightTimerId = null;
  }
}

function buildMetricSnapshot(): MetricSnapshot {
  const snapshot: MetricSnapshot = {};
  const products = props.data?.marketProducts;

  if (products?.status === 'ready') {
    snapshot.productsTotal = products.data.totalCount;
    snapshot.productsUpdatedAt = products.data.latestProduct?.parsedAtUtc ?? null;
  }

  const summary = logisticsSummary.value;

  if (summary) {
    snapshot.productsWithLogistics = summary.productsWithLogistics;
    snapshot.logisticsCoveragePercent = summary.productsTotal > 0
      ? Math.round((summary.productsWithLogistics / summary.productsTotal) * 100)
      : null;

    for (const item of bucketItems(summary.quantityBuckets)) {
      snapshot[item.metricKey] = item.value;
    }
  }

  const events = observedEvents.value;

  if (events) {
    snapshot.newProducts = events.summary.newProducts;
    snapshot.stockDecreased = events.summary.stockDecreased;
    snapshot.stockIncreased = events.summary.stockIncreased;
    snapshot.missingProducts = events.summary.missingProducts;
    snapshot.eventsUpdatedAt = events.currentObservedAtUtc;
  }

  const intelligence = marketIntelligence.value;

  if (intelligence) {
    snapshot.intelligenceEvents = intelligence.events.length;
    snapshot.intelligenceZones = intelligence.competitorWeaknesses.length;
    snapshot.intelligenceUpdatedAt = intelligence.observationWindow.latestObservedAtUtc ?? null;
  }

  return snapshot;
}

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : new Intl.NumberFormat('ru-RU').format(value);
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('ru-RU', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : 'Нет данных';
}
</script>

<template>
  <section class="market-snapshot" aria-labelledby="overview-market-title">
    <header class="market-snapshot__header">
      <label class="market-snapshot__field app-select-field">
        <span>Ниша</span>
        <span v-if="initialMarketLoading" class="market-snapshot__select-skeleton" aria-hidden="true" />
        <select v-else class="app-select" :value="selectedNiche" :disabled="marketUnavailable" @change="onNicheChange">
          <option v-for="niche in niches" :key="niche" :value="niche">{{ niche }}</option>
        </select>
      </label>
    </header>

    <template v-if="initialMarketLoading">
      <div class="market-kpis" aria-hidden="true">
        <article v-for="index in 4" :key="`kpi-skeleton-${index}`" class="market-kpi market-kpi--skeleton app-surface">
          <header class="market-kpi__header">
            <span class="market-kpi__icon market-skeleton" />
            <div class="market-skeleton-stack">
              <span class="market-skeleton market-skeleton--title" />
            </div>
          </header>

          <div class="market-kpi__metrics">
            <div class="market-kpi__metric market-kpi__metric--skeleton">
              <span class="market-skeleton market-skeleton--label" />
              <span class="market-skeleton market-skeleton--value" />
            </div>
            <div class="market-kpi__metric market-kpi__metric--skeleton">
              <span class="market-skeleton market-skeleton--label" />
              <span class="market-skeleton market-skeleton--value" />
            </div>
          </div>

          <span class="market-kpi__link market-skeleton market-skeleton--button" />
        </article>
      </div>

      <div class="market-visuals" aria-hidden="true">
        <section v-for="index in 3" :key="`panel-skeleton-${index}`" class="market-panel market-panel--skeleton app-surface">
          <header class="market-panel__header">
            <span class="market-skeleton market-skeleton--title" />
          </header>
          <div class="bar-list">
            <div v-for="row in 5" :key="`panel-skeleton-${index}-${row}`" class="bar-row">
              <div class="bar-row__top">
                <span class="market-skeleton market-skeleton--label" />
                <span class="market-skeleton market-skeleton--number" />
              </div>
              <div class="market-skeleton market-skeleton--bar" />
            </div>
          </div>
          <span class="market-panel__cta market-skeleton market-skeleton--button" />
        </section>
      </div>
    </template>

    <div v-else-if="marketUnavailable" class="market-snapshot__unavailable app-surface">
      <ListFilter :size="18" />
      <span>Данные временно недоступны.</span>
    </div>

    <template v-else>
      <div class="market-kpis">
        <RouterLink
          v-for="card in snapshotCards"
          :key="card.id"
          class="market-kpi app-surface"
          :class="{ 'market-kpi--unavailable': card.unavailable }"
          :to="card.to"
        >
          <header class="market-kpi__header">
            <span class="market-kpi__icon">
              <component :is="card.icon" :size="16" />
            </span>
            <div>
              <h3>{{ card.title }}</h3>
            </div>
          </header>

          <div v-if="card.unavailable" class="market-kpi__state">Данные временно недоступны.</div>

          <div v-else class="market-kpi__metrics">
            <div
              v-for="metric in card.metrics"
              :key="metric.key ?? metric.label"
              class="market-kpi__metric"
              :class="[
                { 'market-kpi__metric--changed': metricChanged(metric.key) },
                highlightAnimationClass(metric.key)
              ]"
            >
              <span>{{ metric.label }}</span>
              <AnimatedMetricValue
                v-if="metricIsAnimated(metric)"
                :value="metric.numericValue"
                :suffix="metric.suffix"
                :fraction-digits="metric.fractionDigits"
                :class="[
                  { 'overview-value-highlight': metricChanged(metric.key) },
                  highlightAnimationClass(metric.key)
                ]"
              />
              <strong
                v-else
                :class="[
                  { 'overview-value-highlight': metricChanged(metric.key) },
                  highlightAnimationClass(metric.key)
                ]"
              >
                {{ metric.value }}
              </strong>
            </div>
          </div>

          <span class="market-kpi__link">
            Открыть раздел
            <ArrowRight :size="14" />
          </span>
        </RouterLink>
      </div>

      <div class="market-visuals">
        <section class="market-panel app-surface">
          <header class="market-panel__header">
            <h3>Остатки WB по выборке</h3>
          </header>

          <div v-if="stockBucketItems.length === 0" class="market-panel__state">Данные временно недоступны.</div>

          <div v-else class="bar-list">
            <div
              v-for="item in stockBucketItems"
              :key="item.key"
              class="bar-row"
            >
              <div class="bar-row__top">
                <span>{{ item.label }}</span>
                <AnimatedMetricValue
                  :value="item.value"
                  :class="[
                    {
                      'overview-value-highlight': metricChanged(item.metricKey),
                      'bar-row__value--changed': metricChanged(item.metricKey)
                    },
                    highlightAnimationClass(item.metricKey)
                  ]"
                />
              </div>
              <div class="bar-row__track" aria-hidden="true">
                <span
                  :class="[
                    { 'bar-row__fill--changed': metricChanged(item.metricKey) },
                    highlightAnimationClass(item.metricKey)
                  ]"
                  :style="{ width: barWidth(item, stockBucketItems) }"
                />
              </div>
            </div>
          </div>
        </section>

        <section class="market-panel app-surface">
          <header class="market-panel__header">
            <h3>События рынка</h3>
          </header>

          <div v-if="eventBarItems.length === 0" class="market-panel__state">Данные временно недоступны.</div>

          <div v-else class="bar-list">
            <div
              v-for="item in eventBarItems"
              :key="item.key"
              class="bar-row"
            >
              <div class="bar-row__top">
                <span>{{ item.label }}</span>
                <AnimatedMetricValue
                  :value="item.value"
                  :class="[
                    {
                      'overview-value-highlight': metricChanged(item.metricKey),
                      'bar-row__value--changed': metricChanged(item.metricKey)
                    },
                    highlightAnimationClass(item.metricKey)
                  ]"
                />
              </div>
              <div class="bar-row__track" aria-hidden="true">
                <span
                  :class="[
                    { 'bar-row__fill--changed': metricChanged(item.metricKey) },
                    highlightAnimationClass(item.metricKey)
                  ]"
                  :style="{ width: barWidth(item, eventBarItems) }"
                />
              </div>
            </div>
          </div>

          <RouterLink class="market-panel__cta" to="/orders">
            <span>Открыть события</span>
            <ArrowRight :size="14" />
          </RouterLink>
        </section>

        <section class="market-panel app-surface">
          <header class="market-panel__header">
            <h3>Маркетинговая разведка</h3>
          </header>

          <div v-if="!marketIntelligence" class="market-panel__state">Данные временно недоступны.</div>

          <div v-else class="intelligence-mini">
            <div
              v-for="metric in intelligencePanelMetrics"
              :key="metric.key ?? metric.label"
              :class="[
                { 'intelligence-mini__metric--changed': metricChanged(metric.key) },
                highlightAnimationClass(metric.key)
              ]"
            >
              <span>{{ metric.label }}</span>
              <AnimatedMetricValue
                v-if="metricIsAnimated(metric)"
                :value="metric.numericValue"
                :class="[
                  { 'overview-value-highlight': metricChanged(metric.key) },
                  highlightAnimationClass(metric.key)
                ]"
              />
              <strong
                v-else
                :class="[
                  { 'overview-value-highlight': metricChanged(metric.key) },
                  highlightAnimationClass(metric.key)
                ]"
              >
                {{ metric.value }}
              </strong>
            </div>
          </div>

          <RouterLink class="market-panel__cta" :to="marketIntelligenceLink(selectedNiche)">
            <span>Открыть разведку</span>
            <ArrowRight :size="14" />
          </RouterLink>
        </section>
      </div>
    </template>
  </section>
</template>

<style scoped>
.market-snapshot {
  display: grid;
  gap: var(--space-3);
}

.market-snapshot__header,
.market-kpi__header,
.market-panel__header {
  display: flex;
  align-items: flex-start;
  justify-content: flex-start;
  gap: var(--space-3);
}

.market-snapshot__header h2,
.market-snapshot__header p,
.market-kpi h3,
.market-kpi p,
.market-panel__header h3,
.market-panel__header p {
  margin: 0;
}

.market-snapshot__header h2 {
  display: none;
  color: var(--color-text);
  font-size: 1.05rem;
  font-weight: 820;
  letter-spacing: 0;
}

.market-snapshot__header p,
.market-kpi p,
.market-panel__header p,
.market-panel__state,
.market-snapshot__unavailable,
.market-kpi__state {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  line-height: 1.45;
}

.market-snapshot__field {
  display: grid;
  min-width: min(100%, 18rem);
  gap: var(--space-2);
}

.market-kpi__metric span,
.intelligence-mini span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 760;
  text-transform: uppercase;
}

.market-snapshot__field span,
.bar-row__top span {
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 760;
}

.market-snapshot__select-skeleton {
  height: 2.25rem;
  border-radius: var(--radius-md);
}

.market-snapshot__unavailable {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 5rem;
  padding: var(--space-4);
}

.market-kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
}

.market-kpi,
.market-panel {
  border-color: var(--border-ember-subtle);
  background: var(--background-card-ember);
  padding: var(--space-3);
}

.market-kpi {
  display: grid;
  grid-template-rows: auto auto 1fr auto;
  min-height: 14.5rem;
  align-content: start;
  gap: var(--space-3);
}

.market-kpi--unavailable {
  border-color: var(--color-border);
}

.market-kpi--skeleton,
.market-panel--skeleton {
  pointer-events: none;
}

.market-kpi__header {
  justify-content: flex-start;
  min-width: 0;
}

.market-kpi__icon {
  display: grid;
  flex: 0 0 auto;
  width: 1.9rem;
  height: 1.9rem;
  place-items: center;
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-md);
  background: var(--background-brand-mark);
  color: var(--accent-ember-text);
}

.market-kpi h3 {
  color: var(--color-text);
  font-size: 0.93rem;
  font-weight: 820;
  letter-spacing: 0;
}

.market-kpi__metrics,
.intelligence-mini {
  display: grid;
  align-content: start;
  gap: var(--space-2);
}

.market-kpi__metric,
.intelligence-mini div {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
  border: 1px solid var(--surface-metric-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-2);
}

.market-kpi__metric--skeleton {
  min-height: 3.42rem;
}

.market-kpi__metric--changed,
.intelligence-mini__metric--changed {
  animation-duration: 6400ms;
  animation-timing-function: cubic-bezier(0.16, 1, 0.3, 1);
}

.market-kpi__metric--changed.overview-highlight-cycle-a,
.intelligence-mini__metric--changed.overview-highlight-cycle-a {
  animation-name: overview-field-highlight-a;
}

.market-kpi__metric--changed.overview-highlight-cycle-b,
.intelligence-mini__metric--changed.overview-highlight-cycle-b {
  animation-name: overview-field-highlight-b;
}

.market-kpi__metric strong,
.intelligence-mini strong,
.bar-row__top strong {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.overview-value-highlight {
  animation-duration: 6400ms;
  animation-timing-function: cubic-bezier(0.16, 1, 0.3, 1);
}

.overview-value-highlight.overview-highlight-cycle-a {
  animation-name: overview-value-highlight-a;
}

.overview-value-highlight.overview-highlight-cycle-b {
  animation-name: overview-value-highlight-b;
}

.market-kpi__link {
  display: inline-flex;
  align-self: end;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  min-height: 2rem;
  margin-top: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  padding: 0 var(--space-3);
  color: var(--color-text);
  font-size: 0.8125rem;
  font-weight: 720;
}

.market-kpi:hover .market-kpi__link {
  border-color: var(--accent-ember-border);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.market-visuals {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-3);
}

.market-panel {
  display: grid;
  grid-template-rows: auto 1fr auto;
  align-content: start;
  gap: var(--space-3);
}

.market-panel__header {
  display: grid;
  justify-content: stretch;
}

.market-panel__header h3 {
  color: var(--color-text);
  font-size: 0.95rem;
  font-weight: 820;
}

.bar-list {
  display: grid;
  gap: var(--space-2);
}

.bar-row {
  display: grid;
  gap: 0.4rem;
}

.bar-row__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.bar-row__track {
  height: 0.48rem;
  overflow: hidden;
  border-radius: 999px;
  background: var(--surface-chart-track);
}

.bar-row__track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: var(--accent-chart-fill);
  transition: width 750ms cubic-bezier(0.22, 1, 0.36, 1);
}

.bar-row__fill--changed {
  animation-duration: 6400ms;
  animation-timing-function: cubic-bezier(0.16, 1, 0.3, 1);
  will-change: filter, box-shadow, background;
}

.bar-row__fill--changed.overview-highlight-cycle-a {
  animation-name: overview-bar-fill-highlight-a;
}

.bar-row__fill--changed.overview-highlight-cycle-b {
  animation-name: overview-bar-fill-highlight-b;
}

.market-panel__cta {
  display: inline-flex;
  align-self: end;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  min-height: 2rem;
  margin-top: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  padding: 0 var(--space-3);
  color: var(--color-text);
  font-size: 0.8125rem;
  font-weight: 720;
  transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
}

.market-panel__cta:hover {
  border-color: var(--accent-ember-border);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.market-skeleton {
  display: block;
  overflow: hidden;
  position: relative;
  border-radius: var(--radius-sm);
  background: var(--background-overview-skeleton);
  background-size: 220% 100%;
  animation: overview-skeleton 1800ms ease-in-out infinite;
}

.market-kpi__icon.market-skeleton {
  border-color: var(--border-ember-muted);
  background-color: var(--accent-ember-soft);
}

.market-skeleton-stack {
  display: grid;
  align-content: center;
  min-height: 1.9rem;
}

.market-skeleton--title {
  width: min(11rem, 75%);
  height: 0.95rem;
}

.market-skeleton--label {
  width: 6.4rem;
  height: 0.65rem;
}

.market-skeleton--value {
  width: 4rem;
  height: 1rem;
}

.market-skeleton--number {
  width: 2.5rem;
  height: 0.9rem;
}

.market-skeleton--bar {
  width: 100%;
  height: 0.48rem;
  border-radius: 999px;
}

.market-skeleton--button {
  width: 100%;
  min-height: 2rem;
  border-color: var(--border-skeleton-button);
  color: transparent;
}

@keyframes overview-skeleton {
  0% {
    background-position: 140% 0;
  }

  100% {
    background-position: -80% 0;
  }
}

@keyframes overview-field-highlight-a {
  0% {
    border-color: rgb(249 115 22 / 0.62);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.055),
      inset 3px 0 0 rgb(249 115 22 / 0.48),
      0 0 0 1px rgb(249 115 22 / 0.18),
      0 0 18px rgb(249 115 22 / 0.14);
  }

  24% {
    border-color: rgb(249 115 22 / 0.48);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.04),
      inset 3px 0 0 rgb(249 115 22 / 0.34),
      0 0 0 1px rgb(249 115 22 / 0.14),
      0 0 15px rgb(249 115 22 / 0.11);
  }

  55% {
    border-color: rgb(249 115 22 / 0.34);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.024),
      inset 2px 0 0 rgb(249 115 22 / 0.22),
      0 0 0 1px rgb(249 115 22 / 0.09),
      0 0 10px rgb(249 115 22 / 0.07);
  }

  78% {
    border-color: rgb(249 115 22 / 0.2);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.012),
      inset 1px 0 0 rgb(249 115 22 / 0.12),
      0 0 0 1px rgb(249 115 22 / 0.045),
      0 0 6px rgb(249 115 22 / 0.04);
  }

  100% {
    border-color: var(--surface-metric-border);
    box-shadow: none;
  }
}

@keyframes overview-field-highlight-b {
  0% {
    border-color: rgb(249 115 22 / 0.62);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.055),
      inset 3px 0 0 rgb(249 115 22 / 0.48),
      0 0 0 1px rgb(249 115 22 / 0.18),
      0 0 18px rgb(249 115 22 / 0.14);
  }

  24% {
    border-color: rgb(249 115 22 / 0.48);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.04),
      inset 3px 0 0 rgb(249 115 22 / 0.34),
      0 0 0 1px rgb(249 115 22 / 0.14),
      0 0 15px rgb(249 115 22 / 0.11);
  }

  55% {
    border-color: rgb(249 115 22 / 0.34);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.024),
      inset 2px 0 0 rgb(249 115 22 / 0.22),
      0 0 0 1px rgb(249 115 22 / 0.09),
      0 0 10px rgb(249 115 22 / 0.07);
  }

  78% {
    border-color: rgb(249 115 22 / 0.2);
    box-shadow:
      inset 0 0 0 999px rgb(249 115 22 / 0.012),
      inset 1px 0 0 rgb(249 115 22 / 0.12),
      0 0 0 1px rgb(249 115 22 / 0.045),
      0 0 6px rgb(249 115 22 / 0.04);
  }

  100% {
    border-color: var(--surface-metric-border);
    box-shadow: none;
  }
}

@keyframes overview-bar-fill-highlight-a {
  0% {
    background: var(--accent-chart-fill-change-strong);
    filter: brightness(1.34) saturate(1.18);
    box-shadow: 0 0 14px var(--accent-chart-fill-glow-strong);
  }

  35% {
    background: var(--accent-chart-fill-change-soft);
    filter: brightness(1.2) saturate(1.1);
    box-shadow: 0 0 10px var(--accent-chart-fill-glow);
  }

  68% {
    background: var(--accent-chart-fill-return);
    filter: brightness(1.08) saturate(1.04);
    box-shadow: 0 0 6px var(--accent-chart-fill-glow-soft);
  }

  100% {
    background: var(--accent-chart-fill);
    filter: brightness(1) saturate(1);
    box-shadow: none;
  }
}

@keyframes overview-bar-fill-highlight-b {
  0% {
    background: var(--accent-chart-fill-change-strong);
    filter: brightness(1.34) saturate(1.18);
    box-shadow: 0 0 14px var(--accent-chart-fill-glow-strong);
  }

  35% {
    background: var(--accent-chart-fill-change-soft);
    filter: brightness(1.2) saturate(1.1);
    box-shadow: 0 0 10px var(--accent-chart-fill-glow);
  }

  68% {
    background: var(--accent-chart-fill-return);
    filter: brightness(1.08) saturate(1.04);
    box-shadow: 0 0 6px var(--accent-chart-fill-glow-soft);
  }

  100% {
    background: var(--accent-chart-fill);
    filter: brightness(1) saturate(1);
    box-shadow: none;
  }
}

@keyframes overview-value-highlight-a {
  0% {
    color: var(--accent-ember-text-strong);
    text-shadow: 0 0 12px rgb(249 115 22 / 0.32);
  }

  32% {
    color: var(--accent-ember-text);
    text-shadow: 0 0 9px rgb(249 115 22 / 0.22);
  }

  68% {
    color: rgb(255 237 213);
    text-shadow: 0 0 5px rgb(249 115 22 / 0.1);
  }

  100% {
    color: var(--color-text);
    text-shadow: none;
  }
}

@keyframes overview-value-highlight-b {
  0% {
    color: var(--accent-ember-text-strong);
    text-shadow: 0 0 12px rgb(249 115 22 / 0.32);
  }

  32% {
    color: var(--accent-ember-text);
    text-shadow: 0 0 9px rgb(249 115 22 / 0.22);
  }

  68% {
    color: rgb(255 237 213);
    text-shadow: 0 0 5px rgb(249 115 22 / 0.1);
  }

  100% {
    color: var(--color-text);
    text-shadow: none;
  }
}

@media (max-width: 1280px) {
  .market-kpis,
  .market-visuals {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .market-snapshot__header {
    display: grid;
  }

  .market-kpis,
  .market-visuals {
    grid-template-columns: 1fr;
  }

  .market-kpi {
    min-height: auto;
  }
}

@media (prefers-reduced-motion: reduce) {
  .market-kpi__metric--changed,
  .market-kpi__metric--changed.overview-highlight-cycle-a,
  .market-kpi__metric--changed.overview-highlight-cycle-b,
  .intelligence-mini__metric--changed,
  .intelligence-mini__metric--changed.overview-highlight-cycle-a,
  .intelligence-mini__metric--changed.overview-highlight-cycle-b,
  .overview-value-highlight,
  .overview-value-highlight.overview-highlight-cycle-a,
  .overview-value-highlight.overview-highlight-cycle-b,
  .bar-row__fill--changed,
  .bar-row__fill--changed.overview-highlight-cycle-a,
  .bar-row__fill--changed.overview-highlight-cycle-b {
    animation: none;
  }

  .market-kpi__metric--changed,
  .intelligence-mini__metric--changed {
    border-color: rgb(249 115 22 / 0.32);
    box-shadow: 0 0 0 1px rgb(249 115 22 / 0.08);
  }

  .overview-value-highlight {
    color: var(--accent-ember-text);
    text-shadow: none;
  }

  .bar-row__fill--changed {
    background: var(--accent-chart-fill-reduced);
    filter: brightness(1.08) saturate(1.04);
    box-shadow: none;
  }

  .bar-row__track span {
    transition: none;
  }

  .market-skeleton {
    animation: none;
    background-position: 0 0;
  }
}
</style>
