<script setup lang="ts">
import { computed, ref } from 'vue';

import EmptyState from '@/shared/ui/EmptyState.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import type { MarketConcentration, MarketConcentrationRanking } from './marketIntelligence.types';

const props = defineProps<{
  concentration: MarketConcentration | null;
  loading: boolean;
  topN?: number | null;
  calculatedAtUtc?: string | null;
  limitations?: string[];
}>();

type ConcentrationTableMode = 'sellers' | 'brands' | 'roots';
type ConcentrationRankingMode = 'count' | 'position' | 'reviews';
type ChartItem = {
  key: string;
  name: string;
  sharePercent: number;
  top100SharePercent?: number | null;
  feedbackSharePercent?: number | null;
  bestPosition: number | null;
};
type OpenProductsPayload = {
  kind: 'seller' | 'brand' | 'root';
  key: string;
  title: string;
};

const emit = defineEmits<{
  (event: 'open-products', payload: OpenProductsPayload): void;
}>();

const tableMode = ref<ConcentrationTableMode>('sellers');
const rankingMode = ref<ConcentrationRankingMode>('count');

const fallbackRanking = computed<MarketConcentrationRanking | null>(() => {
  const data = props.concentration;
  if (!data) {
    return null;
  }

  return {
    key: 'count',
    title: 'Топ по количеству',
    sampleSize: data.sampleSize,
    top3SellersSharePercent: data.top3SellersSharePercent,
    top5SellersSharePercent: data.top5SellersSharePercent,
    hhi: data.hhi,
    normalizedConcentrationScore: data.normalizedConcentrationScore,
    sellerLeaders: data.sellerLeaders,
    brandLeaders: data.brandLeaders,
    rootClusters: data.rootClusters,
    insight: data.insight
  };
});

const activeRanking = computed<MarketConcentrationRanking | null>(() => {
  const data = props.concentration;
  if (!data) {
    return null;
  }

  return data.rankings?.find((ranking) => ranking.key === rankingMode.value)
    ?? fallbackRanking.value;
});

const chartItems = computed<ChartItem[]>(() => {
  const ranking = activeRanking.value;
  if (!ranking) {
    return [];
  }

  if (tableMode.value === 'brands') {
    return ranking.brandLeaders.map((brand) => ({
      key: brand.name,
      name: brand.name,
      sharePercent: brand.sharePercent,
      top100SharePercent: brand.top100SharePercent,
      feedbackSharePercent: brand.feedbackSharePercent,
      bestPosition: brand.bestPosition
    }));
  }

  if (tableMode.value === 'roots') {
    return ranking.rootClusters.map((cluster) => ({
      key: cluster.wbRootId,
      name: cluster.wbRootId,
      sharePercent: cluster.sharePercent,
      top100SharePercent: cluster.top100SharePercent,
      feedbackSharePercent: cluster.feedbackSharePercent,
      bestPosition: cluster.bestPosition
    }));
  }

  return ranking.sellerLeaders.map((seller) => ({
    key: seller.name,
    name: seller.name,
    sharePercent: seller.sharePercent,
    top100SharePercent: seller.top100SharePercent,
    feedbackSharePercent: seller.feedbackSharePercent,
    bestPosition: seller.bestPosition
  }));
});

const chartTitle = computed(() => {
  const rankingTitle = activeRanking.value?.title ?? 'Топ по количеству';
  const entityTitle = tableMode.value === 'brands'
    ? 'брендов'
    : tableMode.value === 'roots'
      ? 'root-групп'
      : 'продавцов';

  return `${rankingTitle} ${entityTitle}`;
});

const chartEmptyTitle = computed(() => {
  if (tableMode.value === 'brands') {
    return 'Нет данных о брендах';
  }

  if (tableMode.value === 'roots') {
    return 'Нет повторяющихся root-групп';
  }

  return 'Нет данных о продавцах';
});

const chartEmptyDescription = computed(() => {
  if (tableMode.value === 'brands') {
    return 'В выбранной нише нет brand-данных для диаграммы.';
  }

  if (tableMode.value === 'roots') {
    return 'В выбранной нише нет повторяющихся root-групп для диаграммы.';
  }

  return 'В выбранной нише нет seller-данных для диаграммы.';
});

const isSnapshotStale = computed(() => {
  if (!props.calculatedAtUtc) {
    return false;
  }

  const calculatedAt = new Date(props.calculatedAtUtc).getTime();
  return !Number.isNaN(calculatedAt) && Date.now() - calculatedAt > 36 * 60 * 60 * 1000;
});

