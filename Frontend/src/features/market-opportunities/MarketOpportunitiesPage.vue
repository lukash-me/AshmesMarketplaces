<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { RefreshCw, SlidersHorizontal } from 'lucide-vue-next';

import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import {
  getHotProductsRecommendations,
  recalculateHotProductsRecommendations
} from '@/features/parser-products/hotProductsRecommendations.api';
import type {
  HotProductRecommendationItem,
  HotProductsDuplicateCluster,
  HotProductsGroup,
  HotProductsListResponse
} from '@/features/parser-products/hotProductsRecommendations.types';
import { getParserProductFilterOptions } from '@/features/parser-products/parserProducts.api';
import type { ParserProductFilterOptions, ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import Input from '@/shared/ui/Input.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

type OpportunityFilters = {
  search: string;
  sourceSubcategory: string;
  groupKey: string;
};

type OpportunityFilterKey = keyof OpportunityFilters;

const groupPriority = [
  'high_position_weak_reviews',
  'bad_recent_reviews',
  'repeated_review_complaint',
  'weak_description',
  'weak_visible_description',
  'missing_key_specs',
  'expensive_without_advantage',
  'top_low_stock',
  'fast_position_growth',
  'duplicate_cards',
  'high_position_weak_card',
  'low_review_count_top_position',
  'good_reviews_weak_visibility'
];

const groupLabels: Record<string, string> = {
  high_position_weak_card: 'Слабая карточка',
  high_position_weak_reviews: 'Слабые отзывы',
  bad_recent_reviews: 'Плохие последние отзывы',
  repeated_review_complaint: 'Повторяющаяся жалоба',
  weak_description: 'Слабое описание',
  weak_visible_description: 'Слабое описание',
  missing_key_specs: 'Нет важных характеристик',
  expensive_without_advantage: 'Высокая цена',
  top_low_stock: 'Низкий остаток',
  fast_position_growth: 'Быстрый рост',
  duplicate_cards: 'Одинаковые карточки',
  low_review_count_top_position: 'Мало отзывов в топе',
  good_reviews_weak_visibility: 'Хорошие отзывы, слабая видимость'
};

const emptyFilterOptions: ParserProductFilterOptions = {
  categories: [],
  subcategories: [],
  brands: [],
  sellers: []
};

const response = ref<HotProductsListResponse | null>(null);
const filterOptions = ref<ParserProductFilterOptions>(emptyFilterOptions);
const selectedProduct = ref<ParserProductListItem | null>(null);
const selectedCluster = ref<HotProductsDuplicateCluster | null>(null);
const loading = ref(false);
const recalculating = ref(false);
const filterOptionsLoading = ref(false);
const error = ref('');
const filterError = ref('');
let loadVersion = 0;

const filters = reactive<OpportunityFilters>({
  search: '',
  sourceSubcategory: '',
  groupKey: ''
});

const groups = computed(() => response.value?.groups.filter((group) => group.totalCount > 0) ?? []);
const orderedGroups = computed(() =>
  [...groups.value].sort((left, right) => groupOrder(left.key) - groupOrder(right.key))
);
const selectedGroup = computed(() =>
  orderedGroups.value.find((group) => group.key === filters.groupKey)
  ?? orderedGroups.value[0]
  ?? null
);
const groupOptions = computed(() => orderedGroups.value.map((group) => group.title));
const selectedGroupTitle = computed({
  get: () => getGroupTitle(filters.groupKey),
  set: (value: string) => {
    filters.groupKey = orderedGroups.value.find((group) => group.title === value)?.key ?? '';
  }
});
const activeItems = computed(() => filterBySearch(selectedGroup.value?.items ?? []));
const activeClusters = computed(() =>
  selectedGroup.value?.key === 'duplicate_cards'
    ? selectedGroup.value.clusters
    : []
);
const fallbackItems = computed(() => {
  if (orderedGroups.value.length > 0) {
    return [];
  }

  return filterBySearch(response.value?.items ?? []);
});
const chips = computed(() => {
  const result: Array<{ key: OpportunityFilterKey; label: string; value: string }> = [];
  if (filters.search.trim()) {
    result.push({ key: 'search', label: 'Поиск', value: filters.search.trim() });
  }
  if (filters.sourceSubcategory.trim()) {
    result.push({ key: 'sourceSubcategory', label: 'Ниша', value: filters.sourceSubcategory.trim() });
  }
  if (filters.groupKey.trim()) {
    result.push({ key: 'groupKey', label: 'Подборка', value: getGroupTitle(filters.groupKey) });
  }
  return result;
});

watch(
  () => filters.sourceSubcategory,
  () => {
    void loadOpportunities();
  },
  { immediate: true }
);

watch(
  () => filters.groupKey,
  () => {
    if (!filters.groupKey) {
      selectedCluster.value = null;
    }
  }
);

void loadFilterOptions();

async function loadFilterOptions(): Promise<void> {
  filterOptionsLoading.value = true;
  filterError.value = '';

  try {
    filterOptions.value = await getParserProductFilterOptions({});
  } catch (err) {
    filterError.value = getProblemMessage(err, 'Не удалось загрузить варианты фильтров.');
  } finally {
    filterOptionsLoading.value = false;
  }
}

async function loadOpportunities(): Promise<void> {
  const version = ++loadVersion;
  loading.value = true;
  error.value = '';
  selectedCluster.value = null;

  try {
    const data = await getHotProductsRecommendations({
      page: 1,
      pageSize: 50,
      ...(filters.sourceSubcategory ? { sourceSubcategory: filters.sourceSubcategory } : {}),
      ...(filters.groupKey ? { groupKey: filters.groupKey } : {})
    });

    if (version !== loadVersion) {
      return;
    }

    response.value = data;
    if (filters.groupKey && !data.groups.some((group) => group.key === filters.groupKey)) {
      filters.groupKey = '';
    }
  } catch (err) {
    if (version !== loadVersion) {
      return;
    }

    response.value = null;
    error.value = getProblemMessage(err, 'Не удалось загрузить перспективные товары.');
  } finally {
    if (version === loadVersion) {
      loading.value = false;
    }
  }
}

async function recalculate(): Promise<void> {
  if (recalculating.value) {
    return;
  }

  recalculating.value = true;
  error.value = '';

  try {
    await recalculateHotProductsRecommendations({
      ...(filters.sourceSubcategory ? { sourceSubcategory: filters.sourceSubcategory } : {}),
      maxProducts: 100000,
      maxRecommendations: 200,
      minProductsForScoring: 5,
      forceRecalculate: true
    });
    await loadOpportunities();
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось обновить подборки.');
  } finally {
    recalculating.value = false;
  }
}

function applyFilters(): void {
  selectedCluster.value = null;
  void loadOpportunities();
}

function resetFilters(): void {
  filters.search = '';
  filters.sourceSubcategory = '';
  filters.groupKey = '';
  selectedCluster.value = null;
  void loadOpportunities();
}

function removeFilter(key: OpportunityFilterKey): void {
  filters[key] = '';
  selectedCluster.value = null;
  if (key !== 'search') {
    void loadOpportunities();
  }
}

function selectGroup(group: HotProductsGroup): void {
  filters.groupKey = group.key;
  selectedCluster.value = null;
}

function groupOrder(key: string): number {
  const index = groupPriority.indexOf(key);
  return index === -1 ? groupPriority.length : index;
}

function getGroupTitle(key: string): string {
  return orderedGroups.value.find((group) => group.key === key)?.title ?? groupLabels[key] ?? key;
}

function filterBySearch(items: HotProductRecommendationItem[]): HotProductRecommendationItem[] {
  const search = filters.search.trim().toLocaleLowerCase('ru-RU');
  if (!search) {
    return items;
  }

  return items.filter((item) => [
    item.productName,
    item.wbProductId,
    item.brandName,
    item.sellerName,
    item.sourceSubcategory
  ].some((value) => value?.toLocaleLowerCase('ru-RU').includes(search)));
}

function factorLabels(item: HotProductRecommendationItem, groupKey?: string): string[] {
  const factors = groupKey
    ? item.factors.filter((factor) => factor.code === groupKey)
    : item.factors;

  return factors.map((factor) => {
    if (factor.value === null || factor.value === undefined || factor.value === '') {
      return factor.label;
    }

    return `${factor.label}: ${factor.value}`;
  });
}

function factorTone(item: HotProductRecommendationItem, groupKey?: string): string {
  const factor = groupKey
    ? item.factors.find((entry) => entry.code === groupKey)
    : item.factors[0];
  return factor?.direction === 'positive'
    ? 'positive'
    : factor?.direction === 'negative'
      ? 'negative'
      : 'neutral';
}

function formatMoney(value: number | null): string {
  return value === null
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)} ₽`;
}

function formatNumber(value: number | null): string {
  return value === null
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU').format(value);
}

function openProduct(item: HotProductRecommendationItem): void {
  if (!item.parserProductRowId) {
    return;
  }

  selectedProduct.value = toParserProduct(item);
}

function visibleClusterItems(cluster: HotProductsDuplicateCluster): HotProductRecommendationItem[] {
  return cluster.items.slice(0, 5);
}

function toParserProduct(item: HotProductRecommendationItem): ParserProductListItem {
  return {
    id: item.parserProductRowId!,
    parserRunId: '',
    parsedAtUtc: response.value?.run?.computedAtUtc ?? new Date().toISOString(),
    wbProductId: item.wbProductId ?? '',
    wbRootId: item.wbRootId,
    name: item.productName,
    brandName: item.brandName,
    sellerName: item.sellerName,
    priceRegular: item.priceWithoutDiscount,
    priceDiscounted: item.price,
    priceWbWallet: item.walletPrice,
    discountPercent: null,
    totalQuantity: item.totalQuantity,
    ratingRounded: item.rating ? Math.round(item.rating) : null,
    reviewRating: item.rating,
    feedbackCount: item.feedbackCount,
    sourceCategory: item.sourceCategory,
    sourceSubcategory: item.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: item.thumbnailUrl,
    rank: item.position
      ? {
          absolutePosition: item.position,
          page: 1,
          positionOnPage: item.position,
          query: item.sourceSubcategory ?? '',
          sourceCategory: item.sourceCategory,
          sourceSubcategory: item.sourceSubcategory,
          sourceRegionDest: null,
          sort: null,
          observedAtUtc: response.value?.run?.computedAtUtc ?? new Date().toISOString(),
          parserRunId: '',
          rankContextId: '',
          contextsCount: 1
        }
      : null,
    position: {
      state: item.position ? 'observed' : 'unknown',
      absolutePosition: item.position,
      observedRangeLimit: item.observedRangeLimit,
      query: item.sourceSubcategory,
      sourceCategory: item.sourceCategory,
      sourceSubcategory: item.sourceSubcategory,
      observedAtUtc: response.value?.run?.computedAtUtc ?? null
    },
    parsedReviewEvidence: undefined,
    logistics: null
  };
}
</script>

<template>
  <div class="market-opportunities">
    <PageHeader
      title="Перспективные товары"
      description="Подборки показывают практические ситуации для проверки: слабые отзывы, слабое описание, высокая цена, низкий остаток и одинаковые карточки."
    >
      <Button variant="primary" :loading="recalculating" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить
      </Button>
    </PageHeader>

    <form class="filters app-surface" @submit.prevent="applyFilters">
      <div class="filters__search">
        <Input
          v-model="filters.search"
          class="filter-control"
          :class="{ 'filter-control--active': Boolean(filters.search.trim()) }"
          label="Поиск"
          placeholder="Название, WB id, бренд или продавец"
        />
        <div class="filters__actions">
          <Button class="filters__apply" type="submit">Применить</Button>
          <Button v-if="chips.length" type="button" variant="ghost" @click="resetFilters">Сбросить</Button>
        </div>
      </div>

      <div class="filters__selects">
        <MarketFilterSelect
          v-model="filters.sourceSubcategory"
          class="filter-control"
          :class="{ 'filter-control--active': Boolean(filters.sourceSubcategory) }"
          label="Ниша"
          placeholder="Все ниши"
          search-placeholder="Найти нишу"
          :options="filterOptions.subcategories"
        />
        <MarketFilterSelect
          v-model="selectedGroupTitle"
          class="filter-control"
          :class="{ 'filter-control--active': Boolean(filters.groupKey) }"
          label="Подборка"
          placeholder="Все идеи"
          search-placeholder="Найти подборку"
          :options="groupOptions"
        />
      </div>

      <p v-if="filterError" class="filters__notice">{{ filterError }}</p>
      <p v-else-if="filterOptionsLoading" class="filters__notice">Загружаем варианты фильтров...</p>

      <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры перспективных товаров">
        <button
          v-for="chip in chips"
          :key="chip.key"
          class="filters__chip"
          type="button"
          :title="`Убрать ${chip.label}`"
          @click="removeFilter(chip.key)"
        >
          <span>{{ chip.label }}</span>
          <strong>{{ chip.value }}</strong>
          <span aria-hidden="true">x</span>
        </button>
      </div>
    </form>

    <LoadingState v-if="loading" class="app-operator-panel" />
    <EmptyState
      v-else-if="error"
      class="app-operator-panel"
      title="Не удалось загрузить подборки"
      :description="error"
    />
    <EmptyState
      v-else-if="!response?.run"
      class="app-operator-panel"
      title="Подборки еще не рассчитаны"
      description="Запустите обновление, чтобы увидеть практические идеи по выбранным нишам."
    >
      <Button variant="primary" :loading="recalculating" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить
      </Button>
    </EmptyState>

    <template v-else>
      <section v-if="orderedGroups.length" class="opportunities-groups app-operator-panel">
        <div class="opportunities-tabs" role="tablist" aria-label="Подборки перспективных товаров">
          <button
            v-for="group in orderedGroups"
            :key="group.key"
            class="opportunities-tab"
            :class="{ 'opportunities-tab--active': selectedGroup?.key === group.key }"
            type="button"
            @click="selectGroup(group)"
          >
            {{ group.title }}
            <span>{{ group.totalCount }}</span>
          </button>
        </div>

        <div v-if="selectedGroup" class="opportunities-group">
          <header class="opportunities-group__header">
            <div>
              <h2>{{ selectedGroup.title }}</h2>
              <p>{{ selectedGroup.description }}</p>
            </div>
            <span>{{ activeItems.length }} товаров</span>
          </header>

          <div v-if="activeClusters.length" class="duplicate-clusters">
            <article
              v-for="cluster in activeClusters"
              :key="cluster.key"
              class="duplicate-cluster app-operator-card"
            >
              <header>
                <div>
                  <h3>{{ cluster.title }}</h3>
                  <p>{{ cluster.totalCount }} похожих карточек</p>
                </div>
                <button class="app-operator-link" type="button" @click="selectedCluster = cluster">
                  Посмотреть карточки кластера
                </button>
              </header>
              <div class="duplicate-cluster__items">
                <button
                  v-for="item in visibleClusterItems(cluster)"
                  :key="item.id"
                  class="duplicate-cluster__item"
                  type="button"
                  @click="openProduct(item)"
                >
                  <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
                  <span>{{ item.productName }}</span>
                </button>
              </div>
            </article>
          </div>

          <div v-if="activeItems.length" class="opportunities-grid">
            <article
              v-for="item in activeItems"
              :key="item.id"
              class="opportunity-card app-operator-card app-operator-card--interactive"
              :class="{ 'opportunity-card--disabled': !item.parserProductRowId }"
              @click="openProduct(item)"
            >
              <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
              <div class="opportunity-card__body">
                <span>{{ item.sourceSubcategory || 'Ниша не указана' }}</span>
                <h3>{{ item.productName }}</h3>
                <p>{{ item.brandName || 'Бренд не указан' }} · {{ item.sellerName || 'Продавец не указан' }}</p>
                <strong>{{ item.reason }}</strong>

                <div class="opportunity-tags">
                  <span
                    v-for="label in factorLabels(item, selectedGroup.key)"
                    :key="label"
                    class="opportunity-tag"
                    :class="`opportunity-tag--${factorTone(item, selectedGroup.key)}`"
                  >
                    {{ label }}
                  </span>
                </div>

                <button
                  v-if="item.parserProductRowId"
                  class="app-operator-link"
                  type="button"
                  @click.stop="openProduct(item)"
                >
                  Карточка
                </button>
              </div>
            </article>
          </div>
          <EmptyState
            v-else
            title="Поиск не дал результатов"
            description="Измените поисковую строку или выберите другую подборку."
          />
        </div>
      </section>

      <section v-else class="opportunities-groups app-operator-panel">
        <EmptyState
          title="Обновите анализ, чтобы увидеть подборки"
          description="Текущий сохраненный анализ не содержит новых эвристических групп."
        >
          <Button variant="primary" :loading="recalculating" @click="recalculate">
            <RefreshCw :size="16" />
            Обновить
          </Button>
        </EmptyState>

        <div v-if="fallbackItems.length" class="opportunities-fallback">
          <h2>
            <SlidersHorizontal :size="17" />
            Все найденные товары
          </h2>
          <div class="opportunities-grid">
            <article
              v-for="item in fallbackItems"
              :key="item.id"
              class="opportunity-card app-operator-card app-operator-card--interactive"
              :class="{ 'opportunity-card--disabled': !item.parserProductRowId }"
              @click="openProduct(item)"
            >
              <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
              <div class="opportunity-card__body">
                <span>{{ item.sourceSubcategory || 'Ниша не указана' }}</span>
                <h3>{{ item.productName }}</h3>
                <div class="opportunity-tags">
                  <span
                    v-for="label in factorLabels(item)"
                    :key="label"
                    class="opportunity-tag opportunity-tag--neutral"
                  >
                    {{ label }}
                  </span>
                </div>
              </div>
            </article>
          </div>
        </div>
      </section>
    </template>

    <div v-if="selectedCluster" class="cluster-modal" role="dialog" aria-modal="true">
      <div class="cluster-modal__backdrop" @click="selectedCluster = null" />
      <section class="cluster-modal__panel app-operator-panel">
        <header>
          <div>
            <h2>{{ selectedCluster.title }}</h2>
            <p>{{ selectedCluster.totalCount }} похожих карточек в кластере</p>
          </div>
          <button class="app-operator-link" type="button" @click="selectedCluster = null">Закрыть</button>
        </header>
        <div class="opportunities-grid">
          <article
            v-for="item in selectedCluster.items"
            :key="item.id"
            class="opportunity-card app-operator-card app-operator-card--interactive"
            :class="{ 'opportunity-card--disabled': !item.parserProductRowId }"
            @click="openProduct(item)"
          >
            <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
            <div class="opportunity-card__body">
              <span>{{ item.sourceSubcategory || 'Ниша не указана' }}</span>
              <h3>{{ item.productName }}</h3>
              <p>{{ item.brandName || 'Бренд не указан' }} · {{ item.sellerName || 'Продавец не указан' }}</p>
            </div>
          </article>
        </div>
      </section>
    </div>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
  </div>
</template>

<style scoped>
.market-opportunities {
  display: grid;
  gap: var(--space-4);
}

.filters {
  position: relative;
  z-index: 3;
  display: grid;
  gap: var(--space-3);
  border-color: var(--color-border-strong);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.035), transparent 42%),
    var(--surface-panel);
  overflow: visible;
  padding: var(--space-3);
}

.filters__search,
.filters__selects {
  display: grid;
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

.filters__apply {
  min-width: 7.5rem;
}

.filters__notice {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
}

.filters__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filters__chip {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  border: 1px solid var(--accent-primary-border);
  border-radius: 999px;
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-meta-size);
  font-weight: 760;
  padding: 0.3rem 0.6rem;
}

.opportunities-groups {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.opportunities-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.opportunities-tab {
  display: inline-flex;
  min-height: 2.25rem;
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
}

.opportunities-tab:hover,
.opportunities-tab--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.opportunities-tab span {
  min-width: 1.4rem;
  border-radius: 999px;
  background: var(--operator-metric-bg);
  padding: 0.12rem 0.45rem;
  text-align: center;
}

.opportunities-group,
.opportunities-fallback,
.duplicate-clusters {
  display: grid;
  gap: var(--space-4);
}

.opportunities-group__header,
.duplicate-cluster header,
.cluster-modal__panel > header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
}

.opportunities-group__header h2,
.opportunities-fallback h2,
.duplicate-cluster h3,
.cluster-modal__panel h2,
.opportunity-card h3 {
  margin: 0;
  color: var(--color-text);
}

.opportunities-group__header p,
.duplicate-cluster p,
.cluster-modal__panel p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
}

.opportunities-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}

.opportunity-card {
  display: grid;
  grid-template-columns: 5rem minmax(0, 1fr);
  gap: var(--space-3);
  padding: var(--space-3);
}

.opportunity-card--disabled {
  cursor: default;
}

.opportunity-card__body {
  display: grid;
  align-content: start;
  gap: var(--space-2);
  min-width: 0;
}

.opportunity-card__body > span,
.opportunity-card__body p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.opportunity-card__body > strong {
  color: var(--color-text);
  font-size: var(--operator-body-size);
  font-weight: 650;
}

.opportunity-card h3 {
  font-size: var(--operator-title-size);
  line-height: 1.25;
}

.opportunity-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: var(--space-2);
}

.opportunity-tag {
  display: inline-flex;
  width: fit-content;
  max-width: 100%;
  border: 1px solid var(--operator-border-muted);
  border-radius: 999px;
  background: var(--operator-metric-bg);
  color: var(--color-text);
  font-size: var(--operator-meta-size);
  font-weight: 760;
  line-height: 1.25;
  padding: 0.32rem 0.65rem;
}

.opportunity-tag--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.opportunity-tag--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger-text);
}

.opportunity-tag--neutral {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.duplicate-cluster {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-3);
}

.duplicate-cluster__items {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--space-2);
}

.duplicate-cluster__item {
  display: grid;
  grid-template-columns: 3.25rem minmax(0, 1fr);
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-metric-bg);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-meta-size);
  font-weight: 700;
  padding: var(--space-2);
  text-align: left;
}

.duplicate-cluster__item span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cluster-modal {
  position: fixed;
  z-index: 70;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--space-5);
}

.cluster-modal__backdrop {
  position: absolute;
  inset: 0;
  background: rgb(15 23 42 / 0.54);
}

.cluster-modal__panel {
  position: relative;
  z-index: 1;
  display: grid;
  width: min(78rem, 100%);
  max-height: min(48rem, 86vh);
  gap: var(--space-4);
  overflow: auto;
  padding: var(--space-4);
}

@media (min-width: 880px) {
  .filters__search {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__selects {
    grid-template-columns: repeat(2, minmax(16rem, 1fr));
  }
}

@media (max-width: 980px) {
  .opportunities-grid,
  .duplicate-cluster__items {
    grid-template-columns: 1fr;
  }

  .opportunities-group__header,
  .duplicate-cluster header,
  .cluster-modal__panel > header {
    display: grid;
  }
}
</style>
