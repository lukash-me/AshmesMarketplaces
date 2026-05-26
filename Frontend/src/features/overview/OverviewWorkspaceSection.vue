<script setup lang="ts">
import { computed, type Component } from 'vue';
import type { RouteLocationRaw } from 'vue-router';
import { ArrowRight, BarChart3, Boxes, CreditCard, Radar, Truck } from 'lucide-vue-next';

import { MARKET_INTELLIGENCE_DEFAULT_LINK } from './overview.api';
import type { OverviewCardStatus, OverviewData, OverviewMetric } from './overview.types';

const props = defineProps<{
  data: OverviewData | null;
  loading: boolean;
}>();

interface WorkspaceCard {
  id: string;
  title: string;
  description: string;
  status: OverviewCardStatus;
  icon: Component;
  to: RouteLocationRaw;
  ctaLabel: string;
  metrics: OverviewMetric[];
}

const cards = computed<WorkspaceCard[]>(() => [
  buildObservedOrdersCard(),
  buildLogisticsCard(),
  buildExpensesCard(),
  buildMarketProductsCard(),
  buildMarketIntelligenceCard()
]);

function buildObservedOrdersCard(): WorkspaceCard {
  const resource = props.data?.observedStockDecreases;

  if (props.loading && !resource) {
    return baseCard('orders', 'Предполагаемые заказы', Boxes, '/orders', 'Открыть раздел', 'loading');
  }

  if (!resource || resource.status === 'unavailable') {
    return baseCard('orders', 'Предполагаемые заказы', Boxes, '/orders', 'Открыть раздел', 'unavailable');
  }

  const hasNotEnoughData = resource.data.warnings.some((warning) => warning.includes('not_enough'));

  return {
    ...baseCard('orders', 'Предполагаемые заказы', Boxes, '/orders', 'Открыть раздел', hasNotEnoughData || resource.data.totalCount === 0 ? 'empty' : 'ready'),
    description: 'Наблюдаемое снижение остатков WB для проверки событий рынка.',
    metrics: [
      { label: 'Изменений остатка', value: formatNumber(resource.data.totalCount) },
      { label: 'Обновлено', value: formatDateTime(resource.data.currentObservedAtUtc) }
    ]
  };
}

function buildLogisticsCard(): WorkspaceCard {
  const resource = props.data?.logisticsSummary;

  if (props.loading && !resource) {
    return baseCard('logistics', 'Логистика', Truck, '/logistics', 'Открыть раздел', 'loading');
  }

  if (!resource || resource.status === 'unavailable') {
    return baseCard('logistics', 'Логистика', Truck, '/logistics', 'Открыть раздел', 'unavailable');
  }

  const summary = resource.data;

  return {
    ...baseCard('logistics', 'Логистика', Truck, '/logistics', 'Открыть раздел', summary.productsTotal > 0 ? 'ready' : 'empty'),
    description: `Логистика найдена для ${formatNumber(summary.productsWithLogistics)} товаров из ${formatNumber(summary.productsTotal)} в текущей выборке.`,
    metrics: [
      { label: 'Товаров с логистикой', value: formatNumber(summary.productsWithLogistics) },
      { label: 'Всего в выборке', value: formatNumber(summary.productsTotal) },
      { label: 'Обновлено', value: formatDateTime(summary.latestObservedAtUtc) }
    ]
  };
}

function buildExpensesCard(): WorkspaceCard {
  const resource = props.data?.expenses;

  if (props.loading && !resource) {
    return baseCard('expenses', 'Расходы', CreditCard, '/expenses', 'Открыть раздел', 'loading');
  }

  if (!resource || resource.status === 'unavailable') {
    return baseCard('expenses', 'Расходы', CreditCard, '/expenses', 'Открыть раздел', 'unavailable');
  }

  return {
    ...baseCard('expenses', 'Расходы', CreditCard, '/expenses', 'Открыть раздел', resource.data.totalCount > 0 ? 'ready' : 'empty'),
    description: 'Сохраненные расходы для контроля операционной базы.',
    metrics: [{ label: 'Записей', value: formatNumber(resource.data.totalCount) }]
  };
}

function buildMarketProductsCard(): WorkspaceCard {
  const resource = props.data?.marketProducts;

  if (props.loading && !resource) {
    return baseCard('market-products', 'Рыночная аналитика', BarChart3, '/market/products', 'Открыть раздел', 'loading');
  }

  if (!resource || resource.status === 'unavailable') {
    return baseCard('market-products', 'Рыночная аналитика', BarChart3, '/market/products', 'Открыть раздел', 'unavailable');
  }

  return {
    ...baseCard('market-products', 'Рыночная аналитика', BarChart3, '/market/products', 'Открыть раздел', resource.data.totalCount > 0 ? 'ready' : 'empty'),
    description: 'Таблица наблюдаемых товаров, позиций, цен, отзывов и логистики.',
    metrics: [{ label: 'Товаров', value: formatNumber(resource.data.totalCount) }]
  };
}

