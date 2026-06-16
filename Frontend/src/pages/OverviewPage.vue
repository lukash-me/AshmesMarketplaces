<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { Eye, History } from 'lucide-vue-next';

import {
  getWorkspaceOverview,
  markWorkspaceOverviewViewed
} from '@/features/overview/workspaceOverview.api';
import type {
  WorkspaceOverview,
  WorkspaceOverviewChange,
  WorkspaceOverviewGroup,
  WorkspaceOverviewProduct,
  WorkspaceOverviewSimilarProductGroup,
  WorkspaceOverviewSimilarProductGroupItem,
  WorkspaceOverviewSimilarProduct
} from '@/features/overview/workspaceOverview.types';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import { useAuthStore } from '@/features/auth/auth.store';
import { useActiveWorkspace } from '@/features/workspace-market-products/useActiveWorkspace';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

type ProductMetric = {
  label: string;
  value: string;
};

type TagItem = {
  key: string;
  label: string;
  tone: 'positive' | 'negative' | 'neutral' | 'warning';
};

const workspace = useActiveWorkspace();
const authStore = useAuthStore();
const { user } = storeToRefs(authStore);
const overview = ref<WorkspaceOverview | null>(null);
const loading = ref(false);
const error = ref('');
const selectedProduct = ref<ParserProductListItem | null>(null);
const activeSimilarGroups = ref<Record<string, string>>({});
const markingViewedIds = ref<Set<string>>(new Set());
const markingAllViewed = ref(false);
let requestVersion = 0;

const activeWorkspaceId = computed(() => workspace.activeWorkspaceId.value);
const hasProducts = computed(() => (overview.value?.workspaceProductCount ?? 0) > 0);
const groups = computed<WorkspaceOverviewGroup[]>(() =>
  overview.value ? [overview.value.newItems] : []
);
const overviewScheduleText = computed(() => {
  const schedule = user.value?.analysisSchedule;
  if (!schedule) {
    return 'Обзор обновляется автоматически один раз в сутки.';
  }

  return `Обновляется ежедневно в ${schedule.overviewLocalTime}. Следующий запуск: ${formatScheduleDate(schedule.nextOverviewRunAtUtc)}.`;
});

watch(
  activeWorkspaceId,
  () => {
    void loadOverview();
  },
  { immediate: true }
);

async function loadOverview(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const version = ++requestVersion;

  if (!workspaceId) {
    overview.value = null;
    error.value = '';
    return;
  }

  loading.value = true;
  error.value = '';

  try {
    const response = await getWorkspaceOverview(workspaceId);
    if (version === requestVersion) {
      overview.value = response;
    }
  } catch (requestError) {
    if (version === requestVersion) {
      error.value = getProblemMessage(requestError, 'Не удалось загрузить обзор рабочей области.');
      overview.value = null;
    }
  } finally {
    if (version === requestVersion) {
      loading.value = false;
    }
  }
}

function formatScheduleDate(value: string | null | undefined): string {
  if (!value) {
    return 'ожидает назначения';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'ожидает назначения';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

async function markViewed(product: WorkspaceOverviewProduct): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  if (!workspaceId || markingViewedIds.value.has(product.id)) {
    return;
  }

  setMarkingViewed(product.id, true);
  error.value = '';

  try {
    await markWorkspaceOverviewViewed(workspaceId, { productIds: [product.id] });
    await loadOverview();
  } catch (requestError) {
    error.value = getProblemMessage(requestError, 'Не удалось отметить изменения просмотренными.');
  } finally {
    setMarkingViewed(product.id, false);
  }
}

async function markAllViewed(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const productIds = overview.value?.newItems.products.map((product) => product.id) ?? [];
  if (!workspaceId || productIds.length === 0 || markingAllViewed.value) {
    return;
  }

  markingAllViewed.value = true;
  error.value = '';

  try {
    await markWorkspaceOverviewViewed(workspaceId, { productIds });
    await loadOverview();
  } catch (requestError) {
    error.value = getProblemMessage(requestError, 'Не удалось отметить все изменения просмотренными.');
  } finally {
    markingAllViewed.value = false;
  }
}

function setMarkingViewed(id: string, marking: boolean): void {
  const next = new Set(markingViewedIds.value);
  if (marking) {
    next.add(id);
  } else {
    next.delete(id);
  }
  markingViewedIds.value = next;
}

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value);
}

