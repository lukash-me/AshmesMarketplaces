<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { X } from 'lucide-vue-next';

import { useAuthStore } from '@/features/auth/auth.store';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import { getHotProductsRecommendations } from '@/features/parser-products/hotProductsRecommendations.api';
import type {
  HotProductRecommendationFactor,
  HotProductRecommendationItem,
  HotProductsGroup,
  HotProductsListResponse
} from '@/features/parser-products/hotProductsRecommendations.types';
import { getParserProductFilterOptions } from '@/features/parser-products/parserProducts.api';
import type { ParserProductFilterOptions, ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import Input from '@/shared/ui/Input.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

type FactorMode = 'any' | 'all';

type OpportunityFilters = {
  search: string;
  sourceSubcategory: string;
  groupKey: string;
  factorKeys: string[];
  factorMode: FactorMode;
};

const groupPriority = [
  'good_reviews_weak_visibility',
  'bad_recent_reviews',
  'low_review_count_top_position',
  'high_position_weak_card',
  'good_reviews_weak_card',
  'expensive_without_advantage',
  'top_low_stock',
  'good_reviews_low_stock',
  'good_reviews_high_price',
  'duplicate_cards',
  'repeated_review_complaint',
  'weak_description',
  'weak_visible_description',
  'missing_key_specs',
  'fast_position_growth',
  'slow_delivery',
  'top_low_stock_slow_central_delivery',
  'faster_than_peers_region_delivery'
];

const factorCatalog: Record<string, { label: string; help: string }> = {
  good_reviews_weak_visibility: {
    label: 'Хорошие отзывы, слабая видимость',
    help: 'Товар нравится покупателям, но находится ниже похожих товаров в выдаче.'
  },
  bad_recent_reviews: {
    label: 'Плохие последние отзывы',
    help: 'Последние отзывы с оценкой 3 и ниже. Текст отзыва временно не анализируется.'
  },
  low_review_count_top_position: {
    label: 'Мало отзывов в топе',
    help: 'Карточка заметна в выдаче, но отзывов меньше, чем у похожих товаров.'
  },
  high_position_weak_card: {
    label: 'Слабая карточка в топе',
    help: 'Карточка видима в выдаче, но у нее найдены проверяемые слабые места в описании, характеристиках или визуальной подаче.'
  },
  good_reviews_weak_card: {
    label: 'Хороший товар, слабая карточка',
    help: 'Отзывы выглядят сильными, но карточке не хватает проверяемой информации или визуального качества.'
  },
  expensive_without_advantage: {
    label: 'Высокая цена',
    help: 'Цена выше похожих товаров без видимых преимуществ в наблюдаемых данных.'
  },
  top_low_stock: {
    label: 'Низкий остаток',
    help: 'Товар заметен в выдаче, но остаток ниже типичного уровня в похожей выборке.'
  },
  good_reviews_low_stock: {
    label: 'Хорошие отзывы, низкий остаток',
    help: 'Покупатели оценивают товар хорошо, но остаток выглядит низким.'
  },
  good_reviews_high_price: {
    label: 'Хорошие отзывы, высокая цена',
    help: 'У товара хорошие отзывы, но цена выше похожих товаров.'
  },
  duplicate_cards: {
    label: 'Одинаковые карточки',
    help: 'В нише найдены группы похожих или почти одинаковых карточек.'
  },
  repeated_review_complaint: {
    label: 'Повторяющаяся жалоба',
    help: 'В текстах отзывов повторяется одна и та же жалоба.'
  },
  weak_description: {
    label: 'Слабое описание',
    help: 'Описание получено и выглядит слишком коротким или неполным.'
  },
  weak_visible_description: {
    label: 'Слабое описание',
    help: 'У видимых конкурентов описание выглядит неполным.'
  },
  missing_key_specs: {
    label: 'Мало характеристик',
    help: 'Характеристики получены, но их мало для проверки товара.'
  },
  fast_position_growth: {
    label: 'Быстрый рост',
    help: 'Позиция товара заметно улучшилась между наблюдениями.'
  },
  seller_stock_slow_central_delivery: {
    label: 'Долгая доставка со склада продавца',
    help: 'До московской контрольной точки доставка дольше послезавтра, источник доставки - склад продавца.'
  },
  top_low_stock_slow_central_delivery: {
    label: 'Топ, низкий остаток и долгая доставка',
    help: 'Товар высоко в выдаче, остаток низкий, доставка до Центрального региона дольше послезавтра.'
  },
  top_slow_cluster_region_delivery: {
    label: 'Топ, долгая доставка в регион кластера',
    help: 'Товар высоко в выдаче, но доставка в характерный регион похожих товаров дольше послезавтра.'
  },
  top_slow_central_delivery: {
    label: 'Топ, долгая доставка в Центральный регион',
    help: 'Товар высоко в выдаче, но доставка до московской контрольной точки дольше послезавтра.'
  },
  peers_slow_region_delivery: {
    label: 'Похожие доставляются с задержкой',
    help: 'У похожих карточек в выбранном регионе доставка обычно дольше послезавтра.'
  },
  faster_than_peers_region_delivery: {
    label: 'Быстрее похожих',
    help: 'Карточка доставляется в регион быстрее медианы похожих товаров.'
  }
};

const deprecatedFactorCodes = new Set(['high_position_weak_reviews']);
const contentEvidenceFactorCodes = new Set([
  'high_position_weak_card',
  'good_reviews_weak_card',
  'weak_description',
  'weak_visible_description',
  'missing_key_specs'
]);
const negativeFactorCodes = new Set([
  'bad_recent_reviews',
  'low_review_count_top_position',
  'expensive_without_advantage',
  'top_low_stock',
  'weak_description',
  'weak_visible_description',
  'missing_key_specs',
  'repeated_review_complaint',
  'seller_stock_slow_central_delivery',
  'top_low_stock_slow_central_delivery',
  'top_slow_central_delivery'
]);

const hiddenFactorCodes = new Set([
  'top_slow_cluster_region_delivery'
]);

const factorLabelOverrides: Record<string, string> = {
  slow_delivery: 'Долгая доставка',
  seller_stock_slow_central_delivery: 'Долгая доставка со склада продавца',
  top_slow_central_delivery: 'Товар в топе, доставка дольше похожих',
  peers_slow_region_delivery: 'Похожие доставляются долго',
  faster_than_peers_region_delivery: 'Быстрее похожих'
};

const factorHelpOverrides: Record<string, string> = {
  slow_delivery: 'Карточки с долгой доставкой в значимые регионы.',
  seller_stock_slow_central_delivery: 'Доставка до Центрального региона дольше послезавтра, источник - склад продавца.',
  top_slow_central_delivery: 'Товар в топе, но доставка до региона дольше медианы похожих товаров минимум на сутки.',
  peers_slow_region_delivery: 'У похожих карточек в регионе доставка обычно дольше послезавтра.',
  faster_than_peers_region_delivery: 'Карточка доставляется в регион быстрее медианы похожих товаров.'
};

const compositeGroupFactorCodes: Record<string, string[]> = {
  slow_delivery: [
    'seller_stock_slow_central_delivery',
    'top_slow_central_delivery',
    'top_low_stock_slow_central_delivery'
  ]
};

const combinableLogisticsFactorCodes = new Set([
  'peers_slow_region_delivery',
  'faster_than_peers_region_delivery',
  'seller_stock_slow_central_delivery',
  'top_slow_central_delivery',
  'top_low_stock_slow_central_delivery'
]);

const emptyFilterOptions: ParserProductFilterOptions = {
  categories: [],
  subcategories: [],
  brands: [],
  sellers: []
};

const authStore = useAuthStore();
const { user } = storeToRefs(authStore);
const response = ref<HotProductsListResponse | null>(null);
const filterOptions = ref<ParserProductFilterOptions>(emptyFilterOptions);
const selectedProduct = ref<ParserProductListItem | null>(null);
const loading = ref(false);
const filterOptionsLoading = ref(false);
const error = ref('');
const filterError = ref('');
let loadVersion = 0;

const filters = reactive<OpportunityFilters>({
  search: '',
  sourceSubcategory: '',
  groupKey: '',
  factorKeys: [],
  factorMode: 'any'
});

const groups = computed(() => response.value?.groups.filter((group) => groupVisibleCount(group) > 0) ?? []);

const orderedGroups = computed(() =>
  [...groups.value].sort((left, right) => groupOrder(left.key) - groupOrder(right.key))
);

const selectedGroup = computed(() =>
  orderedGroups.value.find((group) => group.key === filters.groupKey)
  ?? orderedGroups.value[0]
  ?? null
);

const hotProductsScheduleText = computed(() => {
  const schedule = user.value?.analysisSchedule;
  if (!schedule) {
    return 'Перспективные товары обновляются автоматически один раз в сутки.';
  }

  return `Обновляется ежедневно в ${schedule.hotProductsLocalTime}. Следующий запуск: ${formatScheduleDate(schedule.nextHotProductsRunAtUtc)}.`;
});

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

const factorSourceItems = computed(() => {
  if (!selectedGroup.value) {
    return filterBySearch(response.value?.items ?? []);
  }

  const items = selectedGroup.value.key === 'duplicate_cards'
    ? selectedGroup.value.clusters.flatMap((cluster) => cluster.items)
    : visibleGroupItems(selectedGroup.value);

  return filterBySearch(items);
});

const availableFactors = computed(() => {
  const map = new Map<string, { key: string; label: string; count: number }>();

  factorSourceItems.value.forEach((item) => {
    visibleFactorTags(item).forEach((factor) => {
      const existing = map.get(factor.code);
      if (existing) {
        existing.count += 1;
        return;
      }

      map.set(factor.code, {
        key: factor.code,
        label: factorLabel(factor.code, factor.label),
        count: 1
      });
    });
  });

  return [...map.values()].sort((left, right) => groupOrder(left.key) - groupOrder(right.key));
});

const activeItems = computed(() => {
  if (!selectedGroup.value || selectedGroup.value.key === 'duplicate_cards') {
    return [];
  }

  return filterItems(visibleGroupItems(selectedGroup.value));
});

const activeClusters = computed(() => {
  if (selectedGroup.value?.key !== 'duplicate_cards') {
    return [];
  }

  return selectedGroup.value.clusters
    .map((cluster) => ({
      ...cluster,
      items: filterItems(cluster.items.filter((item) => matchesGroupFactor(item, selectedGroup.value!.key)))
    }))
    .filter((cluster) => cluster.items.length > 0);
});

const fallbackItems = computed(() => (orderedGroups.value.length ? [] : filterItems(response.value?.items ?? [])));

const chips = computed(() => {
  const result: Array<{ key: string; label: string; value: string }> = [];

  if (filters.search.trim()) {
    result.push({ key: 'search', label: 'Поиск', value: filters.search.trim() });
  }
  if (filters.sourceSubcategory.trim()) {
    result.push({ key: 'sourceSubcategory', label: 'Ниша', value: filters.sourceSubcategory.trim() });
  }
  filters.factorKeys.forEach((key) => {
    result.push({ key: `factor:${key}`, label: 'Тег', value: factorLabel(key) });
  });

  return result;
});

void loadFilterOptions();
void loadOpportunities();

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

  try {
    const data = await getHotProductsRecommendations({
      page: 1,
      pageSize: 50,
      ...(filters.sourceSubcategory ? { sourceSubcategory: filters.sourceSubcategory } : {})
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

function applyFilters(): void {
  void loadOpportunities();
}

function resetFilters(): void {
  filters.search = '';
  filters.sourceSubcategory = '';
  filters.groupKey = '';
  filters.factorKeys = [];
  filters.factorMode = 'any';
  void loadOpportunities();
}

function removeFilter(key: string): void {
  if (key === 'search') {
    filters.search = '';
  } else if (key === 'sourceSubcategory') {
    filters.sourceSubcategory = '';
  } else if (key.startsWith('factor:')) {
    toggleFactor(key.slice('factor:'.length));
    return;
  }

  void loadOpportunities();
}

function selectGroup(group: HotProductsGroup): void {
  filters.groupKey = group.key;
  filters.factorKeys = [];
  filters.factorMode = 'any';
}

function toggleFactor(key: string): void {
  filters.factorKeys = filters.factorKeys.includes(key)
    ? filters.factorKeys.filter((value) => value !== key)
    : [...filters.factorKeys, key];
}

function setFactorMode(mode: FactorMode): void {
  filters.factorMode = mode;
}

function groupOrder(key: string): number {
  const index = groupPriority.indexOf(key);
  return index === -1 ? groupPriority.length : index;
}

function factorLabel(key: string, fallback?: string): string {
  return factorLabelOverrides[key] ?? factorCatalog[key]?.label ?? fallback ?? key;
}

function factorHelp(key: string, fallback?: string, value?: HotProductRecommendationFactor['value']): string {
  if (key === 'bad_recent_reviews') {
    const scope = value !== null && typeof value === 'object' && value.reviewScope === 'root'
      ? ' Использован fallback по общей карточке/вариациям, потому что для выбранного WB id не было собственных отзывов.'
      : '';
    const base = `Анализируются последние 10 отзывов: сначала отзывы за 14 дней, затем более старые до набора 10. Плохими временно считаются только оценки 1-3. Текст отзыва, плюсы и минусы сейчас не анализируются.${scope}`;
    const evidence = formatNegativeReviewEvidence(value);
    return evidence ? `${base} ${evidence}` : base;
  }

  if (factorHelpOverrides[key]) {
    return factorHelpOverrides[key];
  }

  return factorCatalog[key]?.help ?? fallback ?? 'Фактор рассчитан по сохраненным рыночным данным.';
}

function formatNegativeReviewEvidence(value: HotProductRecommendationFactor['value']): string {
  if (value === null || typeof value !== 'object') {
    return '';
  }

  const evidence = value.negativeReviewEvidence;
  if (!Array.isArray(evidence) || evidence.length === 0) {
    return '';
  }

  const snippets = evidence
    .slice(0, 3)
    .map((item) => {
      if (item === null || typeof item !== 'object') {
        return '';
      }

      const rating = 'rating' in item && typeof item.rating === 'number'
        ? `оценка ${item.rating}`
        : 'оценка не указана';
      const wbProductId = 'sourceWbProductId' in item && typeof item.sourceWbProductId === 'string'
        ? `WB ${item.sourceWbProductId}`
        : '';
      const reasons = 'reasonCodes' in item && Array.isArray(item.reasonCodes)
        ? item.reasonCodes
            .map((reason) => typeof reason === 'string' ? reasonLabel(reason) : '')
            .filter(Boolean)
            .join(', ')
        : '';
      const snippet = 'snippet' in item && typeof item.snippet === 'string'
        ? item.snippet.trim()
        : '';
      const meta = [rating, wbProductId, reasons].filter(Boolean).join(', ');
      return snippet ? `${meta}: ${snippet}` : meta;
    })
    .filter(Boolean);

  return snippets.length ? `Проверочные примеры: ${snippets.join('; ')}.` : '';
}

function reasonLabel(reason: string): string {
  const labels: Record<string, string> = {
    low_rating: 'низкая оценка',
  };
  return labels[reason] ?? reason;
}

function filterItems(items: HotProductRecommendationItem[]): HotProductRecommendationItem[] {
  return filterBySearch(items).filter((item) => matchesFactorFilter(item));
}

function filterBySearch(items: HotProductRecommendationItem[]): HotProductRecommendationItem[] {
  return items.filter((item) => matchesSearch(item));
}

function matchesSearch(item: HotProductRecommendationItem): boolean {
  const search = filters.search.trim().toLocaleLowerCase('ru-RU');
  if (!search) {
    return true;
  }

  return [
    item.productName,
    item.wbProductId,
    item.wbRootId,
    item.brandName,
    item.sellerName,
    item.sourceSubcategory
  ].some((value) => value?.toLocaleLowerCase('ru-RU').includes(search));
}

function matchesFactorFilter(item: HotProductRecommendationItem): boolean {
  if (filters.factorKeys.length === 0) {
    return true;
  }

  const codes = new Set(visibleFactorTags(item).map((factor) => factor.code));
  return filters.factorMode === 'all'
    ? filters.factorKeys.every((key) => codes.has(key))
    : filters.factorKeys.some((key) => codes.has(key));
}

function visibleFactorTags(item: HotProductRecommendationItem): HotProductRecommendationFactor[] {
  return combineLogisticsFactorTags(item.factors.filter((factor) => !isHiddenLegacyFactor(factor)));
}

function combineLogisticsFactorTags(factors: HotProductRecommendationFactor[]): HotProductRecommendationFactor[] {
  const result: HotProductRecommendationFactor[] = [];
  const grouped = new Map<string, HotProductRecommendationFactor[]>();

  factors.forEach((factor) => {
    if (!combinableLogisticsFactorCodes.has(factor.code)) {
      result.push(factor);
      return;
    }

    const existing = grouped.get(factor.code);
    if (existing) {
      existing.push(factor);
      return;
    }

    grouped.set(factor.code, [factor]);
  });

  grouped.forEach((items) => {
    if (items.length === 1) {
      result.push(items[0]);
      return;
    }

    result.push({
      ...items[0],
      value: {
        items: items
          .map((item) => item.value)
          .filter((value): value is Record<string, unknown> => value !== null && typeof value === 'object')
      }
    });
  });

  return result;
}

function visibleGroupItems(group: HotProductsGroup): HotProductRecommendationItem[] {
  return group.items.filter((item) => matchesGroupFactor(item, group.key));
}

function matchesGroupFactor(item: HotProductRecommendationItem, groupKey: string): boolean {
  const groupCodes = compositeGroupFactorCodes[groupKey] ?? [groupKey];
  return visibleFactorTags(item).some((factor) => groupCodes.includes(factor.code));
}

function groupVisibleCount(group: HotProductsGroup): number {
  if (group.key === 'duplicate_cards') {
    return group.clusters.reduce(
      (count, cluster) => count + cluster.items.filter((item) => matchesGroupFactor(item, group.key)).length,
      0
    );
  }

  return visibleGroupItems(group).length;
}

function isHiddenLegacyFactor(factor: HotProductRecommendationFactor): boolean {
  return hiddenFactorCodes.has(factor.code)
    || deprecatedFactorCodes.has(factor.code)
    || (contentEvidenceFactorCodes.has(factor.code)
      && (factor.value === null || typeof factor.value !== 'object'))
    || isInvalidTopSlowCentralDeliveryFactor(factor)
    || isInvalidBadRecentReviewsFactor(factor)
    || isInvalidLowReviewCountFactor(factor);
}

function isInvalidTopSlowCentralDeliveryFactor(factor: HotProductRecommendationFactor): boolean {
  if (factor.code !== 'top_slow_central_delivery') {
    return false;
  }

  if (factor.value === null || typeof factor.value !== 'object') {
    return true;
  }

  const deliveryHours = optionalNumericFactorField(factor.value, 'deliveryHours');
  const peerMedianDeliveryHours = optionalNumericFactorField(factor.value, 'peerMedianDeliveryHours');
  const peerSampleSize = optionalNumericFactorField(factor.value, 'peerSampleSize');

  return deliveryHours === null
    || peerMedianDeliveryHours === null
    || peerSampleSize === null
    || peerSampleSize < 5
    || deliveryHours < peerMedianDeliveryHours + 24;
}

function isInvalidBadRecentReviewsFactor(factor: HotProductRecommendationFactor): boolean {
  if (factor.code !== 'bad_recent_reviews') {
    return false;
  }

  const serialized = `${factor.label} ${JSON.stringify(factor.value ?? '')}`.toLocaleLowerCase('ru-RU');
  if (serialized.includes('оценка 0') || serialized.includes('averageRating":0') || serialized.includes('reviewRating":0')) {
    return true;
  }

  if (factor.value === null || typeof factor.value !== 'object') {
    return true;
  }

  if (optionalNumericFactorField(factor.value, 'averageRating') === 0
    || optionalNumericFactorField(factor.value, 'reviewRating') === 0) {
    return true;
  }

  const lowRatingReviews = numericFactorField(factor.value, 'lowRatingReviews');
  const negativeTextReviews = numericFactorField(factor.value, 'negativeTextReviews');
  const badReviewCount = numericFactorField(factor.value, 'badReviewCount');
  const sentimentVersion = numericFactorField(factor.value, 'sentimentVersion');
  const reviewScope = typeof factor.value.reviewScope === 'string' ? factor.value.reviewScope : '';
  if (sentimentVersion < 2) {
    return true;
  }

  if (reviewScope !== 'product' && reviewScope !== 'root') {
    return true;
  }

  if (negativeTextReviews > 0) {
    return true;
  }

  return badReviewCount <= 0 || lowRatingReviews <= 0;
}

function isInvalidLowReviewCountFactor(factor: HotProductRecommendationFactor): boolean {
  if (factor.code !== 'low_review_count_top_position') {
    return false;
  }

  if (factor.value === null || typeof factor.value !== 'object') {
    return true;
  }

  const reviewCount = optionalNumericFactorField(factor.value, 'reviewCount');
  const peerMedianReviewCount = optionalNumericFactorField(factor.value, 'peerMedianReviewCount');
  const peerSampleSize = optionalNumericFactorField(factor.value, 'peerSampleSize');
  return reviewCount === null || reviewCount < 0
    || peerMedianReviewCount === null || peerMedianReviewCount <= 0
    || peerSampleSize === null || peerSampleSize < 5;
}

function numericFactorField(value: Record<string, unknown>, key: string): number {
  return optionalNumericFactorField(value, key) ?? 0;
}

function optionalNumericFactorField(value: Record<string, unknown>, key: string): number | null {
  const raw = value[key];
  return typeof raw === 'number' && Number.isFinite(raw) ? raw : null;
}

function optionalStringFactorField(value: Record<string, unknown>, key: string): string | null {
  const raw = value[key];
  return typeof raw === 'string' && raw.trim() ? raw.trim() : null;
}

function deliveryDays(hours: number | null): number | null {
  if (hours === null || hours <= 0) {
    return null;
  }

  return Math.ceil(hours / 24);
}

function deliveryDaysText(hours: number | null): string | null {
  const days = deliveryDays(hours);
  return days === null ? null : `${days} д`;
}

function deliverySourcePhrase(value: Record<string, unknown>): string {
  const sourceType = optionalStringFactorField(value, 'deliverySourceType');
  if (sourceType === 'wb_warehouse') {
    return 'со склада WB';
  }

  if (sourceType === 'seller_warehouse') {
    return 'со склада продавца';
  }

  return '';
}

function logisticsRegionName(value: Record<string, unknown>): string {
  return optionalStringFactorField(value, 'regionName')
    ?? optionalStringFactorField(value, 'destinationCity')
    ?? 'регион';
}

function withSource(text: string, source: string): string {
  return source ? `${text} ${source}` : text;
}

function combinedLogisticsFactorHeader(code: string): string | null {
  switch (code) {
    case 'peers_slow_region_delivery':
      return 'Похожие доставляются долго:';
    case 'faster_than_peers_region_delivery':
      return 'Доставляется быстрее похожих:';
    case 'seller_stock_slow_central_delivery':
      return 'Долгая доставка со склада продавца:';
    case 'top_slow_central_delivery':
      return 'Товар в топе, но доставка дольше похожих:';
    case 'top_low_stock_slow_central_delivery':
      return 'Топ, низкий остаток и долгая доставка:';
    default:
      return null;
  }
}

function logisticsFactorLine(code: string, value: Record<string, unknown>): string | null {
  const region = logisticsRegionName(value);
  const days = deliveryDaysText(optionalNumericFactorField(value, 'deliveryHours'));
  const peerDays = deliveryDaysText(optionalNumericFactorField(value, 'peerMedianDeliveryHours'));
  const source = deliverySourcePhrase(value);

  switch (code) {
    case 'peers_slow_region_delivery':
      return peerDays ? `${region}: медиана около ${peerDays}` : null;
    case 'faster_than_peers_region_delivery':
      return days && peerDays
        ? `${withSource(`${region}: ${days}`, source)} против медианы похожих ${peerDays}`
        : null;
    case 'seller_stock_slow_central_delivery':
      return days ? `${region}: ${days}` : null;
    case 'top_slow_central_delivery':
      return days && peerDays
        ? `${withSource(`${region}: ${days}`, source)} против ${peerDays} у похожих`
        : null;
    case 'top_low_stock_slow_central_delivery':
      return days ? withSource(`${region}: ${days}`, source) : null;
    default:
      return null;
  }
}

function logisticsFactorText(factor: HotProductRecommendationFactor): string | null {
  if (factor.value === null || typeof factor.value !== 'object') {
    return null;
  }

  const value = factor.value;
  if (Array.isArray(value.items)) {
    const header = combinedLogisticsFactorHeader(factor.code);
    const lines = value.items
      .filter((item): item is Record<string, unknown> => item !== null && typeof item === 'object')
      .map((item) => logisticsFactorLine(factor.code, item))
      .filter((line): line is string => Boolean(line));

    return header && lines.length > 0
      ? `${header}\n  ${lines.join('\n  ')}`
      : null;
  }

  const region = logisticsRegionName(value);
  const days = deliveryDaysText(optionalNumericFactorField(value, 'deliveryHours'));
  const peerDays = deliveryDaysText(optionalNumericFactorField(value, 'peerMedianDeliveryHours'));
  const source = deliverySourcePhrase(value);

  switch (factor.code) {
    case 'peers_slow_region_delivery':
      return peerDays ? `Похожие доставляются долго: ${region}: медиана около ${peerDays}` : null;
    case 'faster_than_peers_region_delivery':
      return days && peerDays
        ? `${withSource(`Доставляется быстрее похожих: ${region}: ${days}`, source)} против медианы похожих ${peerDays}`
        : null;
    case 'seller_stock_slow_central_delivery':
      return days ? `Долгая доставка в ${region} со склада продавца: ${days}` : null;
    case 'top_slow_central_delivery':
      return days && peerDays
        ? `${withSource(`Товар в топе, но доставка в ${region} дольше похожих: ${days}`, source)} против ${peerDays} у похожих`
        : null;
    case 'top_low_stock_slow_central_delivery':
      return days ? withSource(`Топ, низкий остаток и долгая доставка в ${region}: ${days}`, source) : null;
    case 'top_slow_cluster_region_delivery':
      return days && peerDays
        ? `${withSource(`Топ, но похожие доставляются быстрее в ${region}: ${days}`, source)} против ${peerDays} у похожих`
        : null;
    default:
      return null;
  }
}

function factorText(factor: HotProductRecommendationFactor): string {
  const logisticsText = logisticsFactorText(factor);
  if (logisticsText) {
    return logisticsText;
  }

  const label = factorLabel(factor.code, factor.label);
  const value = factor.value;

  if (value === null || value === undefined || value === '') {
    return label;
  }

  if (typeof value === 'object') {
    const objectLabel = 'label' in value ? value.label : null;
    if (factor.code === 'low_review_count_top_position' && !hasPeerReviewComparison(objectLabel)) {
      return label;
    }

    return typeof objectLabel === 'string' && objectLabel.trim()
      ? `${label}: ${objectLabel}`
      : label;
  }

  if (factor.code === 'low_review_count_top_position' && !hasPeerReviewComparison(value)) {
    return label;
  }

  return `${label}: ${value}`;
}

function factorTagClasses(factor: HotProductRecommendationFactor): string[] {
  const text = factorText(factor);
  return [
    `opportunity-tag--${factorTone(factor)}`,
    text.includes('\n') ? 'opportunity-tag--multiline' : ''
  ].filter(Boolean);
}

function hasPeerReviewComparison(value: unknown): boolean {
  return typeof value === 'string' && value.toLocaleLowerCase('ru-RU').includes('против');
}

function factorTone(factor: HotProductRecommendationFactor): string {
  if (factor.direction === 'positive') {
    return 'positive';
  }

  if (factor.direction === 'negative' || negativeFactorCodes.has(factor.code)) {
    return 'negative';
  }

  return 'neutral';
}

function openProduct(item: HotProductRecommendationItem): void {
  if (!item.parserProductRowId) {
    return;
  }

  selectedProduct.value = toParserProduct(item);
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
    <PageHeader title="Перспективные товары" />
    <p class="analysis-schedule-note">{{ hotProductsScheduleText }}</p>

    <form class="filters app-surface" @submit.prevent="applyFilters">
      <div class="filters__search">
        <Input
          v-model="filters.search"
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
          label="Ниша"
          placeholder="Все ниши"
          search-placeholder="Найти нишу"
          :options="filterOptions.subcategories"
        />
      </div>

      <p v-if="filterError" class="filters__notice">{{ filterError }}</p>
      <p v-else-if="filterOptionsLoading" class="filters__notice">Загружаем варианты фильтров...</p>

      <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры">
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
          <X :size="13" aria-hidden="true" />
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
      description="Запустите обновление, чтобы увидеть проверяемые идеи по выбранным нишам."
    >
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
                {{ group.title || factorLabel(group.key) }}
            <span>{{ groupVisibleCount(group) }}</span>
          </button>
        </div>

        <div v-if="selectedGroup" class="opportunities-group">
          <div v-if="availableFactors.length" class="factor-filter">
            <div class="factor-filter__header">
              <strong>Фильтрация</strong>
              <HelpTooltip text="Можно выбрать несколько тегов. Режим «Все» покажет только товары, где есть каждый выбранный тег." />
              <div class="factor-filter__mode" role="group" aria-label="Режим фильтрации тегов">
                <button
                  type="button"
                  class="factor-filter__mode-button"
                  :class="{ 'factor-filter__mode-button--active': filters.factorMode === 'any' }"
                  @click="setFactorMode('any')"
                >
                  Любой
                </button>
                <button
                  type="button"
                  class="factor-filter__mode-button"
                  :class="{ 'factor-filter__mode-button--active': filters.factorMode === 'all' }"
                  @click="setFactorMode('all')"
                >
                  Все
                </button>
              </div>
            </div>
            <div class="factor-filter__chips">
              <button
                v-for="factor in availableFactors"
                :key="factor.key"
                type="button"
                class="factor-filter__chip"
                :class="{ 'factor-filter__chip--active': filters.factorKeys.includes(factor.key) }"
                @click="toggleFactor(factor.key)"
              >
                {{ factor.label }}
                <span>{{ factor.count }}</span>
              </button>
            </div>
          </div>

          <header class="opportunities-group__header">
            <h2>
              <span>{{ selectedGroup.title || factorLabel(selectedGroup.key) }}</span>
              <HelpTooltip :text="factorHelp(selectedGroup.key, selectedGroup.description)" />
            </h2>
            <span>{{ selectedGroup.key === 'duplicate_cards' ? activeClusters.length : activeItems.length }} товаров</span>
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
                  <p>{{ cluster.items.length }} похожих карточек</p>
                </div>
              </header>
              <div class="duplicate-cluster__items">
                <button
                  v-for="item in cluster.items"
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

                <div class="opportunity-tags">
                  <span
                    v-for="factor in visibleFactorTags(item)"
                    :key="`${item.id}:${factor.code}`"
                    class="opportunity-tag"
                    :class="factorTagClasses(factor)"
                  >
                    {{ factorText(factor) }}
                    <HelpTooltip :text="factorHelp(factor.code, factor.label, factor.value)" />
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
            v-if="!activeItems.length && !activeClusters.length"
            title="Поиск не дал результатов"
            description="Измените поиск, подборку или выбранные теги."
          />
        </div>
      </section>

      <section v-else class="opportunities-groups app-operator-panel">
        <EmptyState
          title="Обновите анализ, чтобы увидеть подборки"
          description="Текущий сохраненный анализ не содержит проверяемых групп."
        >
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
                    v-for="factor in visibleFactorTags(item)"
                    :key="`${item.id}:${factor.code}`"
                    class="opportunity-tag"
                    :class="factorTagClasses(factor)"
                  >
                    {{ factorText(factor) }}
                    <HelpTooltip :text="factorHelp(factor.code, factor.label, factor.value)" />
                  </span>
                </div>
              </div>
            </article>
          </div>
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
.market-opportunities {
  display: grid;
  gap: var(--space-4);
}

.analysis-schedule-note {
  margin: calc(var(--space-2) * -1) 0 0;
  color: var(--text-muted);
  font-size: 0.9rem;
}

.filters {
  position: relative;
  z-index: 3;
  display: grid;
  gap: var(--space-3);
  overflow: visible;
  border-color: var(--color-border-strong);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.035), transparent 42%),
    var(--surface-panel);
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

.filters__chips,
.factor-filter__chips,
.opportunities-tabs {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: var(--space-2);
}

.filters__chip,
.factor-filter__chip {
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

.factor-filter {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-card-bg);
  padding: var(--space-3);
}

.factor-filter__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.factor-filter__header strong {
  font-size: var(--operator-body-size);
}

.factor-filter__mode {
  display: inline-flex;
  gap: var(--space-1);
  margin-left: auto;
}

.factor-filter__mode-button {
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-sm);
  background: var(--operator-metric-bg);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-meta-size);
  font-weight: 760;
  padding: 0.28rem 0.55rem;
}

.factor-filter__mode-button--active,
.factor-filter__chip--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.factor-filter__chip span,
.opportunities-tab span {
  min-width: 1.4rem;
  border-radius: 999px;
  background: var(--operator-metric-bg);
  padding: 0.12rem 0.45rem;
  text-align: center;
}

.opportunities-groups {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.opportunities-tab {
  display: inline-flex;
  min-height: 2.25rem;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-card-bg);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-body-size);
  font-weight: 760;
  padding: 0 var(--space-3);
}

.opportunities-tab:hover,
.opportunities-tab--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.opportunities-group,
.opportunities-fallback,
.duplicate-clusters {
  display: grid;
  gap: var(--space-4);
}

.opportunities-group__header,
.duplicate-cluster header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
}

