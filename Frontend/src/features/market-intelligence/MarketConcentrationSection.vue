<script setup lang="ts">
import { computed } from 'vue';

import EmptyState from '@/shared/ui/EmptyState.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import type { MarketConcentration } from './marketIntelligence.types';

const props = defineProps<{
  concentration: MarketConcentration | null;
  loading: boolean;
}>();

const concentrationMetrics = computed(() => {
  const data = props.concentration;
  if (!data) {
    return [];
  }

  return [
    { label: 'Продавцов', value: formatNumber(data.uniqueSellersCount), detail: 'уникальных в topN' },
    { label: 'Брендов', value: formatNumber(data.uniqueBrandsCount), detail: 'уникальных в topN' },
    { label: 'Доля top-5', value: formatPercent(data.top5SellersSharePercent), detail: 'пять крупнейших продавцов' },
    { label: 'Индекс', value: formatPercent(data.normalizedConcentrationScore), detail: '0% фрагментирован, 100% занят лидерами' }
  ];
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

function formatConcentrationIndex(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(value)
    : '—';
}

function concentrationBarStyle(value: number | null | undefined): string {
  const width = typeof value === 'number' ? Math.min(100, Math.max(0, value)) : 0;
  return `width: ${width}%`;
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
          <HelpTooltip text="Показывает, насколько topN выбранной ниши занят крупными продавцами, брендами и повторяющимися root-группами." />
        </h2>
        <p>
          {{
            concentration?.insight
              ?? 'Показываем, фрагментирована ли ниша или значительная часть топа занята несколькими игроками.'
          }}
        </p>
      </div>
      <div v-if="concentration" class="market-concentration__stats">
        <span>Выборка: <b>{{ formatNumber(concentration.sampleSize) }}</b></span>
        <span>HHI: <b>{{ formatConcentrationIndex(concentration.hhi) }}</b></span>
      </div>
    </header>

    <EmptyState
      v-if="!concentration"
      title="Концентрация рынка пока недоступна"
      description="Обновите backend API до версии с расчетом концентрации рынка."
    />

    <EmptyState
      v-else-if="concentration.sampleSize === 0"
      title="Концентрация рынка не рассчитана"
      :description="concentration.limitations[0] ?? 'В выбранном topN нет товаров для расчета концентрации.'"
    />

    <div v-else class="market-concentration__body">
      <div v-if="concentration.limitations.length" class="market-concentration__limitations">
        <p v-for="limitation in concentration.limitations" :key="limitation">{{ limitation }}</p>
      </div>

      <div class="market-concentration__metrics">
        <article v-for="metric in concentrationMetrics" :key="metric.label" class="market-concentration__metric">
          <span>{{ metric.label }}</span>
          <strong>{{ metric.value }}</strong>
          <p>{{ metric.detail }}</p>
        </article>
      </div>

      <div class="market-concentration__grid">
        <section class="market-concentration__chart" aria-label="Доля топ продавцов">
          <h3>Топ продавцов</h3>
          <div v-if="concentration.sellerLeaders.length" class="concentration-bars">
            <div
              v-for="seller in concentration.sellerLeaders.slice(0, 8)"
              :key="seller.name"
              class="concentration-bar"
            >
              <div class="concentration-bar__label">
                <strong>{{ seller.name }}</strong>
                <span>{{ formatPercent(seller.sharePercent) }} · #{{ seller.bestPosition }}</span>
              </div>
              <div class="concentration-bar__track">
                <span :style="concentrationBarStyle(seller.sharePercent)" />
              </div>
            </div>
          </div>
          <EmptyState
            v-else
            title="Нет данных о продавцах"
            description="В выбранной выдаче нет seller-данных для диаграммы."
          />
        </section>

        <section class="market-concentration__tables">
          <table class="market-concentration__table">
            <caption>Топ продавцов</caption>
            <thead>
              <tr>
                <th>Продавец</th>
                <th>Мест</th>
                <th>Доля</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="seller in concentration.sellerLeaders" :key="seller.name">
                <td>{{ seller.name }}</td>
                <td>{{ formatNumber(seller.slotsCount) }}</td>
                <td>{{ formatPercent(seller.sharePercent) }}</td>
                <td>#{{ seller.bestPosition }}</td>
              </tr>
            </tbody>
          </table>

          <table class="market-concentration__table">
            <caption>Топ брендов</caption>
            <thead>
              <tr>
                <th>Бренд</th>
                <th>Мест</th>
                <th>Доля</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="brand in concentration.brandLeaders" :key="brand.name">
                <td>{{ brand.name }}</td>
                <td>{{ formatNumber(brand.slotsCount) }}</td>
                <td>{{ formatPercent(brand.sharePercent) }}</td>
                <td>#{{ brand.bestPosition }}</td>
              </tr>
            </tbody>
          </table>

          <table class="market-concentration__table">
            <caption>Повторяющиеся root-группы</caption>
            <thead>
              <tr>
                <th>WB root</th>
                <th>Карточек</th>
                <th>Доля</th>
                <th>Лучшая позиция</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="cluster in concentration.rootClusters" :key="cluster.wbRootId">
                <td>{{ cluster.wbRootId }}</td>
                <td>{{ formatNumber(cluster.productCount) }}</td>
                <td>{{ formatPercent(cluster.sharePercent) }}</td>
                <td>#{{ cluster.bestPosition }}</td>
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

.market-concentration__header p {
  margin: 0;
  color: var(--text-muted);
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

.market-concentration__body {
  display: grid;
  gap: var(--space-4);
}

.market-concentration__limitations {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--accent-warning-border);
  border-radius: 8px;
  background: var(--accent-warning-bg);
  padding: var(--space-3);
  color: var(--text-primary);
}

.market-concentration__limitations p {
  margin: 0;
}

.market-concentration__metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
}

.market-concentration__metric {
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  padding: var(--space-3);
}

.market-concentration__metric span {
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.market-concentration__metric strong {
  display: block;
  margin-top: var(--space-1);
  color: var(--text-primary);
  font-size: 1.25rem;
}

.market-concentration__metric p {
  margin: var(--space-1) 0 0;
  color: var(--text-muted);
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

  .market-concentration__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .market-concentration__metrics {
    grid-template-columns: 1fr;
  }
}
</style>