function formatMoney(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function formatRating(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

function formatRatingDelta(value: number): string {
  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1
  }).format(value);
}

function productMetrics(product: WorkspaceOverviewProduct): ProductMetric[] {
  return [
    { label: 'Цена', value: formatMoney(product.currentPrice) },
    { label: 'Позиция', value: product.currentPosition ? `#${formatNumber(product.currentPosition)}` : 'Нет данных' },
    { label: 'Остаток', value: formatNumber(product.currentStock) },
    { label: 'Отзывы', value: formatNumber(product.currentFeedbackCount) },
    { label: 'Оценка', value: formatRating(product.currentReviewRating) }
  ];
}

function comparisonTags(
  product: WorkspaceOverviewProduct,
  similar: WorkspaceOverviewSimilarProduct
): TagItem[] {
  const tags: TagItem[] = [];

  if (product.currentPrice !== null && similar.price !== null) {
    const delta = similar.price - product.currentPrice;
    if (Math.abs(delta) >= 1) {
      tags.push({
        key: 'price',
        label: delta < 0 ? `Дешевле на ${formatMoney(Math.abs(delta))}` : `Дороже на ${formatMoney(delta)}`,
        tone: delta < 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentPosition !== null && similar.position !== null) {
    const delta = similar.position - product.currentPosition;
    if (delta !== 0) {
      tags.push({
        key: 'position',
        label: delta < 0
          ? `Выше на ${formatNumber(Math.abs(delta))} ${placeWord(Math.abs(delta))}`
          : `Ниже на ${formatNumber(delta)} ${placeWord(delta)}`,
        tone: delta < 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentFeedbackCount !== null && similar.feedbackCount !== null) {
    const delta = similar.feedbackCount - product.currentFeedbackCount;
    if (delta !== 0) {
      tags.push({
        key: 'feedbacks',
        label: delta > 0
          ? `Отзывов больше на ${formatNumber(delta)}`
          : `Отзывов меньше на ${formatNumber(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentReviewRating !== null && similar.rating !== null) {
    const delta = similar.rating - product.currentReviewRating;
    if (Math.abs(delta) >= 0.05) {
      tags.push({
        key: 'reviewRating',
        label: delta > 0
          ? `Оценка выше на ${formatRatingDelta(Math.abs(delta))}`
          : `Оценка ниже на ${formatRatingDelta(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentStock !== null && similar.totalQuantity !== null) {
    const delta = similar.totalQuantity - product.currentStock;
    if (delta !== 0) {
      tags.push({
        key: 'stock',
        label: delta > 0
          ? `Остаток выше на ${formatNumber(delta)}`
          : `Остаток ниже на ${formatNumber(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  return tags.length > 0
    ? tags
    : [{ key: 'similar', label: 'Похожа по карточке', tone: 'neutral' }];
}

function changeTags(product: WorkspaceOverviewProduct): TagItem[] {
  return [
    product.priceChange,
    product.positionChange,
    product.stockChange,
    product.feedbackChange,
    product.reviewRatingChange
  ]
    .filter((change) => change.delta !== null && change.delta !== 0)
    .map((change) => ({
      key: change.key,
      label: `${changeTagLabel(change)} ${changeDisplayValue(change)}`,
      tone: change.state === 'positive'
        ? 'positive'
        : change.state === 'negative'
          ? 'negative'
          : 'neutral'
    }));
}

function signalTags(product: WorkspaceOverviewProduct): TagItem[] {
  const changeSignalCodes = new Set(['price_changed', 'position_changed', 'stock_changed', 'new_reviews']);
  return product.signals
    .filter((signal) => !changeSignalCodes.has(signal.code))
    .slice(0, 5)
    .map((signal) => ({
      key: signal.code,
      label: signal.title,
      tone: signal.severity === 'high'
        ? 'negative'
        : signal.severity === 'medium'
          ? 'warning'
          : 'neutral'
    }));
}

function similarTags(
  product: WorkspaceOverviewProduct,
  similar: WorkspaceOverviewSimilarProduct
): TagItem[] {
  return comparisonTags(product, similar);
}

function similarGroupTitle(group: WorkspaceOverviewSimilarProductGroup): string {
  const titles: Record<string, string> = {
    price_disadvantage: 'Дешевле',
    position_disadvantage: 'Выше в выдаче',
    review_count_disadvantage: 'Больше отзывов',
    rating_disadvantage: 'Оценка выше',
    stock_disadvantage: 'Остаток выше',
    weak_competitor_cards: 'Слабые похожие',
    similar_faster_region_delivery: 'Похожие доставляют быстрее'
  };

  return titles[group.key] ?? group.title;
}

function similarGroupItemTags(
  product: WorkspaceOverviewProduct,
  item: WorkspaceOverviewSimilarProductGroupItem,
  groupKey: string
): TagItem[] {
  if (groupKey === 'weak_competitor_cards') {
    const facts = item.tags.length > 0 ? item.tags : item.facts;
    return facts.length > 0
      ? facts.map((fact, index) => ({
          key: `weak-${index}`,
          label: compactWeakFact(fact),
          tone: 'negative'
        }))
      : [{ key: 'weak', label: 'Слабые параметры', tone: 'negative' }];
  }

  if (groupKey === 'similar_faster_region_delivery') {
    const facts = item.tags.length > 0 ? item.tags : item.facts;
    return facts.length > 0
      ? [{
          key: 'delivery',
          label: compactDeliveryFacts(facts),
          tone: 'positive'
        }]
      : [{ key: 'delivery', label: 'Похожая доставляется быстрее', tone: 'positive' }];
  }

  return comparisonTags(product, item.product);
}

function compactDeliveryFacts(facts: string[]): string {
  const lines = facts
    .map((fact) => fact.replace(/^Похожая быстрее в\s+/i, '').trim())
    .filter(Boolean);

  return lines.length > 1
    ? `Похожие доставляют быстрее:\n  ${lines.join('\n  ')}`
    : `Похожие доставляют быстрее: ${lines[0]}`;
}

function compactWeakFact(value: string): string {
  return value
    .replace(/^У похожей\s+/i, '')
    .replace(/:\s*/g, ' ')
    .replace(/^низкая/i, 'Низкая')
    .replace(/^мало/i, 'Мало')
    .replace(/^низкий/i, 'Низкий');
}

function similarGroupTabs(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroup[] {
  return (product.similarProductGroups ?? []).filter((group) => group.items.length > 0);
}

function activeSimilarGroup(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroup | null {
  const tabs = similarGroupTabs(product);
  if (tabs.length === 0) {
    return null;
  }

  const selected = activeSimilarGroups.value[product.id];
  const selectedGroup = selected ? tabs.find((group) => group.key === selected) : null;
  if (selectedGroup) {
    return selectedGroup;
  }

  return preferredSimilarGroup(tabs);
}

function activeSimilarGroupItems(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroupItem[] {
  return activeSimilarGroup(product)?.items ?? [];
}

function activeSimilarGroupKey(product: WorkspaceOverviewProduct): string {
  return activeSimilarGroup(product)?.key ?? '';
}

function activeSimilarGroupDescription(product: WorkspaceOverviewProduct): string {
  return activeSimilarGroup(product)?.description ?? '';
}

function preferredSimilarGroup(groups: WorkspaceOverviewSimilarProductGroup[]): WorkspaceOverviewSimilarProductGroup {
  const priority = [
    'price_disadvantage',
    'position_disadvantage',
    'review_count_disadvantage',
    'rating_disadvantage',
    'stock_disadvantage',
    'similar_faster_region_delivery',
    'weak_competitor_cards'
  ];
  for (const key of priority) {
    const group = groups.find((item) => item.key === key);
    if (group) {
      return group;
    }
  }

  return groups[0];
}

function selectSimilarGroup(productId: string, key: string): void {
  activeSimilarGroups.value = {
    ...activeSimilarGroups.value,
    [productId]: key
  };
}

function changeTagLabel(change: WorkspaceOverviewChange): string {
  if (change.key === 'price') {
    return 'Новая цена';
  }

  if (change.key === 'position') {
    return 'Новая позиция';
  }

  if (change.key === 'feedbacks') {
    return 'Новые отзывы';
  }

  if (change.key === 'reviewRating') {
    return 'Новая оценка';
  }

  if (change.key === 'stock') {
    return 'Новые остатки';
  }

  return change.label;
}

function changeDisplayValue(change: WorkspaceOverviewChange): string {
  if (change.key === 'position' && change.delta !== null) {
    if (change.delta === 0) {
      return 'без изменений';
    }

    return change.delta < 0
      ? `↑ ${formatNumber(Math.abs(change.delta))} ${placeWord(Math.abs(change.delta))}`
      : `↓ ${formatNumber(Math.abs(change.delta))} ${placeWord(Math.abs(change.delta))}`;
  }

  if (change.key === 'price' && change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatMoney(Math.abs(change.delta))}`;
  }

  if (change.key === 'reviewRating' && change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatRating(Math.abs(change.delta))}`;
  }

  if (change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatNumber(Math.abs(change.delta))}`;
  }

  return change.displayValue;
}

function placeWord(value: number): string {
  const normalized = Math.abs(Math.trunc(value));
  const lastTwo = normalized % 100;
  const last = normalized % 10;

  if (lastTwo >= 11 && lastTwo <= 14) {
    return 'мест';
  }

  if (last === 1) {
    return 'место';
  }

  if (last >= 2 && last <= 4) {
    return 'места';
  }

  return 'мест';
}

function tagClass(tag: TagItem): string {
  return `overview-tag--${tag.tone}`;
}

function toParserProduct(product: WorkspaceOverviewProduct): ParserProductListItem {
  return {
    id: product.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: product.latestObservedAtUtc ?? new Date().toISOString(),
    wbProductId: product.wbProductId,
    wbRootId: product.wbRootId,
    name: product.name,
    brandName: product.brandName,
    sellerName: product.sellerName,
    priceRegular: product.currentPrice,
    priceDiscounted: product.currentPrice,
    priceWbWallet: product.currentPrice,
    discountPercent: null,
    totalQuantity: product.currentStock,
    ratingRounded: product.currentReviewRating ? Math.round(product.currentReviewRating) : null,
    reviewRating: product.currentReviewRating,
    feedbackCount: product.currentFeedbackCount,
    sourceCategory: product.sourceCategory,
    sourceSubcategory: product.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: product.thumbnailUrl,
    rank: product.currentPosition
      ? {
          absolutePosition: product.currentPosition,
          page: 1,
          positionOnPage: product.currentPosition,
          query: product.sourceSubcategory ?? '',
          sourceCategory: product.sourceCategory,
          sourceSubcategory: product.sourceSubcategory,
          sourceRegionDest: null,
          sort: null,
          observedAtUtc: product.latestObservedAtUtc ?? new Date().toISOString(),
          parserRunId: '',
          rankContextId: '',
          contextsCount: 1
        }
      : null,
    position: {
      state: product.currentPosition ? 'observed' : 'unknown',
      absolutePosition: product.currentPosition,
      observedRangeLimit: null,
      query: product.sourceSubcategory,
      sourceCategory: product.sourceCategory,
      sourceSubcategory: product.sourceSubcategory,
      observedAtUtc: product.latestObservedAtUtc
    },
    parsedReviewEvidence: undefined,
    logistics: null
  };
}

function toParserSimilarProduct(similar: WorkspaceOverviewSimilarProduct): ParserProductListItem | null {
  if (!similar.parserProductRowId || !similar.wbProductId) {
    return null;
  }

  return {
    id: similar.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: new Date().toISOString(),
    wbProductId: similar.wbProductId,
    wbRootId: similar.wbRootId,
    name: similar.name,
    brandName: similar.brandName,
    sellerName: similar.sellerName,
    priceRegular: similar.price,
    priceDiscounted: similar.price,
    priceWbWallet: similar.price,
    discountPercent: null,
    totalQuantity: similar.totalQuantity,
    ratingRounded: similar.rating ? Math.round(similar.rating) : null,
    reviewRating: similar.rating,
    feedbackCount: similar.feedbackCount,
    sourceCategory: null,
    sourceSubcategory: similar.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: similar.thumbnailUrl,
    rank: similar.position
      ? {
          absolutePosition: similar.position,
          page: 1,
          positionOnPage: similar.position,
          query: similar.sourceSubcategory ?? '',
          sourceCategory: null,
          sourceSubcategory: similar.sourceSubcategory,
          sourceRegionDest: null,
          sort: null,
          observedAtUtc: new Date().toISOString(),
          parserRunId: '',
          rankContextId: '',
          contextsCount: 1
        }
      : null,
    position: {
      state: similar.position ? 'observed' : 'unknown',
      absolutePosition: similar.position,
      observedRangeLimit: null,
      query: similar.sourceSubcategory,
      sourceCategory: null,
      sourceSubcategory: similar.sourceSubcategory,
      observedAtUtc: null
    },
    parsedReviewEvidence: undefined,
    logistics: null
  };
}

function openProduct(product: WorkspaceOverviewProduct): void {
  selectedProduct.value = toParserProduct(product);
}

function openSimilarProduct(similar: WorkspaceOverviewSimilarProduct): void {
  selectedProduct.value = toParserSimilarProduct(similar);
}
</script>

<template>
  <div class="overview-page">
    <PageHeader
      title="Обзор"
      description="Сводка по товарам, которые добавлены в рабочую область."
    />
    <p class="analysis-schedule-note">{{ overviewScheduleText }}</p>

    <p v-if="error" class="overview-error">{{ error }}</p>

    <LoadingState v-if="loading" :rows="6" />

    <EmptyState
      v-else-if="!activeWorkspaceId"
      title="Рабочая область не выбрана"
      description="Выберите рабочую область, чтобы увидеть наблюдаемые товары."
    />

    <EmptyState
      v-else-if="!hasProducts"
      title="В рабочей области пока нет товаров"
      description="Добавьте карточки из маркетинговой разведки, чтобы отслеживать изменения и похожие товары."
    >
      <RouterLink class="app-operator-link" to="/market/intelligence">
        Перейти в маркетинговую разведку
      </RouterLink>
    </EmptyState>

    <template v-else>
      <section
        v-for="group in groups"
        :key="group.key"
        class="overview-group app-operator-panel"
      >
        <header class="overview-group__header">
          <div>
            <h2>{{ group.label }}</h2>
            <span>{{ group.count }} товаров</span>
          </div>
          <Button
            v-if="group.key === 'new' && group.products.length > 0"
            variant="primary"
            :loading="markingAllViewed"
            @click="markAllViewed"
          >
            Отметить все просмотренными
          </Button>
        </header>

        <EmptyState
          v-if="group.products.length === 0"
          :title="group.key === 'new' ? 'Новых изменений нет' : 'В этой группе нет товаров'"
          :description="group.key === 'new' ? 'Когда по наблюдаемым товарам появятся изменения после вашего последнего просмотра, они появятся здесь.' : 'Изменить тег можно на странице наблюдаемых товаров.'"
        />

        <div v-else class="overview-list">
          <article
            v-for="product in group.products"
            :key="product.id"
            class="overview-card app-operator-card"
          >
            <div class="overview-card__head">
              <button class="overview-card__image" type="button" @click="openProduct(product)">
                <MarketProductImage :src="product.thumbnailUrl" :alt="product.name" />
              </button>

              <div class="overview-card__identity">
                <span>{{ product.sourceSubcategory || product.sourceCategory || 'Ниша не указана' }}</span>
                <h3>{{ product.name }}</h3>
                <p>{{ product.brandName || 'Бренд не указан' }} · {{ product.sellerName || 'Продавец не указан' }}</p>
              </div>

              <div class="overview-card__metrics">
                <div v-for="metric in productMetrics(product)" :key="metric.label" class="app-operator-metric app-operator-value-metric">
                  <span>{{ metric.label }}</span>
                  <strong>{{ metric.value }}</strong>
                </div>
              </div>
            </div>

            <div class="overview-tags">
              <span
                v-for="tag in changeTags(product)"
                :key="tag.key"
                class="overview-tag"
                :class="tagClass(tag)"
              >
                {{ tag.label }}
              </span>
              <span
                v-for="tag in signalTags(product)"
                :key="tag.key"
                class="overview-tag"
                :class="tagClass(tag)"
              >
                {{ tag.label }}
              </span>
              <span v-if="changeTags(product).length === 0 && signalTags(product).length === 0" class="overview-tag overview-tag--neutral">
                Значимых изменений нет
              </span>
            </div>

            <div class="overview-similar-block">
              <div class="overview-similar-block__header">
                <h4>Похожие товары</h4>
                <span v-if="similarGroupTabs(product).length === 0 && product.similarProducts.length" class="overview-muted">
                  Обновите анализ, чтобы увидеть подборки
                </span>
              </div>

              <div v-if="similarGroupTabs(product).length" class="overview-similar-tabs" role="tablist" aria-label="Подборки похожих товаров">
                <button
                  v-for="tab in similarGroupTabs(product)"
                  :key="tab.key"
                  class="overview-similar-tab"
                  :class="{ 'overview-similar-tab--active': activeSimilarGroupKey(product) === tab.key }"
                  type="button"
                  @click="selectSimilarGroup(product.id, tab.key)"
                >
                  {{ similarGroupTitle(tab) }}
                  <span>{{ tab.items.length }}</span>
                </button>
              </div>

              <p v-if="activeSimilarGroupDescription(product)" class="overview-muted">
                {{ activeSimilarGroupDescription(product) }}
              </p>

              <div v-if="activeSimilarGroupItems(product).length" class="overview-similar-grid">
                <button
                  v-for="item in activeSimilarGroupItems(product)"
                  :key="`${item.product.productKey}-${activeSimilarGroupKey(product)}`"
                  class="overview-similar-card app-operator-card app-operator-card--interactive"
                  type="button"
                  :disabled="!item.product.parserProductRowId"
                  @click="openSimilarProduct(item.product)"
                >
                  <MarketProductImage :src="item.product.thumbnailUrl" :alt="item.product.name" />
                  <div class="overview-similar-card__body">
                    <strong>{{ item.product.name }}</strong>
                    <div class="overview-tags overview-tags--compact">
                      <span
                        v-for="tag in similarGroupItemTags(product, item, activeSimilarGroupKey(product))"
                        :key="tag.key"
                        class="overview-tag"
                        :class="tagClass(tag)"
                      >
                        {{ tag.label }}
                      </span>
                    </div>
                  </div>
                </button>
              </div>

              <div v-else-if="product.similarProducts.length" class="overview-similar-grid">
                <button
                  v-for="similar in product.similarProducts.slice(0, 3)"
                  :key="similar.productKey"
                  class="overview-similar-card app-operator-card app-operator-card--interactive"
                  type="button"
                  :disabled="!similar.parserProductRowId"
                  @click="openSimilarProduct(similar)"
                >
                  <MarketProductImage :src="similar.thumbnailUrl" :alt="similar.name" />
                  <div class="overview-similar-card__body">
                    <strong>{{ similar.name }}</strong>
                    <div class="overview-tags overview-tags--compact">
                      <span
                        v-for="tag in similarTags(product, similar)"
                        :key="tag.key"
                        class="overview-tag"
                        :class="tagClass(tag)"
                      >
                        {{ tag.label }}
                      </span>
                    </div>
                  </div>
                </button>
              </div>
              <p v-else class="overview-muted">Похожие товары пока не найдены.</p>
            </div>

            <footer class="overview-card__actions">
              <Button
                v-if="group.key === 'new'"
                variant="primary"
                :loading="markingViewedIds.has(product.id)"
                @click="markViewed(product)"
              >
                Отметить просмотренным
              </Button>
              <button class="app-operator-link" type="button" @click="openProduct(product)">
                <Eye :size="15" />
                Карточка
              </button>
              <RouterLink class="app-operator-link" to="/workspace/market-products">
                <History :size="15" />
                История
              </RouterLink>
            </footer>
          </article>
        </div>
      </section>
    </template>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
  </div>
</template>

<style scoped>
.overview-page {
  display: grid;
  gap: var(--space-5);
}

.analysis-schedule-note {
  margin: calc(var(--space-3) * -1) 0 0;
  color: var(--text-muted);
  font-size: 0.9rem;
}

.overview-error {
  margin: 0;
  border: 1px solid var(--state-danger-border-strong);
  border-radius: var(--radius-lg);
  background: var(--state-danger-soft);
  padding: var(--space-3);
  color: var(--state-danger);
  font-weight: 650;
}

.overview-group {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.overview-group__header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-3);
}

.overview-group__header h2,
.overview-card__identity h3,
.overview-similar-block h4 {
  margin: 0;
}

.overview-group__header h2 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 780;
}

.overview-group__header span,
.overview-card__identity span,
.overview-card__identity p,
.overview-muted {
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
}

.overview-list {
  display: grid;
  gap: var(--space-4);
}

.overview-card {
  display: grid;
  gap: var(--space-4);
  width: 100%;
  padding: var(--space-4);
}

.overview-card__head {
  display: grid;
  grid-template-columns: 5.75rem minmax(16rem, 1fr) minmax(26rem, 0.95fr);
  gap: var(--space-4);
  align-items: start;
}

.overview-card__image {
  display: grid;
  height: 7.25rem;
  width: 5.75rem;
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-metric-bg);
  padding: 0;
  cursor: pointer;
}

.overview-card__image:focus-visible,
.overview-similar-card:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.overview-card__image :deep(.market-image) {
  height: 100%;
  width: 100%;
}

.overview-card__identity {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
  padding-top: var(--space-1);
}

.overview-card__identity span {
  font-weight: 650;
}

.overview-card__identity h3 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 780;
  line-height: 1.28;
}

.overview-card__identity p {
  margin: 0;
}

.overview-card__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--space-2);
}

.overview-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: var(--space-2);
}

.overview-tags--compact {
  align-items: flex-start;
  gap: var(--space-1);
}

.overview-tag {
  display: inline-flex;
  flex: 0 0 auto;
  width: fit-content;
  max-width: 100%;
  min-height: 1.75rem;
  align-items: flex-start;
  border: 1px solid var(--operator-border-muted);
  border-radius: 999px;
  background: var(--operator-metric-bg);
  padding: 0.28rem var(--space-3);
  color: var(--color-text);
  font-size: var(--operator-meta-size);
  font-weight: 760;
  line-height: 1.18;
  text-align: left;
  white-space: pre-line;
}

.overview-tag--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.overview-tag--negative {
  border-color: var(--state-danger-border-strong);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.overview-tag--warning {
  border-color: var(--state-warning-border);
  background: var(--state-warning-soft);
  color: var(--heat-warm-text);
}

.overview-tag--neutral {
  border-color: var(--heat-dormant-border);
  background: var(--heat-dormant-soft);
  color: var(--heat-dormant-text);
}

.overview-similar-block {
  display: grid;
  gap: var(--space-3);
  padding-top: var(--space-1);
}

.overview-similar-block__header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2);
}

.overview-similar-block h4 {
  color: var(--color-text);
  font-size: 0.925rem;
  font-weight: 780;
}

.overview-similar-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.overview-similar-tab {
  display: inline-flex;
  min-height: 2.15rem;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-card-bg);
  padding: 0 var(--space-3);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-body-size);
  font-weight: 760;
  transition:
    border-color 140ms ease,
    background 140ms ease,
    color 140ms ease;
}

.overview-similar-tab:hover {
  border-color: var(--accent-primary-border);
  background: var(--operator-card-hover-bg);
}

.overview-similar-tab:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.overview-similar-tab--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.overview-similar-tab--active:hover {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.overview-similar-tab span {
  display: inline-flex;
  min-width: 1.35rem;
  justify-content: center;
  border-radius: 999px;
  background: var(--operator-metric-bg);
  padding: 0.12rem 0.45rem;
  color: inherit;
  font-size: var(--operator-meta-size);
}

.overview-similar-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-3);
}

.overview-similar-card {
  display: grid;
  grid-template-columns: 4.25rem minmax(0, 1fr);
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-3);
  text-align: left;
}

.overview-similar-card:disabled {
  cursor: default;
  opacity: 0.82;
}

.overview-similar-card :deep(.market-image) {
  height: 5.25rem;
  width: 4.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--operator-metric-bg);
}

.overview-similar-card__body {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.overview-similar-card__body > strong {
  display: -webkit-box;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.875rem;
  font-weight: 780;
  line-height: 1.25;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.overview-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  border-top: 1px solid var(--color-border);
  padding-top: var(--space-3);
}

.overview-card__actions .app-operator-link {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
}

button.app-operator-link {
  border: 0;
  background: transparent;
  padding: 0;
  font: inherit;
}

@media (max-width: 1260px) {
  .overview-card__head {
    grid-template-columns: 5.75rem minmax(0, 1fr);
  }

  .overview-card__metrics {
    grid-column: 1 / -1;
  }

  .overview-similar-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 900px) {
  .overview-card__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 620px) {
  .overview-card__head,
  .overview-card__metrics {
    grid-template-columns: 1fr;
  }

  .overview-card__image {
    height: 8rem;
    width: 6rem;
  }
}
</style>
