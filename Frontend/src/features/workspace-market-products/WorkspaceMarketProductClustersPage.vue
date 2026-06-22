<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { ArrowLeft, Eye, RefreshCw } from 'lucide-vue-next';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import {
  getWorkspaceOverview,
  recalculateWorkspaceOverview
} from '@/features/overview/workspaceOverview.api';
import {
  formatNumber,
  formatMoney,
  productMetrics,
  similarGroupItemTags,
  similarGroupTabs,
  similarGroupTitle,
  tagClass,
  toParserProduct,
  toParserSimilarProduct
} from '@/features/overview/workspaceOverview.helpers';
import type {
  WorkspaceOverview,
  WorkspaceOverviewProduct,
  WorkspaceOverviewSimilarProduct,
  WorkspaceOverviewSignal,
  WorkspaceOverviewSimilarProductGroup
} from '@/features/overview/workspaceOverview.types';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import Input from '@/shared/ui/Input.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import DemoMarketProductDrawer from './DemoMarketProductDrawer.vue';
import { useActiveWorkspace } from './useActiveWorkspace';
import { getWorkspaceMarketProducts } from './workspaceMarketProducts.api';
import type { WorkspaceMarketProductListItem } from './workspaceMarketProducts.types';

type TagFilter = 'all' | 'competitor' | 'idea' | 'created';

const ALL_SIMILAR_GROUPS_KEY = '__all';

const auth = useAuthStore();
const workspace = useActiveWorkspace();
const overview = ref<WorkspaceOverview | null>(null);
const fallbackProducts = ref<WorkspaceOverviewProduct[]>([]);
const selectedProduct = ref<ParserProductListItem | null>(null);
const selectedDemoProduct = ref<WorkspaceOverviewProduct | null>(null);
const loading = ref(false);
const recalculating = ref(false);
const error = ref('');
const loadStage = ref('idle');
const loadAttempts = ref(0);
const loadStartedAt = ref('');
const filters = reactive({
  search: '',
  tagKey: 'all' as TagFilter,
  groupKey: ''
});
const selectedGroupByProductId = reactive<Record<string, string>>({});
let requestVersion = 0;

const activeWorkspaceId = computed(() => workspace.activeWorkspaceId.value);
const isGuest = computed(() => !auth.isAuthenticated);
const hasProducts = computed(() => allProducts.value.length > 0);
const hasAnalysis = computed(() => Boolean(overview.value?.lastAnalysis) || fallbackProducts.value.length > 0);
const analysisWarnings = computed(() => overview.value?.lastAnalysis?.warnings?.slice(0, 3) ?? []);
const allProducts = computed<WorkspaceOverviewProduct[]>(() => {
  if (!overview.value) {
    return fallbackProducts.value;
  }

  return [
    ...(overview.value.competitors?.products ?? []),
    ...(overview.value.ideas?.products ?? []),
    ...(overview.value.created?.products ?? [])
  ];
});

const groupOptions = computed(() => {
  const groups = new Map<string, { key: string; label: string; count: number }>();

  for (const product of allProducts.value) {
    for (const group of similarGroupTabs(product)) {
      const existing = groups.get(group.key);
      groups.set(group.key, {
        key: group.key,
        label: similarGroupTitle(group),
        count: (existing?.count ?? 0) + group.items.length
      });
    }
  }

  return [...groups.values()].sort((left, right) => groupPriority(left.key) - groupPriority(right.key));
});

const filteredProducts = computed(() => {
  const search = filters.search.trim().toLocaleLowerCase('ru-RU');
  return allProducts.value.filter((product) => {
    if (filters.tagKey !== 'all' && product.tagKey !== filters.tagKey) {
      return false;
    }

    if (filters.groupKey && !similarGroupTabs(product).some((group) => group.key === filters.groupKey)) {
      return false;
    }

    if (!search) {
      return true;
    }

    return [
      product.name,
      product.brandName,
      product.sellerName,
      product.sourceCategory,
      product.sourceSubcategory,
      product.wbProductId,
      product.note
    ]
      .filter(Boolean)
      .some((value) => String(value).toLocaleLowerCase('ru-RU').includes(search));
  });
});

