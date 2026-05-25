<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import PageHeader from '@/widgets/PageHeader.vue';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import MarketProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import type { ParserProductListItem, ParserProductPosition } from '@/features/parser-products/parserProducts.types';

import { getPublicMarketIntelligence } from './marketIntelligence.api';
import type {
  CompetitorWeakness,
  ConcentrationLeader,
  HighPriceVisibleProduct,
  HighRankLowStockProduct,
  MarketEvent,
  PressureSummary,
  PublicMarketIntelligence,
  PublicMarketIntelligenceParams,
  RootCluster
} from './marketIntelligence.types';

type SectionKey = 'events' | 'checks' | 'prices' | 'stock' | 'repeats';

type EventGroup = {
  key: string;
  title: string;
  description: string;
  events: MarketEvent[];
};

type DeltaView = {
  label: string;
  tone: 'positive' | 'negative' | 'neutral';
};

type ProductLike = {
  wbProductId: string;
  wbRootId?: string | null;
  productRowId: string | null;
  thumbnailUrl: string | null;
  productName: string | null;
  brandName: string | null;
  sellerName: string | null;
  position?: number;
  currentPrice?: number | null;
  stock?: { status: string; value: number | null };
  afterValue?: string | null;
};

const defaultRegionDest = '12354108';
const defaultSort = 'popular';
const collapsedEventCount = 4;
const topNOptions = [50, 100, 300, 1000];

const demoContexts = [
  {
    label: 'Светильники бра',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Светильники бра',
    query: 'Светильники бра'
  },
  {
    label: 'Коврики для ванной',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Коврики для ванной',
    query: 'Коврики для ванной'
  },
  {
    label: 'Органайзеры для хранения вещей',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Органайзеры для хранения вещей',
    query: 'Органайзеры для хранения вещей'
  }
] as const;

const sections: Array<{ key: SectionKey; label: string }> = [
  { key: 'events', label: 'События' },
  { key: 'checks', label: 'Зоны для проверки' },
  { key: 'prices', label: 'Скидки и цены' },
  { key: 'stock', label: 'Остатки' },
  { key: 'repeats', label: 'Повторы' }
];

const route = useRoute();
const router = useRouter();

const selectedSubcategory = ref(readInitialSubcategory());
const selectedTopN = ref(readInitialTopN());
const activeSection = ref<SectionKey>('events');
const expandedEventGroups = ref<string[]>([]);
const intelligence = ref<PublicMarketIntelligence | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const selectedProduct = ref<ParserProductListItem | null>(null);

const selectedContext = computed(
  () => demoContexts.find((context) => context.sourceSubcategory === selectedSubcategory.value) ?? demoContexts[0]
);

const requestParams = computed<PublicMarketIntelligenceParams>(() => ({
  sourceCategory: selectedContext.value.sourceCategory,
  sourceSubcategory: selectedContext.value.sourceSubcategory,
  query: selectedContext.value.query,
  sourceRegionDest: readStringQuery('sourceRegionDest') ?? defaultRegionDest,
  sort: readStringQuery('sort') ?? defaultSort,
  topN: selectedTopN.value,
  latestRankRunId: readStringQuery('latestRankRunId'),
  baselineRankRunId: readStringQuery('baselineRankRunId'),
  latestProductRunId: readStringQuery('latestProductRunId'),
  baselineProductRunId: readStringQuery('baselineProductRunId')
}));

const topLabel = computed(() => `Топ-${intelligence.value?.context.topN ?? selectedTopN.value}`);

const promoSummaries = computed(() =>
  (intelligence.value?.promoPressure.summaries ?? []).filter((summary) => !isWalletSummary(summary))
);

const priceSummaries = computed(() =>
  (intelligence.value?.pricePressure.summaries ?? []).filter((summary) => !isWalletSummary(summary))
);

const combinedLimitations = computed(() => {
  const values = [
    ...(intelligence.value?.limitations ?? []),
    ...(intelligence.value?.observationWindow.limitations ?? []),
    ...(intelligence.value?.promoPressure.limitations ?? []),
    ...(intelligence.value?.pricePressure.limitations ?? []),
    ...(intelligence.value?.stockPressure.limitations ?? []),
    ...(intelligence.value?.concentration.limitations ?? []),
    'Если карточка не появилась в следующем обновлении, это не считается нулевым остатком без отдельной проверки доступности.'
  ];

  return [...new Set(values.map(sanitizeText).filter(Boolean))];
});

const eventGroups = computed<EventGroup[]>(() => {
  const groups = new Map<string, MarketEvent[]>();

  for (const event of intelligence.value?.events ?? []) {
    const key = event.type || event.group || 'other';
    groups.set(key, [...(groups.get(key) ?? []), event]);
  }

  return [...groups.entries()]
    .map(([key, events]) => ({
      key,
      ...eventGroupCopy(key),
      events
    }))
    .sort((left, right) => eventGroupOrder(left.key) - eventGroupOrder(right.key));
});

const summaryCards = computed(() => {
  const data = intelligence.value;

  if (!data) {
    return [];
  }

  const medianPrice = priceSummaries.value.find((summary) => summary.type === 'median_current_price');
  const topSeller = data.concentration.sellerLeaders[0];

  return [
    {
      label: 'Количество событий',
      value: formatNumber(data.events.length),
      detail: data.observationWindow.isComparable ? 'изменения в выбранном топе' : 'нужны данные для сравнения'
    },
    {
      label: 'Медианная цена',
      value: medianPrice ? formatSummaryValue(medianPrice.currentValue, medianPrice.unit) : '—',
      detail: medianPrice?.delta != null
        ? `изменение ${formatSignedNumber(medianPrice.delta, medianPrice.unit)}`
        : 'ориентир по нише'
    },
    {
      label: 'Низкий остаток',
      value: formatNumber(data.stockPressure.exactLowStockCount),
      detail: 'точные низкие остатки'
    },
    {
      label: 'Концентрация',
      value: topSeller ? `${formatNumber(topSeller.sharePercent)}%` : '—',
      detail: topSeller ? topSeller.name : 'лидер не выделен'
    }
  ];
});

onMounted(() => {
  void refresh();
});