function buildMarketIntelligenceCard(): WorkspaceCard {
  const resource = props.data?.marketIntelligence;

  if (props.loading && !resource) {
    return baseCard('intelligence', 'Маркетинговая разведка', Radar, MARKET_INTELLIGENCE_DEFAULT_LINK, 'Открыть раздел', 'loading');
  }

  if (!resource || resource.status === 'unavailable') {
    return baseCard('intelligence', 'Маркетинговая разведка', Radar, MARKET_INTELLIGENCE_DEFAULT_LINK, 'Открыть раздел', 'unavailable');
  }

  return {
    ...baseCard('intelligence', 'Маркетинговая разведка', Radar, MARKET_INTELLIGENCE_DEFAULT_LINK, 'Открыть раздел', 'ready'),
    description: 'События рынка, зоны для проверки, цены, остатки и повторы.',
    metrics: [
      { label: 'Событий', value: formatNumber(resource.data.events.length) },
      { label: 'Зон для проверки', value: formatNumber(resource.data.competitorWeaknesses.length) }
    ]
  };
}

function baseCard(
  id: string,
  title: string,
  icon: Component,
  to: RouteLocationRaw,
  ctaLabel: string,
  status: OverviewCardStatus
): WorkspaceCard {
  return {
    id,
    title,
    icon,
    to,
    ctaLabel,
    status,
    description: status === 'unavailable'
      ? 'Данные временно недоступны.'
      : status === 'loading'
        ? 'Загружаем данные.'
        : 'Данных пока нет.',
    metrics: []
  };
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
  <section class="workspace-section" aria-labelledby="overview-workspace-title">
    <div class="workspace-section__header">
      <div>
        <h2 id="overview-workspace-title">Мои разделы</h2>
        <p>Быстрый переход к рабочим областям, которые уже есть в системе.</p>
      </div>
    </div>

    <div class="workspace-grid">
      <article
        v-for="card in cards"
        :key="card.id"
        class="workspace-card app-surface"
        :class="`workspace-card--${card.status}`"
      >
        <header class="workspace-card__header">
          <span class="workspace-card__icon">
            <component :is="card.icon" :size="16" />
          </span>
          <h3>{{ card.title }}</h3>
        </header>

        <p>{{ card.description }}</p>

        <div v-if="card.metrics.length > 0" class="workspace-card__metrics">
          <div v-for="metric in card.metrics" :key="metric.label" class="workspace-card__metric">
            <span>{{ metric.label }}</span>
            <strong>{{ metric.value }}</strong>
          </div>
        </div>

        <RouterLink class="workspace-card__cta" :to="card.to">
          <span>{{ card.ctaLabel }}</span>
          <ArrowRight :size="14" />
        </RouterLink>
      </article>
    </div>
  </section>
</template>

<style scoped>
.workspace-section {
  display: grid;
  gap: var(--space-3);
}

.workspace-section__header h2,
.workspace-section__header p,
.workspace-card p {
  margin: 0;
}

.workspace-section__header h2 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  letter-spacing: 0;
}

.workspace-section__header p,
.workspace-card p,
.workspace-card__metric span {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  line-height: 1.45;
}

.workspace-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-3);
}

.workspace-card {
  display: grid;
  min-height: 12rem;
  align-content: start;
  gap: var(--space-3);
  border-color: rgb(249 115 22 / 0.14);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.026), transparent 7rem),
    var(--surface-panel);
  padding: var(--space-3);
}

.workspace-card--unavailable,
.workspace-card--empty {
  border-color: var(--color-border);
}

.workspace-card__header {
  display: grid;
  grid-template-columns: 1.9rem 1fr;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
}

.workspace-card__icon {
  display: grid;
  width: 1.9rem;
  height: 1.9rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--accent-ember-text);
}

.workspace-card__header h3 {
  min-width: 0;
  margin: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.9rem;
  font-weight: 800;
  letter-spacing: 0;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.workspace-card__metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-2);
}

.workspace-card__metric {
  display: grid;
  min-width: 0;
  gap: 0.18rem;
  border: 1px solid rgb(255 255 255 / 0.07);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-2);
}

.workspace-card__metric span,
.workspace-card__metric strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.workspace-card__metric span {
  font-size: 0.72rem;
  font-weight: 720;
  text-transform: uppercase;
}

.workspace-card__metric strong {
  color: var(--color-text);
  font-size: 0.95rem;
  font-weight: 820;
}

.workspace-card__cta {
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

.workspace-card__cta:hover {
  border-color: var(--accent-ember-border);
  background: rgb(249 115 22 / 0.1);
  color: var(--accent-ember-text-strong);
}

@media (max-width: 1100px) {
  .workspace-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .workspace-grid {
    grid-template-columns: 1fr;
  }

  .workspace-card {
    min-height: auto;
  }
}
</style>