watch(
  activeWorkspaceId,
  () => {
    void loadClusterOverview();
  },
  { immediate: true }
);

async function loadClusterOverview(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const version = ++requestVersion;
  loadAttempts.value += 1;
  loadStartedAt.value = new Date().toISOString();

  if (isGuest.value) {
    overview.value = null;
    fallbackProducts.value = [];
    error.value = '';
    loading.value = false;
    loadStage.value = 'auth-required';
    return;
  }

  if (!workspaceId) {
    overview.value = null;
    fallbackProducts.value = [];
    error.value = '';
    loading.value = false;
    loadStage.value = 'no-workspace';
    return;
  }

  loading.value = true;
  error.value = '';
  loadStage.value = 'loading-products';

  try {
    const products = await getWorkspaceMarketProducts(workspaceId, {
      page: 1,
      pageSize: 500,
      sort: 'updated_desc'
    });

    if (version !== requestVersion) {
      return;
    }

    fallbackProducts.value = products.items.map(mapWorkspaceProductToOverviewProduct);
    loading.value = false;
    loadStage.value = 'loading-overview';

    const response = await getWorkspaceOverview(workspaceId);
    if (version !== requestVersion) {
      return;
    }

    overview.value = response;
    fallbackProducts.value = [];
    loadStage.value = 'loaded';
    if (filters.groupKey && !responseHasGroup(response, filters.groupKey)) {
      filters.groupKey = '';
    }
  } catch (requestError) {
    if (version !== requestVersion) {
      return;
    }

    loadStage.value = 'failed';
    const message = getProblemMessage(requestError, 'Не удалось загрузить кластерный анализ.');
    error.value = fallbackProducts.value.length > 0
      ? `${message} Показан список наблюдаемых товаров без свежих подборок.`
      : message;
  } finally {
    if (version === requestVersion) {
      loading.value = false;
    }
  }
}

async function loadOverview(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const version = ++requestVersion;
  loadAttempts.value += 1;
  loadStartedAt.value = new Date().toISOString();

  if (isGuest.value) {
    overview.value = null;
    fallbackProducts.value = [];
    error.value = '';
    loading.value = false;
    loadStage.value = 'auth-required';
    return;
  }

  if (!workspaceId) {
    overview.value = null;
    fallbackProducts.value = [];
    error.value = '';
    loading.value = false;
    loadStage.value = 'no-workspace';
    return;
  }

  loading.value = true;
  error.value = '';
  loadStage.value = 'loading-products';

  const watchdog = window.setTimeout(() => {
    if (version === requestVersion && loading.value) {
      loading.value = false;
      loadStage.value = 'timeout';
      error.value = 'Не удалось загрузить кластерный анализ: запрос занял слишком много времени.';
    }
  }, 15000);

  try {
    const response = await getWorkspaceOverview(workspaceId);
    if (version === requestVersion) {
      overview.value = response;
      loadStage.value = 'loaded';
      if (filters.groupKey && !responseHasGroup(response, filters.groupKey)) {
        filters.groupKey = '';
      }
    }
  } catch (requestError) {
    if (version === requestVersion) {
      loadStage.value = 'failed';
      error.value = getProblemMessage(requestError, 'Не удалось загрузить кластерный анализ.');
      overview.value = null;
    }
  } finally {
    window.clearTimeout(watchdog);
    if (version === requestVersion) {
      loading.value = false;
    }
  }
}