async function refresh(): Promise<void> {
  loading.value = true;
  error.value = null;

  try {
    intelligence.value = await getPublicMarketIntelligence(requestParams.value);
    expandedEventGroups.value = [];
  } catch (requestError) {
    intelligence.value = null;
    error.value = sanitizeText(getProblemMessage(requestError, 'Не удалось загрузить данные по нише.'));
  } finally {
    loading.value = false;
  }
}

async function applySelection(): Promise<void> {
  await router.replace({
    query: {
      sourceCategory: selectedContext.value.sourceCategory,
      sourceSubcategory: selectedContext.value.sourceSubcategory,
      query: selectedContext.value.query,
      sourceRegionDest: defaultRegionDest,
      sort: defaultSort,
      topN: String(selectedTopN.value)
    }
  });

  await refresh();
}

function toggleEventGroup(key: string): void {
  expandedEventGroups.value = isEventGroupExpanded(key)
    ? expandedEventGroups.value.filter((item) => item !== key)
    : [...expandedEventGroups.value, key];
}

function isEventGroupExpanded(key: string): boolean {
  return expandedEventGroups.value.includes(key);
}

function visibleGroupEvents(group: EventGroup): MarketEvent[] {
  return isEventGroupExpanded(group.key)
    ? group.events
    : group.events.slice(0, visibleCollapsedCount(group));
}

function hasMoreEvents(group: EventGroup): boolean {
  return group.events.length > visibleCollapsedCount(group);
}

function visibleCollapsedCount(group: EventGroup): number {
  return collapsedEventCount;
}

function openProduct(item: ProductLike): void {
  if (!item.productRowId) {
    return;
  }

  selectedProduct.value = toParserProductListItem(item);
}

function closeProduct(): void {
  selectedProduct.value = null;
}

function toParserProductListItem(item: ProductLike): ParserProductListItem {
  const current = intelligence.value;
  const position = positionFromItem(item);

  return {
    id: item.productRowId!,
    parserRunId: current?.observationWindow.latestProductRunId ?? '',
    parsedAtUtc: current?.observationWindow.latestObservedAtUtc ?? new Date().toISOString(),
    wbProductId: item.wbProductId,
    wbRootId: item.wbRootId ?? null,
    name: productTitle(item),
    brandName: item.brandName,
    sellerName: item.sellerName,
    priceRegular: null,
    priceDiscounted: item.currentPrice ?? null,
    priceWbWallet: null,
    discountPercent: null,
    totalQuantity: stockValueFromItem(item),
    ratingRounded: null,
    reviewRating: null,
    feedbackCount: null,
    sourceCategory: current?.context.sourceCategory ?? selectedContext.value.sourceCategory,
    sourceSubcategory: current?.context.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
    sourceQuery: current?.context.query ?? selectedContext.value.query,
    thumbnailUrl: item.thumbnailUrl,
    rank: position.absolutePosition === null
      ? null
      : {
          absolutePosition: position.absolutePosition,
          page: Math.max(1, Math.ceil(position.absolutePosition / 100)),
          positionOnPage: ((position.absolutePosition - 1) % 100) + 1,
          query: position.query ?? selectedContext.value.query,
          sourceCategory: position.sourceCategory,
          sourceSubcategory: position.sourceSubcategory,
          sourceRegionDest: current?.context.sourceRegionDest ?? defaultRegionDest,
          sort: current?.context.sort ?? defaultSort,
          observedAtUtc: position.observedAtUtc ?? current?.observationWindow.latestObservedAtUtc ?? new Date().toISOString(),
          parserRunId: current?.observationWindow.latestRankRunId ?? '',
          rankContextId: '',
          contextsCount: 1
    },
    position
  };
}

function positionFromItem(item: ProductLike): ParserProductPosition {
  const current = intelligence.value;
  const rawPosition = item.position ?? parseNumber(item.afterValue);
  const absolutePosition = Number.isFinite(rawPosition) ? Number(rawPosition) : null;

  return {
    state: absolutePosition === null ? 'unknown' : 'observed',
    absolutePosition,
    observedRangeLimit: current?.context.topN ?? selectedTopN.value,
    query: current?.context.query ?? selectedContext.value.query,
    sourceCategory: current?.context.sourceCategory ?? selectedContext.value.sourceCategory,
    sourceSubcategory: current?.context.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
    observedAtUtc: current?.observationWindow.latestObservedAtUtc ?? null
  };
}

function stockValueFromItem(item: ProductLike): number | null {
  if (item.stock && item.stock.status !== 'unknown') {
    return item.stock.value;
  }

  return null;
}

function readInitialSubcategory(): string {
  const value = readStringQuery('sourceSubcategory');

  return demoContexts.some((context) => context.sourceSubcategory === value)
    ? value!
    : demoContexts[0].sourceSubcategory;
}

function readInitialTopN(): number {
  const raw = readStringQuery('topN');
  const parsed = raw ? Number(raw) : 100;

  return topNOptions.includes(parsed) ? parsed : 100;
}

function readStringQuery(key: string): string | undefined {
  const value = route.query[key];

  if (Array.isArray(value)) {
    return value[0] ?? undefined;
  }

  return typeof value === 'string' && value.trim() ? value : undefined;
}

function eventGroupCopy(type: string): { title: string; description: string } {
  const top = topLabel.value;
  const copy: Record<string, { title: string; description: string }> = {
    entered_checked_range: {
      title: 'Карточки вошли в топ',
      description: `Появились в выбранном ${top} по этой нише.`
    },
    left_checked_range: {
      title: 'Карточки вышли из топа',
      description: `Были в выбранном ${top}, но сейчас не попали в него.`
    },
    sharp_position_move: {
      title: 'Изменение позиций',
      description: 'Карточки с сильным изменением видимости за период сравнения.'
    },
    visible_price_change: {
      title: 'Цена изменилась',
      description: 'У этих карточек изменилась видимая текущая цена.'
    },
    visible_discount_change: {
      title: 'Скидка изменилась',
      description: 'Изменился факт видимого снижения цены относительно регулярной цены.'
    }
  };

  return copy[type] ?? {
    title: 'Другие изменения',
    description: 'Дополнительные изменения, найденные в выбранной нише.'
  };
}