.opportunities-group__header h2,
.opportunities-fallback h2,
.duplicate-cluster h3,
.opportunity-card h3 {
  margin: 0;
  color: var(--color-text);
}

.opportunities-group__header h2,
.opportunities-fallback h2 {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
}

.opportunities-group__header > span {
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
}

.duplicate-cluster p {
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

.opportunity-card h3 {
  font-size: var(--operator-title-size);
  line-height: 1.25;
}

.opportunity-tags {
  display: flex;
  width: 100%;
  min-width: 0;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.4rem;
}

.opportunity-tag {
  display: inline-grid;
  grid-template-columns: minmax(0, 1fr) auto;
  width: fit-content;
  max-width: 100%;
  box-sizing: border-box;
  align-items: flex-start;
  gap: 0.35rem;
  border: 1px solid var(--operator-border-muted);
  border-radius: 6px;
  background: var(--operator-metric-bg);
  color: var(--color-text);
  font-size: var(--operator-meta-size);
  font-weight: 760;
  line-height: 1.25;
  overflow-wrap: anywhere;
  padding: 0.34rem 0.55rem;
  text-align: left;
  white-space: pre-line;
}

.opportunity-tag--multiline {
  border-radius: 4px;
  padding: 0.42rem 0.6rem;
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
  grid-template-columns: repeat(auto-fill, 14rem);
  align-items: start;
  justify-content: start;
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

.duplicate-cluster__item:hover {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
}

.duplicate-cluster__item span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (min-width: 880px) {
  .filters__search {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__selects {
    grid-template-columns: minmax(16rem, 28rem);
  }
}

@media (max-width: 980px) {
  .opportunities-grid {
    grid-template-columns: 1fr;
  }

  .opportunities-group__header,
  .duplicate-cluster header {
    display: grid;
  }

  .factor-filter__mode {
    margin-left: 0;
  }
}
</style>