function mapWorkspaceProductToOverviewProduct(item: WorkspaceMarketProductListItem): WorkspaceOverviewProduct {
  const currentPrice = item.priceWbWallet ?? item.priceDiscounted ?? item.priceRegular;
  const tagKey = item.isDemo ? 'created' : item.tagKey;

  return {
    id: item.id,
    parserProductRowId: item.parserProductRowId,
    wbProductId: item.wbProductId,
    wbRootId: item.wbRootId,
    sourceType: item.sourceType,
    isDemo: item.isDemo,
    tagKey,
    note: item.note,
    name: item.name,
    brandName: item.brandName,
    sellerName: item.sellerName,
    thumbnailUrl: item.thumbnailUrl,
    sourceCategory: item.sourceCategory,
    sourceSubcategory: item.sourceSubcategory,
    currentPrice,
    costPrice: item.costPrice,
    description: item.description,
    supplierName: item.supplierName,
    supplierUrl: item.supplierUrl,
    currentPosition: item.positionAbsolute,
    currentStock: item.totalQuantity,
    currentFeedbackCount: item.feedbackCount,
    currentReviewRating: item.reviewRating,
    latestObservedAtUtc: item.latestObservedAtUtc,
    priceChange: emptyChange('price', 'Цена', currentPrice, formatMoney(currentPrice)),
    positionChange: emptyChange(
      'position',
      'Позиция',
      item.positionAbsolute,
      item.positionAbsolute ? `#${formatNumber(item.positionAbsolute)}` : 'Нет данных'
    ),
    stockChange: emptyChange('stock', 'Остаток', item.totalQuantity, formatNumber(item.totalQuantity)),
    feedbackChange: emptyChange('feedback', 'Отзывы', item.feedbackCount, formatNumber(item.feedbackCount)),
    reviewRatingChange: emptyChange('rating', 'Оценка', item.reviewRating, formatNumber(item.reviewRating)),
    signals: [],
    similarProducts: [],
    similarProductGroups: []
  };
}

function emptyChange(
  key: string,
  label: string,
  currentValue: number | null,
  displayValue: string
): WorkspaceOverviewProduct['priceChange'] {
  return {
    key,
    label,
    currentValue,
    previousValue: null,
    delta: null,
    displayValue,
    state: 'unknown'
  };
}

async function recalculate(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  if (!workspaceId || recalculating.value || isGuest.value) {
    return;
  }

  recalculating.value = true;
  error.value = '';

  try {
    await recalculateWorkspaceOverview(workspaceId);
    await loadClusterOverview();
  } catch (requestError) {
    const message = getProblemMessage(requestError, 'Не удалось обновить анализ.');
    error.value = overview.value
      ? `${message} Показана последняя успешная версия.`
      : message;
  } finally {
    recalculating.value = false;
  }
}