function eventGroupOrder(type: string): number {
  const order: Record<string, number> = {
    sharp_position_move: 0,
    entered_checked_range: 1,
    left_checked_range: 2,
    visible_price_change: 3,
    visible_discount_change: 4
  };

  return order[type] ?? 10;
}

function eventCurrentLabel(event: MarketEvent): string {
  if (event.type === 'left_checked_range') {
    return 'вне топа';
  }

  if (event.type === 'entered_checked_range' || event.type === 'sharp_position_move') {
    return positionText(event.afterValue);
  }

  return sanitizeText(event.afterValue) || '—';
}

function eventPreviousLabel(event: MarketEvent): string {
  if (event.type === 'entered_checked_range') {
    return 'раньше вне топа';
  }

  if (event.type === 'left_checked_range' || event.type === 'sharp_position_move') {
    return event.beforeValue ? `было ${shortPositionText(event.beforeValue)}` : '';
  }

  return event.beforeValue ? `было ${sanitizeText(event.beforeValue)}` : '';
}

function shortPositionText(value: string | number | null | undefined): string {
  const position = parseNumber(value);
  return position === null ? '—' : `#${formatNumber(position)}`;
}

function eventDelta(event: MarketEvent): DeltaView | null {
  const before = parseNumber(event.beforeValue);
  const after = parseNumber(event.afterValue);

  if (event.type === 'sharp_position_move' && before !== null && after !== null) {
    return deltaView(before - after, 'мест');
  }

  if (event.type === 'visible_price_change' && before !== null && after !== null) {
    return deltaView(after - before, '₽');
  }

  if (event.type === 'visible_discount_change') {
    if (event.beforeValue === event.afterValue) {
      return deltaView(0, '');
    }

    return {
      label: event.afterValue === 'есть' ? '+ снижение цены' : '- снижение цены',
      tone: event.afterValue === 'есть' ? 'positive' : 'negative'
    };
  }

  return null;
}

function weaknessTitle(weakness: CompetitorWeakness): string {
  const titles: Record<string, string> = {
    weak_trust_strong_visibility: 'Видимость сильная, доверие слабее ниши',
    high_position_low_reviews: 'Отзывов меньше медианы',
    high_position_low_rating: 'Рейтинг ниже медианы',
    high_position_low_stock: 'Низкий остаток',
    high_price_vs_median: 'Цена выше медианы'
  };

  return titles[weakness.type] ?? sanitizeText(weakness.title);
}

function weaknessDelta(weakness: CompetitorWeakness): DeltaView | null {
  const metric = parseNumber(weakness.metricValue);
  const reference = parseNumber(weakness.referenceValue);

  if (metric === null || reference === null || reference === 0) {
    return null;
  }

  return deltaView(((metric - reference) / reference) * 100, '% от медианы');
}

function weaknessMetricValue(weakness: CompetitorWeakness): string {
  const metric = parseNumber(weakness.metricValue);

  if (metric === null) {
    return weakness.metricValue ?? '—';
  }

  if (weakness.type === 'high_price_vs_median') {
    return formatMoney(metric);
  }

  if (weakness.type === 'high_position_low_reviews' || weakness.type === 'weak_trust_strong_visibility') {
    return `${formatNumber(metric)} отзывов`;
  }

  if (weakness.type === 'high_position_low_stock') {
    return `${formatNumber(metric)} ${stockWord(metric)} осталось`;
  }

  if (weakness.type === 'high_position_low_rating') {
    return `${formatNumber(metric)} рейтинг`;
  }

  return `${formatNumber(metric)} значение`;
}

function weaknessReferenceValue(weakness: CompetitorWeakness): string {
  const reference = parseNumber(weakness.referenceValue);

  if (reference === null) {
    return weakness.referenceValue ? `медиана: ${weakness.referenceValue}` : 'медиана: —';
  }

  if (weakness.type === 'high_price_vs_median') {
    return `медиана цены: ${formatMoney(reference)}`;
  }

  if (weakness.type === 'high_position_low_reviews' || weakness.type === 'weak_trust_strong_visibility') {
    return `медиана отзывов: ${formatNumber(reference)}`;
  }

  if (weakness.type === 'high_position_low_stock') {
    return `медиана остатка: ${formatNumber(reference)} ${stockWord(reference)}`;
  }

  if (weakness.type === 'high_position_low_rating') {
    return `медиана рейтинга: ${formatNumber(reference)}`;
  }

  return `медиана: ${formatNumber(reference)}`;
}

function refreshButtonLabel(): string {
  return loading.value ? 'Обновляем' : 'Обновить';
}

function highPriceDelta(product: HighPriceVisibleProduct): DeltaView | null {
  if (product.currentPrice === null || product.referencePrice === null || product.referencePrice === 0) {
    return null;
  }

  return deltaView(((product.currentPrice - product.referencePrice) / product.referencePrice) * 100, '% от медианы');
}

function positionText(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return 'Позиция —';
  }

  return `Позиция #${value}`;
}

function productTitle(item: { productName: string | null; wbProductId: string }): string {
  return item.productName?.trim() || `Карточка WB ${item.wbProductId}`;
}