function formatNumber(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)
    : '—';
}

function formatPercent(value: number | null | undefined): string {
  return typeof value === 'number'
    ? `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value)}%`
    : '—';
}

function formatPosition(value: number | null | undefined): string {
  if (typeof value === 'number') {
    return `#${formatNumber(value)}`;
  }

  return typeof props.topN === 'number' && props.topN > 0
    ? `>${formatNumber(props.topN)}`
    : '—';
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function concentrationBarStyle(value: number | null | undefined): string {
  const width = typeof value === 'number' ? Math.min(100, Math.max(0, value)) : 0;
  return `width: ${width}%`;
}

function chartSharePercent(item: ChartItem): number {
  if (rankingMode.value === 'position') {
    return item.top100SharePercent ?? 0;
  }

  if (rankingMode.value === 'reviews') {
    return item.feedbackSharePercent ?? 0;
  }

  return item.sharePercent;
}

function chartShareLabel(): string {
  if (rankingMode.value === 'position') {
    return 'доля в топе';
  }

  if (rankingMode.value === 'reviews') {
    return 'доля отзывов';
  }

  return 'доля карточек';
}

function openProducts(payload: OpenProductsPayload): void {
  emit('open-products', payload);
}
</script>

<template>
  <section
    class="market-concentration app-surface app-operator-panel"
    :class="{ 'market-concentration--loading': loading }"
  >
    <div v-if="loading" class="market-concentration__loading" aria-live="polite">
      <span class="market-concentration__loader" />
      <strong>Обновляем концентрацию рынка...</strong>
    </div>

    <header class="market-concentration__header">
      <div>
        <h2>
          <span>Концентрация рынка</span>
          <HelpTooltip text="Показывает, насколько выбранная ниша занята крупными продавцами, брендами и повторяющимися root-группами." />
        </h2>
      </div>
      <div v-if="concentration" class="market-concentration__stats">
        <span>Выборка: <b>{{ formatNumber(concentration.sampleSize) }}</b></span>
      </div>
    </header>

    <p v-if="isSnapshotStale" class="market-concentration__warning">Данные ожидают обновления.</p>
    <p v-else-if="limitations?.length" class="market-concentration__warning">{{ limitations[0] }}</p>

    <EmptyState
      v-if="!concentration"
      title="Концентрация рынка пока недоступна"
      description="Обновите backend API до версии с расчетом концентрации рынка."
    />

    <EmptyState
      v-else-if="concentration.sampleSize === 0"
      title="Концентрация рынка не рассчитана"
      :description="concentration.limitations[0] ?? 'В выбранной нише нет товаров для расчета концентрации.'"
    />

    <div v-else class="market-concentration__body">
      <div class="market-concentration__grid">
        <section class="market-concentration__chart" aria-label="Диаграмма концентрации">
          <h3>{{ chartTitle }}</h3>
          <div v-if="chartItems.length" class="concentration-bars">
            <div
              v-for="item in chartItems.slice(0, 8)"
              :key="item.key"
              class="concentration-bar"
            >
              <div class="concentration-bar__label">
                <strong>{{ item.name }}</strong>
                <span>{{ chartShareLabel() }} {{ formatPercent(chartSharePercent(item)) }} · {{ formatPosition(item.bestPosition) }}</span>
              </div>
              <div class="concentration-bar__track">
                <span :style="concentrationBarStyle(chartSharePercent(item))" />
              </div>
            </div>
          </div>
          <EmptyState
            v-else
            :title="chartEmptyTitle"
            :description="chartEmptyDescription"
          />
        </section>

        <section class="market-concentration__tables">
          <div class="market-concentration__selector" role="group" aria-label="Режим ранжирования">
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': rankingMode === 'count' }"
              class="market-concentration__selector-button"
              @click="rankingMode = 'count'"
            >
              Топ по количеству
            </button>
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': rankingMode === 'position' }"
              class="market-concentration__selector-button"
              @click="rankingMode = 'position'"
            >
              Топ по позиции
            </button>
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': rankingMode === 'reviews' }"
              class="market-concentration__selector-button"
              @click="rankingMode = 'reviews'"
            >
              Топ по отзывам
            </button>
          </div>

          <div class="market-concentration__selector" role="group" aria-label="Разрез концентрации рынка">
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': tableMode === 'sellers' }"
              class="market-concentration__selector-button"
              @click="tableMode = 'sellers'"
            >
              Топ продавцов
            </button>
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': tableMode === 'brands' }"
              class="market-concentration__selector-button"
              @click="tableMode = 'brands'"
            >
              Топ брендов
            </button>
            <button
              type="button"
              :class="{ 'market-concentration__selector-button--active': tableMode === 'roots' }"
              class="market-concentration__selector-button"
              @click="tableMode = 'roots'"
            >
              Повторяющиеся root-группы
            </button>
          </div>

          <table v-if="tableMode === 'sellers'" class="market-concentration__table">
            <caption class="sr-only">Топ продавцов</caption>
            <thead>
              <tr>
                <th>Продавец</th>
                <th>Мест</th>
                <th>Доля карточек</th>
                <th>Доля в топе</th>
                <th>Отзывы</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="seller in activeRanking?.sellerLeaders ?? []"
                :key="seller.name"
                class="market-concentration__table-row--interactive"
                role="button"
                tabindex="0"
                @click="openProducts({ kind: 'seller', key: seller.name, title: `Карточки продавца ${seller.name}` })"
                @keydown.enter.prevent="openProducts({ kind: 'seller', key: seller.name, title: `Карточки продавца ${seller.name}` })"
                @keydown.space.prevent="openProducts({ kind: 'seller', key: seller.name, title: `Карточки продавца ${seller.name}` })"
              >
                <td>{{ seller.name }}</td>
                <td>{{ formatNumber(seller.slotsCount) }}</td>
                <td>{{ formatPercent(seller.sharePercent) }}</td>
                <td>{{ formatPercent(seller.top100SharePercent ?? 0) }}</td>
                <td>{{ formatNumber(seller.feedbackCount) }}</td>
                <td>{{ formatPosition(seller.bestPosition) }}</td>
              </tr>
            </tbody>
          </table>

          <table v-else-if="tableMode === 'brands'" class="market-concentration__table">
            <caption class="sr-only">Топ брендов</caption>
            <thead>
              <tr>
                <th>Бренд</th>
                <th>Мест</th>
                <th>Доля карточек</th>
                <th>Доля в топе</th>
                <th>Отзывы</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="brand in activeRanking?.brandLeaders ?? []"
                :key="brand.name"
                class="market-concentration__table-row--interactive"
                role="button"
                tabindex="0"
                @click="openProducts({ kind: 'brand', key: brand.name, title: `Карточки бренда ${brand.name}` })"
                @keydown.enter.prevent="openProducts({ kind: 'brand', key: brand.name, title: `Карточки бренда ${brand.name}` })"
                @keydown.space.prevent="openProducts({ kind: 'brand', key: brand.name, title: `Карточки бренда ${brand.name}` })"
              >
                <td>{{ brand.name }}</td>
                <td>{{ formatNumber(brand.slotsCount) }}</td>
                <td>{{ formatPercent(brand.sharePercent) }}</td>
                <td>{{ formatPercent(brand.top100SharePercent ?? 0) }}</td>
                <td>{{ formatNumber(brand.feedbackCount) }}</td>
                <td>{{ formatPosition(brand.bestPosition) }}</td>
              </tr>
            </tbody>
          </table>

          <table v-else class="market-concentration__table">
            <caption class="sr-only">Повторяющиеся root-группы</caption>
            <thead>
              <tr>
                <th>WB root</th>
                <th>Карточек</th>
                <th>Доля карточек</th>
                <th>Доля в топе</th>
                <th>Отзывы</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="cluster in activeRanking?.rootClusters ?? []"
                :key="cluster.wbRootId"
                class="market-concentration__table-row--interactive"
                role="button"
                tabindex="0"
                @click="openProducts({ kind: 'root', key: cluster.wbRootId, title: `Карточки root ${cluster.wbRootId}` })"
                @keydown.enter.prevent="openProducts({ kind: 'root', key: cluster.wbRootId, title: `Карточки root ${cluster.wbRootId}` })"
                @keydown.space.prevent="openProducts({ kind: 'root', key: cluster.wbRootId, title: `Карточки root ${cluster.wbRootId}` })"
              >
                <td>{{ cluster.wbRootId }}</td>
                <td>{{ formatNumber(cluster.productCount) }}</td>
                <td>{{ formatPercent(cluster.sharePercent) }}</td>
                <td>{{ formatPercent(cluster.top100SharePercent ?? 0) }}</td>
                <td>{{ formatNumber(cluster.feedbackCount) }}</td>
                <td>{{ formatPosition(cluster.bestPosition) }}</td>
              </tr>
            </tbody>
          </table>
        </section>
      </div>
    </div>
  </section>
</template>

<style scoped>
.market-concentration {
  position: relative;
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
  overflow: hidden;
}

.market-concentration--loading .market-concentration__body,
.market-concentration--loading .market-concentration__header {
  pointer-events: none;
  opacity: 0.28;
}

.market-concentration__loading {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: grid;
  gap: var(--space-2);
  place-items: center;
  align-content: center;
  background: color-mix(in srgb, var(--surface-panel) 78%, transparent);
  backdrop-filter: blur(1px);
  color: var(--text-primary);
  font-size: 0.92rem;
  font-weight: 800;
}

.market-concentration__loader {
  width: 1.45rem;
  height: 1.45rem;
  border: 2px solid var(--border-subtle);
  border-top-color: var(--accent-ember);
  border-radius: 999px;
  animation: concentration-spin 800ms linear infinite;
}

.market-concentration__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  align-items: flex-start;
}