function productGroups(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroup[] {
  const groups = similarGroupTabs(product);
  if (!filters.groupKey) {
    const selectedKey = selectedSimilarGroupKey(product, groups);
    if (selectedKey === ALL_SIMILAR_GROUPS_KEY) {
      return groups;
    }

    return groups.filter((group) => group.key === selectedKey);
  }

  return groups.filter((group) => group.key === filters.groupKey);
}

function selectedSimilarGroupKey(
  product: WorkspaceOverviewProduct,
  groups = similarGroupTabs(product)
): string {
  if (groups.length === 0) {
    return '';
  }

  const selected = selectedGroupByProductId[product.id];
  if (selected === ALL_SIMILAR_GROUPS_KEY || (selected && groups.some((group) => group.key === selected))) {
    return selected;
  }

  const preferred = product.isDemo
    ? groups.find((group) => group.key === 'strong_similar_cards')
    : undefined;
  const selectedKey = preferred?.key
    ?? [...groups].sort((left, right) => groupPriority(left.key) - groupPriority(right.key))[0].key;
  selectedGroupByProductId[product.id] = selectedKey;
  return selectedKey;
}

function selectSimilarGroup(product: WorkspaceOverviewProduct, groupKey: string): void {
  selectedGroupByProductId[product.id] = groupKey;
}

function isPriceCorridorSignal(signal: WorkspaceOverviewSignal): boolean {
  return signal.code === 'demo_price_corridor' && Boolean(signal.value);
}

function isDescriptionSignal(signal: WorkspaceOverviewSignal): boolean {
  return signal.code === 'demo_description_recommendation';
}

function signalValue(signal: WorkspaceOverviewSignal): Record<string, unknown> {
  return signal.value ?? {};
}

function numberValue(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function stringArrayValue(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

function priceCorridorRows(signal: WorkspaceOverviewSignal): Array<{ label: string; value: string; state?: string }> {
  const value = signalValue(signal);
  const currentPrice = numberValue(value.currentPrice);
  const lowerPrice = numberValue(value.lowerPrice);
  const upperPrice = numberValue(value.upperPrice);
  const currentState = currentPrice !== null && lowerPrice !== null && upperPrice !== null
    ? currentPrice < lowerPrice || currentPrice > upperPrice
      ? 'warning'
      : 'ok'
    : undefined;

  return [
    { label: 'Нижняя граница', value: formatMoney(numberValue(value.lowerPrice)) },
    { label: 'Целевая цена', value: formatMoney(numberValue(value.targetPrice)) },
    { label: 'Верхняя граница', value: formatMoney(numberValue(value.upperPrice)) },
    { label: 'Цена карточки', value: formatMoney(currentPrice), state: currentState },
    { label: 'Медиана похожих', value: formatMoney(numberValue(value.peerMedianPrice)) },
    { label: 'Похожие в расчете', value: formatNumber(numberValue(value.sampleSize)) },
    { label: 'Минимум с учетом себестоимости', value: formatMoney(numberValue(value.costFloor)) }
  ];
}

function descriptionProblems(signal: WorkspaceOverviewSignal): string[] {
  return stringArrayValue(signalValue(signal).problems);
}

function descriptionRequiredFields(signal: WorkspaceOverviewSignal): string[] {
  return stringArrayValue(signalValue(signal).requiredFields);
}

function descriptionSuggestion(signal: WorkspaceOverviewSignal): string | null {
  const suggestion = signalValue(signal).suggestedDescription;
  return typeof suggestion === 'string' && suggestion.trim() ? suggestion : null;
}

function openProduct(product: WorkspaceOverviewProduct): void {
  if (product.isDemo) {
    selectedDemoProduct.value = product;
    return;
  }

  selectedProduct.value = toParserProduct(product);
}

function openSimilarProduct(product: WorkspaceOverviewSimilarProduct): void {
  selectedProduct.value = toParserSimilarProduct(product);
}

function tagLabel(tagKey: WorkspaceOverviewProduct['tagKey']): string {
  if (tagKey === 'created') {
    return 'Созданная';
  }

  return tagKey === 'competitor' ? 'Конкурент' : 'Идея';
}

function tagTone(tagKey: WorkspaceOverviewProduct['tagKey']): 'warning' | 'info' {
  return tagKey === 'competitor' ? 'warning' : 'info';
}

function productTagLabel(product: WorkspaceOverviewProduct): string {
  if (product.isDemo || product.tagKey === 'created') {
    return 'Созданная';
  }

  return tagLabel(product.tagKey);
}

function responseHasGroup(response: WorkspaceOverview, groupKey: string): boolean {
  return [
    ...(response.competitors?.products ?? []),
    ...(response.ideas?.products ?? []),
    ...(response.created?.products ?? [])
  ]
    .some((product) => similarGroupTabs(product).some((group) => group.key === groupKey));
}

function groupPriority(key: string): number {
  const priority = [
    'duplicate_cards',
    'strong_similar_cards',
    'price_disadvantage',
    'position_disadvantage',
    'review_count_disadvantage',
    'rating_disadvantage',
    'stock_disadvantage',
    'similar_faster_region_delivery',
    'weak_competitor_cards'
  ];
  const index = priority.indexOf(key);
  return index === -1 ? priority.length : index;
}
</script>

<template>
  <div
    class="cluster-page"
    :data-debug-loading="String(loading)"
    :data-debug-workspace-id="activeWorkspaceId ?? ''"
    :data-debug-has-overview="String(Boolean(overview))"
    :data-debug-error="error"
    :data-debug-load-stage="loadStage"
    :data-debug-load-attempts="String(loadAttempts)"
    :data-debug-load-started-at="loadStartedAt"
  >
    <PageHeader
      title="Кластерный анализ"
      description="Сравнение наблюдаемых товаров с похожими карточками в выбранной нише."
    >
      <RouterLink class="cluster-page__back app-operator-link" to="/workspace/market-products">
        <ArrowLeft :size="15" />
        Наблюдаемые товары
      </RouterLink>
      <Button variant="primary" :loading="recalculating" :disabled="!activeWorkspaceId" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить анализ
      </Button>
    </PageHeader>

    <AuthRequiredState
      v-if="isGuest"
      description="Кластерный анализ сравнивает ваши наблюдаемые карточки с похожими товарами и показывает сильные и слабые примеры. Войдите, чтобы открыть анализ своей рабочей области."
    />
    <template v-else>
    <p v-if="error" class="cluster-error">{{ error }}</p>
    <div v-if="analysisWarnings.length" class="cluster-warning app-surface">
      <strong>РџСЂРµРґСѓРїСЂРµР¶РґРµРЅРёСЏ Р°РЅР°Р»РёР·Р°</strong>
      <ul>
        <li v-for="warning in analysisWarnings" :key="warning">{{ warning }}</li>
      </ul>
    </div>

    <LoadingState v-if="loading" :rows="6" />

    <EmptyState
      v-else-if="!activeWorkspaceId"
      title="Вне рабочей области"
      description="Создайте или выберите рабочую область, чтобы открыть кластерный анализ."
    >
      <RouterLink class="app-operator-link" to="/management/workspaces">Рабочие области</RouterLink>
    </EmptyState>

    <EmptyState
      v-else-if="!hasProducts"
      title="В рабочей области пока нет товаров"
      description="Добавьте рыночные карточки в наблюдаемые, чтобы сравнивать их с похожими товарами."
    >
      <RouterLink class="app-operator-link" to="/market/intelligence">
        Перейти в маркетинговую разведку
      </RouterLink>
    </EmptyState>

    <EmptyState
      v-else-if="!hasAnalysis"
      title="Анализ еще не рассчитан"
      description="Запустите обновление, чтобы увидеть похожие карточки и сравнения по наблюдаемым товарам."
    >
      <Button variant="primary" :loading="recalculating" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить анализ
      </Button>
    </EmptyState>

    <template v-else>
      <section class="cluster-filters app-surface">
        <Input
          v-model="filters.search"
          label="Поиск"
          placeholder="Название, WB id, бренд, продавец или заметка"
        />

        <label class="cluster-field app-select-field">
          <span>Тег</span>
          <select v-model="filters.tagKey" class="app-select">
            <option value="all">Все</option>
            <option value="competitor">Конкуренты</option>
            <option value="idea">Идеи</option>
            <option value="created">Созданные</option>
          </select>
        </label>

        <label class="cluster-field app-select-field">
          <span>Подборка сравнения</span>
          <select v-model="filters.groupKey" class="app-select">
            <option value="">Все подборки</option>
            <option v-for="group in groupOptions" :key="group.key" :value="group.key">
              {{ group.label }} · {{ group.count }}
            </option>
          </select>
        </label>
      </section>

      <section class="cluster-summary">
        <span>{{ filteredProducts.length }} из {{ allProducts.length }} товаров</span>
        <span>{{ groupOptions.length }} подборок</span>
      </section>

      <EmptyState
        v-if="filteredProducts.length === 0"
        title="Поиск не дал результатов"
        description="Измените поиск, тег или подборку сравнения."
      />

      <section v-else class="cluster-list">
        <article
          v-for="product in filteredProducts"
          :key="product.id"
          class="cluster-card app-operator-card"
        >
          <header class="cluster-card__head">
            <button class="cluster-card__image" type="button" @click="openProduct(product)">
              <MarketProductImage :src="product.thumbnailUrl" :alt="product.name" />
            </button>

            <div class="cluster-card__identity">
              <span>{{ product.sourceSubcategory || product.sourceCategory || 'Ниша не указана' }}</span>
              <h2>{{ product.name }}</h2>
              <p>{{ product.brandName || 'Бренд не указан' }} · {{ product.sellerName || 'Продавец не указан' }}</p>
              <p
                class="cluster-card__note"
                :class="{ 'cluster-card__note--empty': !product.note }"
              >
                {{ product.note || 'Заметка не добавлена' }}
              </p>
            </div>

            <Badge :tone="tagTone(product.tagKey)">{{ productTagLabel(product) }}</Badge>
          </header>

          <div class="cluster-card__metrics">
            <div v-for="metric in productMetrics(product)" :key="metric.label" class="app-operator-metric cluster-metric">
              <span>{{ metric.label }}</span>
              <strong>{{ metric.value }}</strong>
            </div>
          </div>

          <section v-if="product.isDemo && product.signals.length" class="cluster-demo-signals">
            <article v-for="signal in product.signals" :key="signal.code" class="cluster-demo-signal">
              <strong>{{ signal.title }}</strong>
              <p>{{ signal.description }}</p>

              <table v-if="isPriceCorridorSignal(signal)" class="cluster-price-corridor">
                <tbody>
                  <tr v-for="row in priceCorridorRows(signal)" :key="row.label" :class="row.state ? `cluster-price-corridor__row--${row.state}` : ''">
                    <th>{{ row.label }}</th>
                    <td>{{ row.value }}</td>
                  </tr>
                </tbody>
              </table>

              <div v-else-if="isDescriptionSignal(signal)" class="cluster-description-advice">
                <div v-if="descriptionProblems(signal).length">
                  <span>Проблемы</span>
                  <ul>
                    <li v-for="problem in descriptionProblems(signal)" :key="problem">{{ problem }}</li>
                  </ul>
                </div>
                <div v-if="descriptionRequiredFields(signal).length">
                  <span>Что заполнить</span>
                  <ul>
                    <li v-for="field in descriptionRequiredFields(signal)" :key="field">{{ field }}</li>
                  </ul>
                </div>
                <div v-if="descriptionSuggestion(signal)" class="cluster-description-advice__suggestion">
                  <span>Вариант текста</span>
                  <p>{{ descriptionSuggestion(signal) }}</p>
                </div>
              </div>

              <ul v-else>
                <li v-for="fact in signal.metricFacts" :key="fact">{{ fact }}</li>
              </ul>
            </article>
          </section>

          <div v-if="!filters.groupKey && similarGroupTabs(product).length > 1" class="cluster-group-switcher" aria-label="Подборки сравнения">
            <button
              class="cluster-group-switcher__button"
              :class="{ 'cluster-group-switcher__button--active': selectedSimilarGroupKey(product) === ALL_SIMILAR_GROUPS_KEY }"
              type="button"
              @click="selectSimilarGroup(product, ALL_SIMILAR_GROUPS_KEY)"
            >
              Все
              <span>{{ similarGroupTabs(product).length }}</span>
            </button>
            <button
              v-for="group in similarGroupTabs(product)"
              :key="group.key"
              class="cluster-group-switcher__button"
              :class="{ 'cluster-group-switcher__button--active': selectedSimilarGroupKey(product) === group.key }"
              type="button"
              @click="selectSimilarGroup(product, group.key)"
            >
              {{ similarGroupTitle(group) }}
              <span>{{ group.items.length }}</span>
            </button>
          </div>

          <div v-if="productGroups(product).length" class="cluster-groups">
            <section
              v-for="group in productGroups(product)"
              :key="group.key"
              class="cluster-group"
            >
              <header>
                <div>
                  <h3>{{ similarGroupTitle(group) }}</h3>
                  <p>{{ group.description }}</p>
                </div>
                <span>{{ group.items.length }} товаров</span>
              </header>

              <div class="cluster-group__items">
                <button
                  v-for="item in group.items"
                  :key="`${group.key}:${item.product.productKey}`"
                  class="cluster-similar app-operator-card"
                  :class="{ 'cluster-similar--disabled': !item.product.parserProductRowId }"
                  type="button"
                  :disabled="!item.product.parserProductRowId"
                  @click="openSimilarProduct(item.product)"
                >
                  <MarketProductImage :src="item.product.thumbnailUrl" :alt="item.product.name" />
                  <div class="cluster-similar__body">
                    <strong>{{ item.product.name }}</strong>
                    <span>{{ item.product.sourceSubcategory || product.sourceSubcategory || 'Ниша не указана' }}</span>
                    <div class="cluster-tags">
                      <span
                        v-for="tag in similarGroupItemTags(product, item, group.key)"
                        :key="tag.key"
                        class="cluster-tag"
                        :class="[tagClass(tag, 'cluster-tag'), tag.label.includes('\n') ? 'cluster-tag--multiline' : '']"
                      >
                        {{ tag.label }}
                      </span>
                    </div>
                  </div>
                </button>
              </div>
            </section>
          </div>

          <EmptyState
            v-else
            title="Кластеры еще не рассчитаны"
            description="Обновите анализ, чтобы увидеть похожие карточки по этому товару."
          />

          <footer class="cluster-card__actions">
            <button class="app-operator-link" type="button" @click="openProduct(product)">
              <Eye :size="15" />
              Карточка
            </button>
          </footer>
        </article>
      </section>
    </template>
    </template>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
    <DemoMarketProductDrawer
      :open="Boolean(selectedDemoProduct)"
      :product="selectedDemoProduct"
      @close="selectedDemoProduct = null"
    />
  </div>
</template>

<style scoped>
.cluster-page {
  display: grid;
  gap: var(--space-4);
}

.cluster-page__back {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
}

.cluster-error {
  margin: 0;
  border: 1px solid var(--state-danger-border-strong);
  border-radius: var(--radius-lg);
  background: var(--state-danger-soft);
  padding: var(--space-3);
  color: var(--state-danger);
  font-weight: 650;
}

.cluster-warning {
  display: grid;
  gap: var(--space-2);
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  padding: var(--space-3);
  color: var(--accent-ember-text-strong);
}

.cluster-warning strong,
.cluster-warning ul {
  margin: 0;
}

.cluster-warning ul {
  display: grid;
  gap: var(--space-1);
  padding-left: 1.1rem;
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.cluster-filters {
  display: grid;
  gap: var(--space-3);
  border-color: var(--color-border-strong);
  padding: var(--space-3);
}

.cluster-field {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.cluster-summary {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
  font-weight: 650;
}

.cluster-list,
.cluster-groups {
  display: grid;
  gap: var(--space-4);
}

.cluster-group-switcher {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  align-items: center;
}

.cluster-group-switcher__button {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  border: 1px solid var(--operator-border-muted);
  border-radius: 6px;
  background: var(--operator-panel-bg);
  color: var(--color-text);
  padding: 0.38rem 0.58rem;
  font-weight: 700;
  cursor: pointer;
}

.cluster-group-switcher__button span {
  min-width: 1.4rem;
  border-radius: 999px;
  background: var(--operator-metric-bg);
  color: var(--color-text-muted);
  padding: 0.08rem 0.35rem;
  text-align: center;
  font-size: 0.75rem;
}

.cluster-group-switcher__button--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.cluster-card {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
}

.cluster-card__head {
  display: grid;
  grid-template-columns: 5.5rem minmax(0, 1fr) auto;
  gap: var(--space-3);
  align-items: start;
}

.cluster-card__image {
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  padding: 0;
  aspect-ratio: 3 / 4;
  cursor: pointer;
}

.cluster-card__identity {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
}

.cluster-card__identity h2,
.cluster-card__identity p,
.cluster-group h3,
.cluster-group p {
  margin: 0;
}

.cluster-card__identity h2 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  line-height: 1.25;
}

.cluster-card__identity > span,
.cluster-card__identity p,
.cluster-group p,
.cluster-group header > span,
.cluster-similar__body > span {
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.cluster-card__note {
  margin-top: var(--space-1);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-metric-bg);
  padding: 0.45rem 0.6rem;
  line-height: 1.4;
}

.cluster-card__note--empty {
  color: var(--color-text-subtle);
}

.cluster-card__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--space-2);
}

.cluster-metric {
  display: grid;
  gap: 0.25rem;
  padding: 0.55rem 0.65rem;
}

.cluster-metric span {
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
  font-weight: 760;
  text-transform: uppercase;
}

.cluster-metric strong {
  color: var(--color-text);
  font-size: var(--operator-value-size);
  font-weight: 820;
}

.cluster-demo-signals {
  display: grid;
  gap: var(--space-2);
}

.cluster-demo-signal {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--accent-primary-border);
  border-radius: var(--radius-md);
  background: var(--accent-ember-soft);
  padding: var(--space-3);
}

.cluster-demo-signal strong,
.cluster-demo-signal p {
  margin: 0;
}

.cluster-demo-signal ul {
  display: grid;
  gap: var(--space-1);
  margin: 0;
  padding-left: 1.1rem;
}

.cluster-price-corridor {
  width: 100%;
  border-collapse: collapse;
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: 6px;
  font-size: 0.88rem;
}

.cluster-price-corridor th,
.cluster-price-corridor td {
  border-bottom: 1px solid var(--operator-border-muted);
  padding: 0.42rem 0.55rem;
  text-align: left;
}

.cluster-price-corridor tr:last-child th,
.cluster-price-corridor tr:last-child td {
  border-bottom: 0;
}

.cluster-price-corridor th {
  width: 52%;
  color: var(--color-text-muted);
  font-weight: 700;
}

.cluster-price-corridor td {
  font-weight: 800;
}

.cluster-price-corridor__row--warning td {
  color: var(--state-danger-text);
}

.cluster-price-corridor__row--ok td {
  color: var(--state-success-text);
}

.cluster-description-advice {
  display: grid;
  gap: var(--space-2);
}

.cluster-description-advice > div {
  display: grid;
  gap: var(--space-1);
}

.cluster-description-advice span {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 800;
  text-transform: uppercase;
}

.cluster-description-advice__suggestion p {
  border: 1px solid var(--operator-border-muted);
  border-radius: 6px;
  background: var(--operator-metric-bg);
  padding: 0.55rem;
}

.cluster-group {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-lg);
  background:
    linear-gradient(90deg, rgb(127 29 29 / 0.03), transparent 42%),
    var(--operator-card-bg);
  padding: var(--space-3);
}