function brandSeller(item: { brandName: string | null; sellerName: string | null }): string {
  return [item.brandName, item.sellerName].filter(Boolean).join(' · ') || 'Бренд и продавец не указаны';
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

function stockWord(value: number): string {
  const rounded = Math.abs(Math.trunc(value));
  const lastTwo = rounded % 100;
  const last = rounded % 10;

  if (lastTwo >= 11 && lastTwo <= 14) {
    return 'товаров';
  }

  if (last === 1) {
    return 'товар';
  }

  if (last >= 2 && last <= 4) {
    return 'товара';
  }

  return 'товаров';
}

function formatMoney(value: number | null): string {
  return value === null
    ? '—'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)} ₽`;
}

function formatSummaryValue(value: number | null, unit: string): string {
  if (value === null) {
    return '—';
  }

  const safeUnit = normalizeUnit(unit);
  return `${formatNumber(value)}${safeUnit ? ` ${safeUnit}` : ''}`;
}

function formatSignedNumber(value: number | null, unit: string): string {
  if (value === null) {
    return '—';
  }

  const safeUnit = normalizeUnit(unit);
  const sign = value > 0 ? '+' : '';
  return `${sign}${formatNumber(value)}${safeUnit ? ` ${safeUnit}` : ''}`;
}

function deltaView(value: number, unit: string): DeltaView {
  const rounded = Math.abs(value) >= 10 ? Math.round(value) : Math.round(value * 10) / 10;
  const separator = unit.startsWith('%') ? '' : ' ';
  const label = `${rounded > 0 ? '+' : ''}${formatNumber(rounded)}${unit ? `${separator}${unit}` : ''}`;

  return {
    label,
    tone: rounded > 0 ? 'positive' : rounded < 0 ? 'negative' : 'neutral'
  };
}

function parseNumber(value: string | null | undefined): number | null {
  if (!value) {
    return null;
  }

  const normalized = value
    .replace(/\s/g, '')
    .replace(',', '.')
    .match(/-?\d+(\.\d+)?/);

  if (!normalized) {
    return null;
  }

  const number = Number(normalized[0]);
  return Number.isFinite(number) ? number : null;
}

function normalizeUnit(unit: string): string {
  const safeUnit = sanitizeText(unit).toLowerCase();

  if (safeUnit.includes('руб')) {
    return '₽';
  }

  if (safeUnit.includes('шт')) {
    return 'шт.';
  }

  return sanitizeText(unit);
}

function displayPressureTitle(summary: PressureSummary): string {
  const titles: Record<string, string> = {
    visible_discount_share: 'Карточки со снижением цены',
    median_current_price: 'Медианная текущая цена',
    median_regular_price: 'Медианная регулярная цена',
    leader_price_drops: 'Снижения цены среди заметных карточек'
  };

  return titles[summary.type] ?? sanitizeText(summary.title);
}

function displayPressureDescription(summary: PressureSummary): string {
  const descriptions: Record<string, string> = {
    visible_discount_share: 'Доля карточек, у которых текущая цена ниже регулярной.',
    median_current_price: 'Ориентир по текущим видимым ценам в выбранном топе.',
    median_regular_price: 'Ориентир по регулярным ценам, где они доступны.',
    leader_price_drops: 'Карточки, у которых цена стала ниже за период сравнения.'
  };

  return descriptions[summary.type] ?? sanitizeText(summary.description);
}

function isWalletSummary(summary: PressureSummary): boolean {
  const text = `${summary.type} ${summary.title} ${summary.description}`.toLowerCase();
  return text.includes('wallet') || text.includes('wb-') || text.includes('wb ') || text.includes('кошел');
}

function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    exact: 'точное значение',
    capped: 'с верхней границей',
    unknown: 'нет данных'
  };

  return labels[status] ?? 'нет данных';
}

function severityTone(severity: string): 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'ember' {
  return severity === 'low' ? 'neutral' : 'ember';
}

function concentrationWidth(value: number): string {
  if (!Number.isFinite(value) || value <= 0) {
    return '0%';
  }

  return `${Math.min(100, Math.max(1, value))}%`;
}

function clusterSummary(cluster: RootCluster): string {
  return `${cluster.productCount} карточек · лучшая позиция ${positionText(cluster.bestPosition)}`;
}

function sanitizeText(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  return value
    .replace(/публичн[а-я\s-]*наблюдени[а-я]*/gi, 'текущих данных')
    .replace(/сопоставим[а-я\s-]*наблюдени[а-я]*/gi, 'данные для сравнения')
    .replace(/наблюдени[а-я]*/gi, 'данные')
    .replace(/сопоставлени[а-я]*/gi, 'сравнение')
    .replace(/сопоставим[а-я]*/gi, 'сравнимые')
    .replace(/проверенн[а-я\s-]*диапазон[а-я]*/gi, 'выбранном топе')
    .replace(/промо-признак[а-я-]*/gi, 'скидки и цены')
    .replace(/parser|parsed|parsing|staged|staging|root-scoped|payload|attribution/gi, '')
    .replace(/парсер|парсинг|спаршенный/gi, '')
    .replace(/campaign|рекламная кампания|бюджет|ставка|CPC|CTR|DRR/gi, '')
    .replace(/прибыль|маржа|спрос|рост спроса|гарантия|hot product|рекомендаци[а-я]*/gi, '')
    .replace(/\s{2,}/g, ' ')
    .trim();
}
</script>

<template>
  <div class="market-intelligence">
    <PageHeader
      title="Маркетинговая разведка"
      subtitle="Следите за изменениями в нише: видимость, цены, остатки и зоны для проверки."
    />

    <section class="mi-controls app-surface">
      <div class="mi-controls__fields">
        <label class="mi-field">
          <span>Ниша</span>
          <select v-model="selectedSubcategory" @change="applySelection">
            <option v-for="context in demoContexts" :key="context.sourceSubcategory" :value="context.sourceSubcategory">
              {{ context.label }}
            </option>
          </select>
        </label>

        <label class="mi-field mi-field--short">
          <span>{{ topLabel }}</span>
          <select v-model.number="selectedTopN" @change="applySelection">
            <option v-for="option in topNOptions" :key="option" :value="option">Топ-{{ option }}</option>
          </select>
          <small>Количество позиций выдачи, которые сравниваются в этой нише.</small>
        </label>

        <Button class="mi-refresh" variant="primary" :loading="loading" @click="refresh">{{ refreshButtonLabel() }}</Button>
      </div>

      <div v-if="intelligence" class="mi-sync">
        <span>Последняя синхронизация WB</span>
        <strong>{{ formatDateTime(intelligence.observationWindow.latestObservedAtUtc) }}</strong>
      </div>
    </section>

    <LoadingState v-if="loading && !intelligence" label="Загружаем данные по нише..." />

    <EmptyState v-else-if="error" title="Данные не загружены" :description="error" />

    <template v-else-if="intelligence">
      <section class="mi-summary-grid">
        <article v-for="card in summaryCards" :key="card.label" class="mi-summary app-surface">
          <span>{{ card.label }}</span>
          <strong>{{ card.value }}</strong>
          <p>{{ card.detail }}</p>
        </article>
      </section>

      <nav class="mi-tabs app-surface" aria-label="Разделы маркетинговой разведки">
        <div class="mi-tabs__label">Выберите раздел</div>
        <button
          v-for="section in sections"
          :key="section.key"
          type="button"
          :class="{ 'mi-tabs__item--active': activeSection === section.key }"
          class="mi-tabs__item"
          @click="activeSection = section.key"
        >
          {{ section.label }}
        </button>
      </nav>

      <section v-if="activeSection === 'events'" class="mi-section app-surface">
        <header class="mi-section__header">
          <div>
            <h2>Изменения в выбранном топе</h2>
            <p>Группы карточек, где изменились позиции, цена или скидка.</p>
          </div>
        </header>

        <EmptyState
          v-if="eventGroups.length === 0"
          title="Пока нет событий для выбранной ниши"
          description="Когда появятся данные для сравнения, здесь будут изменения по карточкам."
        />

        <div v-else class="event-groups">
          <article v-for="group in eventGroups" :key="group.key" class="event-group">
            <header class="event-group__header">
              <div>
                <h3>{{ group.title }}</h3>
                <p>{{ group.description }}</p>
              </div>
              <Badge tone="info">Карточек: {{ formatNumber(group.events.length) }}</Badge>
            </header>

            <div v-if="visibleGroupEvents(group).length" class="product-card-grid">
              <article
                v-for="event in visibleGroupEvents(group)"
                :key="`${event.type}-${event.wbProductId}-${event.beforeValue}-${event.afterValue}`"
                class="product-card"
                :class="{ 'product-card--clickable': event.productRowId }"
                :tabindex="event.productRowId ? 0 : undefined"
                :role="event.productRowId ? 'button' : undefined"
                @click="openProduct(event)"
                @keydown.enter.prevent="openProduct(event)"
                @keydown.space.prevent="openProduct(event)"
              >
                <div class="product-card__image">
                  <MarketProductImage :src="event.thumbnailUrl" :alt="productTitle(event)" />
                </div>
                <div class="product-card__body">
                  <div class="product-card__top">
                    <Badge :tone="severityTone(event.severity)">{{ group.title }}</Badge>
                  </div>
                  <span class="product-card__sku">WB {{ event.wbProductId }}</span>
                  <h4>{{ productTitle(event) }}</h4>
                  <p>{{ brandSeller(event) }}</p>
                  <div class="value-line product-card__wide">
                    <strong>{{ eventCurrentLabel(event) }}</strong>
                    <div v-if="eventPreviousLabel(event) || eventDelta(event)" class="value-line__meta">
                      <span v-if="eventPreviousLabel(event)">{{ eventPreviousLabel(event) }}</span>
                      <span
                        v-if="eventDelta(event)"
                        class="delta-chip"
                        :class="`delta-chip--${eventDelta(event)?.tone}`"
                      >
                        {{ eventDelta(event)?.label }}
                      </span>
                    </div>
                  </div>
                  <p v-if="event.limitations.length" class="product-card__note">
                    {{ sanitizeText(event.limitations[0]) }}
                  </p>
                  <Button
                    v-if="event.productRowId"
                    class="product-card__action"
                    variant="ghost"
                    @click.stop="openProduct(event)"
                  >
                    Подробнее
                  </Button>
                  <span v-else class="product-card__disabled">Описание недоступно для этой карточки</span>
                </div>
              </article>
            </div>

            <Button
              v-if="hasMoreEvents(group)"
              class="event-group__toggle"
              variant="ghost"
              @click="toggleEventGroup(group.key)"
            >
              {{ isEventGroupExpanded(group.key) ? 'Свернуть' : 'Показать больше карточек' }}
            </Button>
          </article>
        </div>
      </section>

      <section v-else-if="activeSection === 'checks'" class="mi-section app-surface">
        <header class="mi-section__header">
          <div>
            <h2>Зоны для проверки</h2>
            <p>Карточки, у которых высокая видимость сочетается с заметными слабыми признаками.</p>
          </div>
        </header>

        <EmptyState
          v-if="intelligence.competitorWeaknesses.length === 0"
          title="Зоны для проверки не найдены"
          description="Для выбранной ниши нет данных этого типа."
        />

        <div v-else class="product-card-grid product-card-grid--checks">
          <article
            v-for="weakness in intelligence.competitorWeaknesses.slice(0, 12)"
            :key="`${weakness.type}-${weakness.wbProductId}-${weakness.position}`"
            class="product-card product-card--check"
            :class="{ 'product-card--clickable': weakness.productRowId }"
            :tabindex="weakness.productRowId ? 0 : undefined"
            :role="weakness.productRowId ? 'button' : undefined"
            @click="openProduct(weakness)"
            @keydown.enter.prevent="openProduct(weakness)"
            @keydown.space.prevent="openProduct(weakness)"
          >
            <div class="product-card__image">
              <MarketProductImage :src="weakness.thumbnailUrl" :alt="productTitle(weakness)" />
            </div>
            <div class="product-card__body">
              <div class="product-card__top">
                <Badge :tone="severityTone(weakness.severity)">{{ weaknessTitle(weakness) }}</Badge>
                <span class="numeric">{{ positionText(weakness.position) }}</span>
              </div>
              <span class="product-card__sku">WB {{ weakness.wbProductId }}</span>
              <h4>{{ productTitle(weakness) }}</h4>
              <p>{{ brandSeller(weakness) }}</p>
              <div class="metric-compare product-card__wide">
                <div class="metric-compare__current">
                  <strong>{{ weaknessMetricValue(weakness) }}</strong>
                  <span
                    v-if="weaknessDelta(weakness)"
                    class="delta-chip"
                    :class="`delta-chip--${weaknessDelta(weakness)?.tone}`"
                  >
                    {{ weaknessDelta(weakness)?.label }}
                  </span>
                </div>
                <span class="metric-compare__reference">{{ weaknessReferenceValue(weakness) }}</span>
              </div>
              <p class="product-card__note">{{ sanitizeText(weakness.explanation) }}</p>
              <Button
                v-if="weakness.productRowId"
                class="product-card__action"
                variant="ghost"
                @click.stop="openProduct(weakness)"
              >
                Подробнее
              </Button>
              <span v-else class="product-card__disabled">Описание недоступно для этой карточки</span>
            </div>
          </article>
        </div>
      </section>

      <section v-else-if="activeSection === 'prices'" class="mi-section app-surface">
        <header class="mi-section__header">
          <div>
            <h2>Скидки и ценовое давление</h2>
            <p>Цены, скидки и отклонения от медианы.</p>
          </div>
        </header>

        <div class="mi-small-grid">
          <div
            v-for="summary in [...promoSummaries, ...priceSummaries]"
            :key="`${summary.type}-${summary.title}`"
            class="mi-metric"
          >
            <span>{{ displayPressureTitle(summary) }}</span>
            <strong>{{ formatSummaryValue(summary.currentValue, summary.unit) }}</strong>
            <span
              v-if="summary.delta !== null"
              class="delta-chip"
              :class="`delta-chip--${deltaView(summary.delta, normalizeUnit(summary.unit)).tone}`"
            >
              {{ deltaView(summary.delta, normalizeUnit(summary.unit)).label }}
            </span>
            <p>{{ displayPressureDescription(summary) }}</p>
          </div>
        </div>

        <div v-if="intelligence.pricePressure.highPriceVisibleProducts.length" class="mi-subsection">
          <h3>Высокая цена и видимость</h3>
          <div class="compact-product-grid">
            <article
              v-for="product in intelligence.pricePressure.highPriceVisibleProducts.slice(0, 8)"
              :key="product.wbProductId"
              class="compact-product"
              :class="{ 'compact-product--clickable': product.productRowId }"
              :tabindex="product.productRowId ? 0 : undefined"
              :role="product.productRowId ? 'button' : undefined"
              @click="openProduct(product)"
              @keydown.enter.prevent="openProduct(product)"
              @keydown.space.prevent="openProduct(product)"
            >
              <div class="compact-product__image">
                <MarketProductImage :src="product.thumbnailUrl" :alt="productTitle(product)" />
              </div>
              <div class="compact-product__main">
                <span class="product-card__sku">WB {{ product.wbProductId }}</span>
                <strong>{{ productTitle(product) }}</strong>
                <span>{{ brandSeller(product) }}</span>
              </div>
              <div class="compact-product__values">
                <span>{{ positionText(product.position) }}</span>
                <div class="compact-product__current">
                  <strong>{{ formatMoney(product.currentPrice) }}</strong>
                  <span
                    v-if="highPriceDelta(product)"
                    class="delta-chip"
                    :class="`delta-chip--${highPriceDelta(product)?.tone}`"
                  >
                    {{ highPriceDelta(product)?.label }}
                  </span>
                </div>
                <span v-if="product.referencePrice !== null">медиана {{ formatMoney(product.referencePrice) }}</span>
              </div>
              <Button
                v-if="product.productRowId"
                class="compact-product__action"
                variant="ghost"
                @click.stop="openProduct(product)"
              >
                Подробнее
              </Button>
            </article>
          </div>
        </div>
      </section>

      <section v-else-if="activeSection === 'stock'" class="mi-section app-surface">
        <header class="mi-section__header">
          <div>
            <h2>Остатки</h2>
            <p>Только явные значения из текущих данных.</p>
          </div>
        </header>

        <div class="mi-small-grid">
          <div v-if="intelligence.stockPressure.exactZeroCount > 0" class="mi-metric">
            <span>Точный ноль</span>
            <strong>{{ formatNumber(intelligence.stockPressure.exactZeroCount) }}</strong>
            <p>показано только при явном значении 0</p>
          </div>
          <div class="mi-metric">
            <span>Низкий остаток</span>
            <strong>{{ formatNumber(intelligence.stockPressure.exactLowStockCount) }}</strong>
            <p>точно указанный низкий остаток</p>
          </div>
          <div class="mi-metric">
            <span>С верхней границей</span>
            <strong>{{ formatNumber(intelligence.stockPressure.cappedCount) }}</strong>
            <p>количество ограничено источником</p>
          </div>
          <div class="mi-metric">
            <span>Остаток не указан</span>
            <strong>{{ formatNumber(intelligence.stockPressure.unknownCount) }}</strong>
            <p>нет точного значения</p>
          </div>
        </div>

        <div v-if="intelligence.stockPressure.highRankLowStockProducts.length" class="mi-subsection">
          <h3>Видимые карточки с низким остатком</h3>
          <div class="compact-product-grid">
            <article
              v-for="product in intelligence.stockPressure.highRankLowStockProducts"
              :key="product.wbProductId"
              class="compact-product"
              :class="{ 'compact-product--clickable': product.productRowId }"
              :tabindex="product.productRowId ? 0 : undefined"
              :role="product.productRowId ? 'button' : undefined"
              @click="openProduct(product)"
              @keydown.enter.prevent="openProduct(product)"
              @keydown.space.prevent="openProduct(product)"
            >
              <div class="compact-product__image">
                <MarketProductImage :src="product.thumbnailUrl" :alt="productTitle(product)" />
              </div>
              <div class="compact-product__main">
                <span class="product-card__sku">WB {{ product.wbProductId }}</span>
                <strong>{{ productTitle(product) }}</strong>
                <span>{{ brandSeller(product) }}</span>
              </div>
              <div class="compact-product__values">
                <span>{{ positionText(product.position) }}</span>
                <strong>Остаток: {{ product.stock.displayValue }}</strong>
                <span>{{ statusLabel(product.stock.status) }}</span>
              </div>
              <Button
                v-if="product.productRowId"
                class="compact-product__action"
                variant="ghost"
                @click.stop="openProduct(product)"
              >
                Подробнее
              </Button>
            </article>
          </div>
        </div>
      </section>

      <section v-else class="mi-section app-surface">
        <header class="mi-section__header">
          <div>
            <h2>Повторы и концентрация</h2>
            <p>Кто занимает заметную часть выбранного топа.</p>
          </div>
        </header>

        <div class="histogram-grid">
          <div class="histogram-panel">
            <h3>Продавцы</h3>
            <article v-for="leader in intelligence.concentration.sellerLeaders" :key="leader.name" class="bar-row">
              <div class="bar-row__head">
                <span>{{ leader.name }}</span>
                <strong>{{ leader.slotsCount }} · {{ formatNumber(leader.sharePercent) }}%</strong>
              </div>
              <div class="bar-row__track">
                <span :style="{ width: concentrationWidth(leader.sharePercent) }" />
              </div>
              <p>{{ sanitizeText(leader.description) }}</p>
            </article>
          </div>

          <div class="histogram-panel">
            <h3>Бренды</h3>
            <article v-for="leader in intelligence.concentration.brandLeaders" :key="leader.name" class="bar-row">
              <div class="bar-row__head">
                <span>{{ leader.name }}</span>
                <strong>{{ leader.slotsCount }} · {{ formatNumber(leader.sharePercent) }}%</strong>
              </div>
              <div class="bar-row__track">
                <span :style="{ width: concentrationWidth(leader.sharePercent) }" />
              </div>
              <p>{{ sanitizeText(leader.description) }}</p>
            </article>
          </div>

          <div class="histogram-panel">
            <h3>Похожие варианты</h3>
            <article v-for="cluster in intelligence.concentration.rootClusters" :key="cluster.wbRootId" class="bar-row">
              <div class="bar-row__head">
                <span>Группа похожих вариантов</span>
                <strong>{{ cluster.productCount }} · {{ positionText(cluster.bestPosition) }}</strong>
              </div>
              <div class="bar-row__track">
                <span :style="{ width: concentrationWidth((cluster.productCount / intelligence.context.topN) * 100) }" />
              </div>
              <p>{{ clusterSummary(cluster) }}</p>
            </article>
          </div>
        </div>

        <article class="mi-limitations-panel">
          <h3>Что важно учитывать</h3>
          <EmptyState
            v-if="combinedLimitations.length === 0"
            title="Явных ограничений нет"
            description="Для выбранной ниши нет дополнительных предупреждений."
          />
          <ul v-else class="mi-limitations">
            <li v-for="limitation in combinedLimitations" :key="limitation">{{ limitation }}</li>
          </ul>
        </article>
      </section>
    </template>

    <MarketProductDetailDrawer :open="Boolean(selectedProduct)" :product="selectedProduct" @close="closeProduct" />
  </div>
</template>

<style scoped>
.market-intelligence {
  display: grid;
  gap: var(--space-4);
}

.mi-controls,
.mi-tabs,
.mi-summary,
.mi-section {
  border-color: rgb(249 115 22 / 0.18);
}

.mi-controls {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
  gap: var(--space-4);
  padding: var(--space-4);
}

.mi-controls__fields {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: var(--space-3);
}

.mi-field {
  display: grid;
  min-width: min(100%, 18rem);
  gap: var(--space-2);
}

.mi-field--short {
  min-width: 9rem;
}

.mi-field span,
.mi-sync span,
.mi-summary span,
.mi-metric span {
  display: block;
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 700;
}

.mi-field small {
  max-width: 16rem;
  color: var(--color-text-subtle);
  font-size: 0.72rem;
  line-height: 1.35;
}

.mi-field select {
  height: 2.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
}

.mi-field select:focus {
  outline: none;
  box-shadow: var(--focus-ring);
}

.mi-sync,
.mi-summary,
.mi-metric,
.product-card,
.compact-product,
.bar-row {
  min-width: 0;
}

.mi-sync strong,
.mi-summary strong,
.mi-metric strong {
  display: block;
  overflow-wrap: anywhere;
}

.mi-sync {
  display: grid;
  width: min(100%, 17rem);
  gap: var(--space-2);
  min-height: 3.65rem;
  align-content: center;
  justify-self: end;
  border: 1px solid rgb(249 115 22 / 0.14);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  padding: 0 var(--space-3);
}

.mi-sync span {
  line-height: 1.2;
  overflow-wrap: anywhere;
}

.mi-refresh {
  align-self: start;
  margin-top: 1.6rem;
  min-height: 2.35rem;
}

.mi-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
}

.mi-summary {
  position: relative;
  overflow: hidden;
  padding: var(--space-4);
  border-color: rgb(249 115 22 / 0.26);
  background:
    linear-gradient(135deg, rgb(249 115 22 / 0.1), transparent 42%),
    var(--color-surface);
}

.mi-summary::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: var(--accent-ember);
}

.mi-summary strong {
  margin-top: var(--space-2);
  color: var(--color-text);
  font-size: 1.55rem;
  line-height: 1.05;
  font-variant-numeric: tabular-nums;
}

.mi-summary p,
.mi-metric p,
.bar-row p,
.product-card p,
.product-card__note {
  margin: var(--space-2) 0 0;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  line-height: 1.4;
}

.mi-tabs {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3);
  border-color: rgb(249 115 22 / 0.34);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--color-surface);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.035), 0 12px 34px rgb(0 0 0 / 0.18);
}

.mi-tabs__label {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--accent-ember-text-strong);
  font-size: 0.78rem;
  font-weight: 820;
  text-transform: uppercase;
}

.mi-tabs__label::before {
  display: inline-block;
  width: 0.55rem;
  height: 0.55rem;
  border-radius: 999px;
  background: var(--accent-ember);
  box-shadow: 0 0 14px rgb(249 115 22 / 0.52);
  content: '';
}

.mi-tabs__item {
  min-height: 2.35rem;
  border: 1px solid rgb(249 115 22 / 0.14);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-4);
  font-size: 0.8125rem;
  font-weight: 820;
}

.mi-tabs__item:hover,
.mi-tabs__item--active {
  border-color: var(--accent-primary-border);
  background: var(--button-primary-bg);
  color: var(--text-on-fire);
}

.mi-section {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
}

.mi-section__header,
.event-group__header,
.product-card__top,
.bar-row__head,
.compact-product {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.mi-section__header h2,
.event-group__header h3,
.mi-subsection h3,
.histogram-panel h3,
.product-card h4 {
  margin: 0;
  font-size: 1rem;
  font-weight: 750;
}

.mi-section__header p,
.event-group__header p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.event-groups,
.mi-subsection,
.histogram-panel {
  display: grid;
  gap: var(--space-3);
}

.event-group,
.product-card,
.compact-product,
.mi-metric,
.histogram-panel,
.bar-row,
.mi-limitations-panel {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
}

.event-group {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-3);
}

.product-card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}

.product-card {
  display: grid;
  grid-template-columns: 5.75rem minmax(0, 1fr);
  align-items: start;
  overflow: hidden;
  min-height: 7.75rem;
  transition: border-color 140ms ease, background 140ms ease, box-shadow 140ms ease, transform 140ms ease;
}

.product-card--clickable,
.compact-product--clickable {
  cursor: pointer;
}

.product-card--clickable:hover,
.product-card--clickable:focus-visible,
.compact-product--clickable:hover,
.compact-product--clickable:focus-visible {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.08), transparent 42%),
    var(--color-surface-hover);
  box-shadow: inset 2px 0 0 rgb(249 115 22 / 0.42), 0 14px 34px rgb(0 0 0 / 0.18);
  outline: none;
}

.product-card__image,
.compact-product__image {
  display: grid;
  place-items: center;
  overflow: hidden;
  background: var(--surface-control);
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 800;
}

.product-card__image {
  width: 5.75rem;
  height: 7.25rem;
  border-right: 1px solid var(--color-border);
}

.product-card__image :deep(.market-image),
.compact-product__image :deep(.market-image) {
  height: 100%;
  width: 100%;
}

.product-card__image :deep(.market-image__asset),
.product-card__image :deep(.market-image__preview),
.compact-product__image :deep(.market-image__asset),
.compact-product__image :deep(.market-image__preview) {
  object-fit: contain;
}

.product-card__body {
  display: grid;
  gap: 0.45rem;
  padding: var(--space-3);
  grid-template-columns: minmax(0, 1fr);
}

.product-card--check .product-card__body {
  min-height: 100%;
}

.product-card h4 {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  font-size: 0.95rem;
  line-height: 1.25;
}

.product-card__wide {
  grid-column: 1 / -1;
  margin-left: calc(-5.75rem - var(--space-3));
  margin-top: var(--space-1);
 }

.product-card__sku {
  color: var(--color-text-subtle);
  font-size: 0.75rem;
  font-weight: 700;
}

.numeric,
.value-line,
.metric-compare {
  font-variant-numeric: tabular-nums;
}

.value-line,
.metric-compare {
  display: grid;
  gap: var(--space-1);
  align-self: stretch;
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-2);
}

.value-line span,
.metric-compare span {
  color: var(--color-text-muted);
}

.value-line {
  display: grid;
  gap: var(--space-2);
}

.value-line__meta,
.metric-compare__current {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.metric-compare__current strong {
  font-size: 1rem;
}

.metric-compare__reference {
  font-size: 0.78rem;
}

.delta-chip {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 0.18rem 0.42rem;
  font-size: 0.75rem;
  font-weight: 750;
  font-variant-numeric: tabular-nums;
}

.delta-chip--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.delta-chip--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.delta-chip--neutral {
  border-color: var(--heat-dormant-border);
  background: var(--heat-dormant-soft);
  color: var(--heat-dormant-text);
}

.product-card__action,
.compact-product__action {
  justify-self: start;
}

.product-card__action {
  grid-column: 1 / -1;
  margin-left: calc(-5.75rem - var(--space-3));
}

.product-card__disabled {
  align-self: end;
  color: var(--color-text-subtle);
  font-size: 0.75rem;
}

.event-group__toggle {
  justify-self: start;
}

.mi-small-grid,
.histogram-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}

.mi-metric {
  display: grid;
  gap: var(--space-2);
  padding: var(--space-3);
}

.mi-metric strong {
  font-size: 1.15rem;
}

.mi-subsection {
  margin-top: var(--space-4);
}

.compact-product {
  display: grid;
  grid-template-columns: 4.75rem minmax(0, 1fr);
  grid-template-areas:
    'image main'
    'values values'
    'action action';
  align-items: start;
  padding: var(--space-3);
  transition: border-color 140ms ease, background 140ms ease, box-shadow 140ms ease;
}

.compact-product__image {
  grid-area: image;
  aspect-ratio: 1;
  border-radius: var(--radius-md);
}

.compact-product__main,
.compact-product__values {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
}

.compact-product__main {
  grid-area: main;
}

.compact-product strong,
.compact-product span,
.bar-row span,
.bar-row strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.compact-product span,
.bar-row p {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.compact-product__values {
  grid-area: values;
  justify-items: start;
  font-variant-numeric: tabular-nums;
  margin-top: var(--space-2);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-2);
}

.compact-product__action {
  grid-area: action;
  margin-top: var(--space-2);
}

.compact-product-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
  margin-top: var(--space-3);
}

.compact-product__current {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.histogram-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.histogram-panel {
  align-content: start;
  padding: var(--space-3);
}

.bar-row {
  display: grid;
  gap: var(--space-2);
  padding: var(--space-3);
}

.bar-row__track {
  height: 0.8rem;
  overflow: hidden;
  border: 1px solid rgb(249 115 22 / 0.18);
  border-radius: 999px;
  background: rgb(15 23 42 / 0.72);
}

.bar-row__track span {
  display: block;
  min-width: 0.75rem;
  height: 100%;
  border-radius: inherit;
  background: var(--accent-ember);
  box-shadow: 0 0 18px rgb(249 115 22 / 0.36);
}

.mi-limitations-panel {
  display: grid;
  gap: var(--space-3);
  margin-top: var(--space-3);
  padding: var(--space-3);
}

.mi-limitations-panel h3 {
  margin: 0;
  font-size: 0.95rem;
}

.mi-limitations {
  display: grid;
  gap: var(--space-2);
  margin: 0;
  padding-left: 1rem;
  color: var(--color-text-muted);
}

@media (max-width: 1180px) {
  .mi-summary-grid,
  .histogram-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 860px) {
  .mi-controls {
    grid-template-columns: 1fr;
  }

  .mi-sync {
    justify-self: stretch;
  }

  .mi-summary-grid,
  .mi-small-grid,
  .histogram-grid,
  .compact-product-grid,
  .product-card-grid {
    grid-template-columns: 1fr;
  }

  .compact-product {
    grid-template-columns: 4.5rem minmax(0, 1fr);
  }

  .compact-product__values,
  .compact-product__action {
    justify-items: start;
  }
}

@media (max-width: 720px) {
  .mi-field,
  .mi-sync,
  .mi-refresh {
    width: 100%;
  }

  .mi-refresh {
    margin-top: 0;
  }

  .product-card {
    grid-template-columns: 5.25rem minmax(0, 1fr);
  }

  .product-card__image {
    width: 5.25rem;
    height: 6.75rem;
  }

  .product-card__wide,
  .product-card__action {
    margin-left: calc(-5.25rem - var(--space-3));
  }
}
</style>