.market-concentration__header h2 {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin: 0 0 var(--space-1);
  font-size: 1.1rem;
}

.market-concentration__body {
  display: grid;
  gap: var(--space-4);
}

.market-concentration__stats {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.82rem;
  font-weight: 700;
}

.market-concentration__stats span {
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  padding: 0.35rem 0.55rem;
  background: var(--surface-raised);
}

.market-concentration__warning {
  margin: 0;
  border: 1px solid var(--accent-primary-border);
  border-radius: 6px;
  background: var(--accent-primary-soft);
  color: var(--text-primary);
  padding: 0.55rem 0.75rem;
  font-size: 0.86rem;
}

.market-concentration__grid {
  display: grid;
  grid-template-columns: minmax(22rem, 0.65fr) minmax(30rem, 1fr);
  gap: var(--space-4);
  align-items: start;
}

.market-concentration__chart,
.market-concentration__tables {
  display: grid;
  gap: var(--space-3);
}

.market-concentration__chart {
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  padding: var(--space-4);
}

.market-concentration__chart h3 {
  margin: 0;
  font-size: 1rem;
}

.concentration-bars {
  display: grid;
  gap: var(--space-3);
}

.concentration-bar {
  display: grid;
  gap: var(--space-1);
}

.concentration-bar__label {
  display: flex;
  justify-content: space-between;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

.concentration-bar__label strong {
  min-width: 0;
  overflow: hidden;
  color: var(--text-primary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.concentration-bar__track {
  height: 0.7rem;
  overflow: hidden;
  border-radius: 999px;
  background: color-mix(in srgb, var(--border-subtle) 70%, transparent);
}

.concentration-bar__track span {
  display: block;
  height: 100%;
  min-width: 0.2rem;
  border-radius: inherit;
  background: var(--accent-ember);
}

.market-concentration__selector {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.market-concentration__selector-button {
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  background: var(--surface-panel);
  color: var(--text-primary);
  cursor: pointer;
  font: inherit;
  font-size: 0.84rem;
  font-weight: 800;
  padding: 0.5rem 0.75rem;
}

.market-concentration__selector-button--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.market-concentration__table {
  width: 100%;
  border-collapse: collapse;
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  font-size: 0.84rem;
}

.market-concentration__table caption {
  padding: 0 0 var(--space-2);
  color: var(--text-primary);
  font-weight: 900;
  text-align: left;
}

.market-concentration__table th,
.market-concentration__table td {
  border-bottom: 1px solid var(--border-subtle);
  padding: 0.68rem 0.72rem;
  text-align: left;
}

.market-concentration__table th {
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.market-concentration__table td {
  color: var(--text-primary);
  font-weight: 700;
}

.market-concentration__table tr:last-child td {
  border-bottom: 0;
}

.market-concentration__table-row--interactive {
  cursor: pointer;
}

.market-concentration__table-row--interactive:hover td,
.market-concentration__table-row--interactive:focus-visible td {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.market-concentration__table-row--interactive:focus-visible {
  outline: 2px solid var(--accent-primary-border);
  outline-offset: -2px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
}

@keyframes concentration-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 960px) {
  .market-concentration__header,
  .market-concentration__grid {
    display: grid;
    grid-template-columns: 1fr;
  }

  .market-concentration__stats {
    justify-content: flex-start;
  }
}
</style>