.cluster-group header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
}

.cluster-group h3 {
  color: var(--color-text);
  font-size: var(--operator-title-size);
  font-weight: 820;
}

.cluster-group__items {
  display: grid;
  grid-template-columns: repeat(auto-fill, 16rem);
  justify-content: start;
  gap: var(--space-2);
}

.cluster-similar {
  display: grid;
  grid-template-columns: 4rem minmax(0, 1fr);
  gap: var(--space-2);
  align-items: start;
  border-color: var(--operator-border-muted);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  padding: var(--space-2);
  text-align: left;
}

.cluster-similar:hover:not(:disabled) {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
}

.cluster-similar--disabled {
  cursor: default;
  opacity: 0.78;
}

.cluster-similar__body {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.cluster-similar__body strong {
  overflow: hidden;
  color: var(--color-text);
  font-size: var(--operator-body-size);
  font-weight: 800;
  line-height: 1.25;
  text-overflow: ellipsis;
}

.cluster-tags {
  display: flex;
  width: 100%;
  min-width: 0;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.35rem;
}

.cluster-tag {
  display: inline-block;
  width: fit-content;
  max-width: 100%;
  box-sizing: border-box;
  align-items: flex-start;
  border: 1px solid var(--operator-border-muted);
  border-radius: 6px;
  background: var(--operator-metric-bg);
  color: var(--color-text);
  font-size: var(--operator-meta-size);
  font-weight: 760;
  line-height: 1.25;
  overflow-wrap: anywhere;
  padding: 0.3rem 0.52rem;
  text-align: left;
  white-space: pre-line;
}

.cluster-tag--multiline {
  border-radius: 4px;
  padding: 0.38rem 0.58rem;
}

.cluster-tag--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.cluster-tag--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger-text);
}

.cluster-tag--warning {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.cluster-tag--neutral {
  border-color: var(--operator-border-muted);
  background: var(--operator-metric-bg);
  color: var(--color-text-muted);
}

.cluster-card__actions {
  border-top: 1px solid var(--color-border);
  padding-top: var(--space-3);
}

@media (min-width: 880px) {
  .cluster-filters {
    grid-template-columns: minmax(18rem, 1fr) minmax(10rem, 14rem) minmax(14rem, 20rem);
    align-items: end;
  }
}

@media (max-width: 980px) {
  .cluster-card__head,
  .cluster-card__metrics {
    grid-template-columns: 1fr;
  }
}
</style>
