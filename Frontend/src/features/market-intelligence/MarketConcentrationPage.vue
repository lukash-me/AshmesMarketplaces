<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import PageHeader from '@/widgets/PageHeader.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';

import MarketConcentrationSection from './MarketConcentrationSection.vue';
import { getPublicMarketConcentration, getPublicMarketConcentrationContexts, getPublicMarketConcentrationProducts } from './marketIntelligence.api';
import {
  buildMarketIntelligenceParams,
  marketIntelligenceDefaultRegionDest,
  marketIntelligenceDefaultSort,
  marketIntelligenceTopN
} from './marketIntelligence.contexts';
import type {
  PriceQualityPoint,
  PublicMarketConcentrationSnapshot,
  PublicMarketIntelligenceAvailableContext,
  PublicMarketIntelligenceParams
} from './marketIntelligence.types';

type ProductsModalContext = {
  kind: 'seller' | 'brand' | 'root';
  key: string;
  title: string;
};
type ModalSortKey = 'price' | 'rating' | 'feedback' | 'stock' | 'position';
type ModalSortDirection = 'asc' | 'desc';

const MODAL_PAGE_SIZE = 30;
const modalSortOptions: Array<{ key: ModalSortKey; label: string }> = [
  { key: 'price', label: 'Цена' },
  { key: 'rating', label: 'Рейтинг' },
  { key: 'feedback', label: 'Отзывы' },
  { key: 'stock', label: 'Остаток' },
  { key: 'position', label: 'Позиция' }
];
const modalSortDirectionOptions: Array<{ key: ModalSortDirection; label: string }> = [
  { key: 'asc', label: 'По возрастанию' },
  { key: 'desc', label: 'По убыванию' }
];

const route = useRoute();
const router = useRouter();

const selectedSubcategory = ref(readInitialSubcategory());
const availableContexts = ref<PublicMarketIntelligenceAvailableContext[]>([]);
const contextsLoaded = ref(false);
const intelligence = ref<PublicMarketConcentrationSnapshot | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const concentrationPoints = ref<PriceQualityPoint[]>([]);
const productsModalContext = ref<ProductsModalContext | null>(null);
const productsModalPage = ref(1);
const productsModalLoading = ref(false);
const productsModalError = ref<string | null>(null);
const selectedProduct = ref<ParserProductListItem | null>(null);
const modalSortKey = ref<ModalSortKey>('position');
const modalSortDirection = ref<ModalSortDirection>('asc');
let syncingContextSelection = false;

const selectedContext = computed(() =>
  availableContexts.value.find((context) => context.sourceSubcategory === selectedSubcategory.value)
    ?? availableContexts.value[0]
    ?? null
);
const nicheOptions = computed(() =>
  availableContexts.value
    .map((context) => context.sourceSubcategory)
    .filter((value): value is string => Boolean(value))
);
const marketConcentration = computed(() => intelligence.value?.marketConcentration ?? null);
const modalProducts = computed(() => {
  const context = productsModalContext.value;
  if (!context) {
    return [];
  }

  return concentrationPoints.value;
});
const modalTotalPages = computed(() => Math.max(1, Math.ceil(modalProducts.value.length / MODAL_PAGE_SIZE)));
const sortedModalProducts = computed(() => {
  return [...modalProducts.value].sort((left, right) => {
    const leftValue = getSortValue(left, modalSortKey.value);
    const rightValue = getSortValue(right, modalSortKey.value);

    if (leftValue === null && rightValue === null) {
      return productTitle(left).localeCompare(productTitle(right), 'ru');
    }
    if (leftValue === null) {
      return modalSortKey.value === 'position' && modalSortDirection.value === 'desc' ? -1 : 1;
    }
    if (rightValue === null) {
      return modalSortKey.value === 'position' && modalSortDirection.value === 'desc' ? 1 : -1;
    }

    const diff = leftValue - rightValue;
    if (diff === 0) {
      return productTitle(left).localeCompare(productTitle(right), 'ru');
    }

    return modalSortDirection.value === 'asc' ? diff : -diff;
  });
});
const paginatedModalProducts = computed(() => {
  const start = (productsModalPage.value - 1) * MODAL_PAGE_SIZE;
  return sortedModalProducts.value.slice(start, start + MODAL_PAGE_SIZE);
});
const modalPaginationItems = computed(() => buildPaginationItems(productsModalPage.value, modalTotalPages.value));
const requestParams = computed<PublicMarketIntelligenceParams | null>(() => {
  if (!selectedContext.value) {
    return null;
  }

  return buildMarketIntelligenceParams(
    selectedContext.value,
    {
      sourceRegionDest: readStringQuery('sourceRegionDest') ?? selectedContext.value.sourceRegionDest ?? marketIntelligenceDefaultRegionDest,
      sort: readStringQuery('sort') ?? selectedContext.value.sort ?? marketIntelligenceDefaultSort
    }
  );
});

onMounted(() => {
  void refresh();
});

watch(selectedSubcategory, () => {
  if (syncingContextSelection) {
    return;
  }

  void applySubcategorySelection();
});

async function refresh(): Promise<void> {
  loading.value = true;
  error.value = null;
  closeProductsModal();

  try {
    await loadAvailableContexts();

    const params = requestParams.value;
    if (availableContexts.value.length === 0 || !params) {
      intelligence.value = null;
      return;
    }

    intelligence.value = await getPublicMarketConcentration(params);
  } catch (requestError) {
    intelligence.value = null;
    error.value = getProblemMessage(requestError, 'Не удалось загрузить концентрацию рынка.');
  } finally {
    loading.value = false;
  }
}

async function loadAvailableContexts(): Promise<void> {
  const contexts = await getPublicMarketConcentrationContexts();
  availableContexts.value = contexts;
  contextsLoaded.value = true;

  if (contexts.length === 0) {
    if (selectedSubcategory.value) {
      syncingContextSelection = true;
      selectedSubcategory.value = '';
      await nextTick();
      syncingContextSelection = false;
    }

    return;
  }

  const requested = readStringQuery('sourceSubcategory');
  const selected = contexts.find((context) => context.sourceSubcategory === selectedSubcategory.value);
  const requestedContext = contexts.find((context) => context.sourceSubcategory === requested);
  const nextContext = selected ?? requestedContext ?? contexts[0];

  if (nextContext?.sourceSubcategory && selectedSubcategory.value !== nextContext.sourceSubcategory) {
    syncingContextSelection = true;
    selectedSubcategory.value = nextContext.sourceSubcategory;
    await nextTick();
    syncingContextSelection = false;
  }

  if (requested !== nextContext?.sourceSubcategory) {
    await router.replace({
      query: {
        ...route.query,
        sourceSubcategory: nextContext?.sourceSubcategory
      }
    });
  }
}

async function applySubcategorySelection(): Promise<void> {
  if (availableContexts.value.length === 0) {
    await loadAvailableContexts();
  }

  if (availableContexts.value.length === 0) {
    return;
  }

  const selected = availableContexts.value.find((context) => context.sourceSubcategory === selectedSubcategory.value);
  if (!selected) {
    selectedSubcategory.value = availableContexts.value[0]?.sourceSubcategory ?? '';
    return;
  }

  closeProductsModal();
  const {
    latestRankRunId: _latestRankRunId,
    baselineRankRunId: _baselineRankRunId,
    latestProductRunId: _latestProductRunId,
    baselineProductRunId: _baselineProductRunId,
    ...query
  } = route.query;

  await router.replace({
    query: {
      ...query,
      sourceSubcategory: selectedSubcategory.value
    }
  });
  await refresh();
}

async function openProductsModal(context: ProductsModalContext): Promise<void> {
  const params = requestParams.value;
  if (!params) {
    return;
  }

  productsModalContext.value = context;
  productsModalPage.value = 1;
  selectedProduct.value = null;
  concentrationPoints.value = [];
  productsModalError.value = null;
  productsModalLoading.value = true;

  try {
    concentrationPoints.value = await getPublicMarketConcentrationProducts(params, context.kind, context.key);
  } catch (requestError) {
    productsModalError.value = getProblemMessage(requestError, 'Не удалось загрузить карточки группы.');
  } finally {
    productsModalLoading.value = false;
  }
}

function closeProductsModal(): void {
  productsModalContext.value = null;
  productsModalPage.value = 1;
  concentrationPoints.value = [];
  productsModalError.value = null;
  productsModalLoading.value = false;
  selectedProduct.value = null;
}

function setModalPage(page: number): void {
  productsModalPage.value = Math.min(Math.max(page, 1), modalTotalPages.value);
}

function buildPaginationItems(currentPage: number, totalPages: number): Array<number | string> {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  if (currentPage <= 4) {
    return [1, 2, 3, 4, 5, 'end-ellipsis', totalPages];
  }

  if (currentPage >= totalPages - 3) {
    return [1, 'start-ellipsis', totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
  }

  return [
    1,
    'start-ellipsis',
    currentPage - 2,
    currentPage - 1,
    currentPage,
    currentPage + 1,
    currentPage + 2,
    'end-ellipsis',
    totalPages
  ];
}

function setModalSortKey(key: ModalSortKey): void {
  modalSortKey.value = key;
  modalSortDirection.value = defaultSortDirection(key);
  productsModalPage.value = 1;
}

function setModalSortDirection(direction: ModalSortDirection): void {
  modalSortDirection.value = direction;
  productsModalPage.value = 1;
}

function defaultSortDirection(key: ModalSortKey): ModalSortDirection {
  return key === 'position' || key === 'price' ? 'asc' : 'desc';
}

function getSortValue(point: PriceQualityPoint, key: ModalSortKey): number | null {
  if (key === 'price') {
    return point.price;
  }
  if (key === 'rating') {
    return point.rating;
  }
  if (key === 'feedback') {
    return point.feedbackCount;
  }
  if (key === 'stock') {
    return point.stock;
  }

  return point.position;
}

function openProduct(point: PriceQualityPoint): void {
  if (!point.productRowId) {
    return;
  }

  selectedProduct.value = toParserProductListItem(point);
}

function closeProduct(): void {
  selectedProduct.value = null;
}

function toParserProductListItem(point: PriceQualityPoint): ParserProductListItem {
  const current = intelligence.value;
  const context = selectedContext.value;
  const observedAt = current?.observationWindow.latestObservedAtUtc ?? new Date().toISOString();

  return {
    id: point.productRowId!,
    parserRunId: current?.observationWindow.latestProductRunId ?? '',
    parsedAtUtc: observedAt,
    wbProductId: point.wbProductId,
    wbRootId: point.wbRootId,
    name: productTitle(point),
    brandName: point.brandName,
    sellerName: point.sellerName,
    priceRegular: null,
    priceDiscounted: point.price,
    priceWbWallet: null,
    discountPercent: null,
    totalQuantity: point.stock,
    ratingRounded: null,
    reviewRating: point.rating,
    feedbackCount: point.feedbackCount,
    sourceCategory: current?.context.sourceCategory ?? context?.sourceCategory ?? '',
    sourceSubcategory: current?.context.sourceSubcategory ?? context?.sourceSubcategory ?? '',
    sourceQuery: current?.context.query ?? context?.query ?? '',
    thumbnailUrl: point.thumbnailUrl,
    rank: point.position === null ? null : {
      absolutePosition: point.position,
      page: Math.max(1, Math.ceil(point.position / 100)),
      positionOnPage: ((point.position - 1) % 100) + 1,
      query: current?.context.query ?? context?.query ?? '',
      sourceCategory: current?.context.sourceCategory ?? context?.sourceCategory ?? '',
      sourceSubcategory: current?.context.sourceSubcategory ?? context?.sourceSubcategory ?? '',
      sourceRegionDest: current?.context.sourceRegionDest ?? context?.sourceRegionDest ?? marketIntelligenceDefaultRegionDest,
      sort: current?.context.sort ?? context?.sort ?? marketIntelligenceDefaultSort,
      observedAtUtc: observedAt,
      parserRunId: current?.observationWindow.latestRankRunId ?? '',
      rankContextId: current?.observationWindow.latestRankRunId ?? '',
      contextsCount: 1
    },
    position: {
      state: point.position === null ? 'unknown' : 'observed',
      absolutePosition: point.position,
      observedRangeLimit: current?.context.topN ?? context?.topN ?? marketIntelligenceTopN,
      query: current?.context.query ?? context?.query ?? '',
      sourceCategory: current?.context.sourceCategory ?? context?.sourceCategory ?? '',
      sourceSubcategory: current?.context.sourceSubcategory ?? context?.sourceSubcategory ?? '',
      observedAtUtc: point.position === null ? null : observedAt
    }
  };
}

function productTitle(point: PriceQualityPoint): string {
  return point.productName?.trim() || `WB ${point.wbProductId}`;
}

function formatPrice(value: number | null | undefined): string {
  return typeof value === 'number'
    ? `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)} ₽`
    : 'Нет цены';
}

function formatNumber(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value)
    : '—';
}

function formatPosition(value: number | null | undefined): string {
  if (typeof value === 'number') {
    return `#${formatNumber(value)}`;
  }

  const topN = intelligence.value?.context.topN;
  return typeof topN === 'number' && topN > 0
    ? `>${formatNumber(topN)}`
    : '—';
}

function readInitialSubcategory(): string {
  return readStringQuery('sourceSubcategory') ?? '';
}

function readStringQuery(key: string): string | undefined {
  const raw = route.query[key];
  return typeof raw === 'string' && raw.length > 0 ? raw : undefined;
}
</script>

<template>
  <div class="market-concentration-page">
    <PageHeader
      title="Концентрация рынка"
      subtitle="Показывает, насколько топ выбранной ниши занят крупными продавцами, брендами и повторяющимися root-группами."
    />

    <section v-if="availableContexts.length" class="mi-controls app-surface">
      <div class="mi-controls__fields">
        <MarketFilterSelect
          v-model="selectedSubcategory"
          class="mi-field"
          label="Ниша"
          placeholder="Все ниши"
          search-placeholder="Найти нишу"
          :options="nicheOptions"
        />
      </div>
    </section>

    <LoadingState v-if="loading && !intelligence" label="Считаем концентрацию рынка..." />

    <EmptyState v-else-if="error" title="Концентрация не загружена" :description="error" />

    <EmptyState
      v-else-if="contextsLoaded && availableContexts.length === 0"
      title="Концентрация рынка еще не рассчитана"
      description="Запустите расчет концентрации рынка или дождитесь планового обновления."
    />

    <MarketConcentrationSection
      v-else
      :concentration="marketConcentration"
      :loading="loading"
      :top-n="intelligence?.context.topN"
      :calculated-at-utc="intelligence?.calculatedAtUtc"
      :limitations="intelligence?.limitations ?? []"
      @open-products="openProductsModal"
    />

    <Teleport to="body">
      <div v-if="productsModalContext" class="concentration-products-modal" role="presentation">
        <button
          class="concentration-products-modal__backdrop"
          type="button"
          aria-label="Закрыть список карточек"
          @click="closeProductsModal"
        />
        <section
          class="concentration-products-modal__panel app-surface app-operator-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="concentration-products-title"
        >
          <header class="concentration-products-modal__header">
            <div>
              <h2 id="concentration-products-title">{{ productsModalContext.title }}</h2>
              <p>{{ formatNumber(modalProducts.length) }} карточек в выбранной группе</p>
            </div>
            <button
              class="concentration-products-modal__close"
              type="button"
              aria-label="Закрыть список карточек"
              @click="closeProductsModal"
            >
              ×
            </button>
          </header>

          <div class="concentration-products-modal__sort">
            <div class="concentration-products-modal__sort-group" aria-label="Сортировка карточек">
              <span>Сортировка</span>
              <div class="concentration-products-modal__sort-buttons">
                <button
                  v-for="option in modalSortOptions"
                  :key="option.key"
                  class="concentration-products-modal__sort-button"
                  :class="{ 'concentration-products-modal__sort-button--active': modalSortKey === option.key }"
                  type="button"
                  @click="setModalSortKey(option.key)"
                >
                  {{ option.label }}
                </button>
              </div>
            </div>
            <div class="concentration-products-modal__sort-group" aria-label="Порядок сортировки">
              <span>Порядок</span>
              <div class="concentration-products-modal__sort-buttons">
                <button
                  v-for="option in modalSortDirectionOptions"
                  :key="option.key"
                  class="concentration-products-modal__sort-button"
                  :class="{ 'concentration-products-modal__sort-button--active': modalSortDirection === option.key }"
                  type="button"
                  @click="setModalSortDirection(option.key)"
                >
                  {{ option.label }}
                </button>
              </div>
            </div>
          </div>

          <LoadingState v-if="productsModalLoading" label="Загружаем карточки группы..." />

          <EmptyState
            v-else-if="productsModalError"
            title="Карточки не загружены"
            :description="productsModalError"
          />

          <div v-else-if="paginatedModalProducts.length" class="concentration-products-modal__grid">
            <article
              v-for="point in paginatedModalProducts"
              :key="point.wbProductId"
              class="concentration-product-card"
              :class="{ 'concentration-product-card--disabled': !point.productRowId }"
              role="button"
              :tabindex="point.productRowId ? 0 : -1"
              @click="openProduct(point)"
              @keydown.enter.prevent="openProduct(point)"
              @keydown.space.prevent="openProduct(point)"
            >
              <img
                v-if="point.thumbnailUrl"
                :src="point.thumbnailUrl"
                :alt="productTitle(point)"
                loading="lazy"
              />
              <div v-else class="concentration-product-card__placeholder">Нет фото</div>
              <div class="concentration-product-card__body">
                <span>{{ point.brandName || 'Бренд не указан' }}</span>
                <strong>{{ productTitle(point) }}</strong>
                <dl>
                  <div>
                    <dt>Цена</dt>
                    <dd>{{ formatPrice(point.price) }}</dd>
                  </div>
                  <div>
                    <dt>Рейтинг</dt>
                    <dd>{{ formatNumber(point.rating) }}</dd>
                  </div>
                  <div>
                    <dt>Отзывы</dt>
                    <dd>{{ formatNumber(point.feedbackCount) }}</dd>
                  </div>
                  <div>
                    <dt>Остаток</dt>
                    <dd>{{ formatNumber(point.stock) }}</dd>
                  </div>
                  <div>
                    <dt>Позиция</dt>
                    <dd>{{ formatPosition(point.position) }}</dd>
                  </div>
                </dl>
                <p v-if="!point.productRowId">Нет детальной карточки</p>
              </div>
            </article>
          </div>

          <EmptyState
            v-else
            title="Карточки не найдены"
            description="В текущей выборке нет карточек для выбранного значения."
          />

          <footer v-if="modalTotalPages > 1" class="concentration-products-modal__pager">
            <Button
              class="concentration-products-modal__pager-nav"
              type="button"
              variant="secondary"
              :disabled="productsModalPage <= 1"
              @click="setModalPage(productsModalPage - 1)"
            >
              Назад
            </Button>
            <div class="concentration-products-modal__pager-pages" aria-label="Страницы карточек в группе">
              <template v-for="item in modalPaginationItems" :key="item">
                <span v-if="typeof item === 'string'" class="concentration-products-modal__pager-ellipsis" aria-hidden="true">…</span>
                <button
                  v-else
                  class="concentration-products-modal__pager-page"
                  :class="{ 'concentration-products-modal__pager-page--active': item === productsModalPage }"
                  type="button"
                  :aria-current="item === productsModalPage ? 'page' : undefined"
                  @click="setModalPage(item)"
                >
                  {{ item }}
                </button>
              </template>
            </div>
            <Button
              class="concentration-products-modal__pager-nav"
              type="button"
              variant="secondary"
              :disabled="productsModalPage >= modalTotalPages"
              @click="setModalPage(productsModalPage + 1)"
            >
              Далее
            </Button>
          </footer>
        </section>
      </div>
    </Teleport>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      layer="modal"
      @close="closeProduct"
    />
  </div>
</template>

<style scoped>
.market-concentration-page {
  display: grid;
  gap: var(--space-5);
}

.mi-controls {
  position: relative;
  z-index: 25;
  overflow: visible;
  padding: var(--space-4);
}

.mi-controls__fields {
  position: relative;
  overflow: visible;
  display: grid;
  grid-template-columns: minmax(16rem, 1fr) auto;
  gap: var(--space-3);
  align-items: end;
}

.concentration-products-modal {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: grid;
  place-items: center;
  padding: var(--space-4);
}

.concentration-products-modal__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: rgba(15, 23, 42, 0.62);
  cursor: pointer;
}

.concentration-products-modal__panel {
  position: relative;
  z-index: 1;
  display: grid;
  width: min(70rem, 100%);
  max-height: min(48rem, calc(100vh - 2rem));
  grid-template-rows: auto auto minmax(0, 1fr) auto;
  gap: var(--space-4);
  padding: var(--space-4);
  overflow: hidden;
}

.concentration-products-modal__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  align-items: flex-start;
}

.concentration-products-modal__header h2,
.concentration-products-modal__header p {
  margin: 0;
}

.concentration-products-modal__header h2 {
  color: var(--text-primary);
  font-size: 1.15rem;
}

.concentration-products-modal__header p {
  margin-top: var(--space-1);
  color: var(--text-muted);
}

.concentration-products-modal__close {
  width: 2rem;
  height: 2rem;
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  background: var(--surface-panel);
  color: var(--text-muted);
  cursor: pointer;
  font-size: 1.4rem;
  line-height: 1;
}

.concentration-products-modal__sort {
  display: grid;
  grid-template-columns: minmax(20rem, 1fr) auto;
  gap: var(--space-3);
  align-items: end;
}

.concentration-products-modal__sort-group {
  display: grid;
  gap: var(--space-2);
}

.concentration-products-modal__sort-group > span {
  color: var(--text-muted);
  font-size: 0.82rem;
  font-weight: 800;
}

.concentration-products-modal__sort-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}

.concentration-products-modal__sort-button {
  display: inline-flex;
  min-height: 2rem;
  align-items: center;
  justify-content: center;
  border: 1px solid rgb(249 115 22 / 0.18);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 0.8125rem;
  font-weight: 720;
  padding: 0 var(--space-3);
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, box-shadow 120ms ease;
}

.concentration-products-modal__sort-button:hover {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.09), transparent),
    var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.concentration-products-modal__sort-button--active {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.22), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 8px 20px rgb(249 115 22 / 0.08);
}

.concentration-products-modal__sort-button:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.concentration-products-modal__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
  gap: var(--space-3);
  overflow: auto;
  padding-right: var(--space-1);
}

.concentration-product-card {
  display: grid;
  grid-template-columns: 5rem minmax(0, 1fr);
  gap: var(--space-3);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  padding: var(--space-3);
  cursor: pointer;
}

.concentration-product-card:hover,
.concentration-product-card:focus-visible {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  outline: 0;
}

.concentration-product-card--disabled {
  cursor: not-allowed;
  opacity: 0.66;
}

.concentration-product-card--disabled:hover {
  border-color: var(--border-subtle);
  background: var(--surface-panel);
}

.concentration-product-card img,
.concentration-product-card__placeholder {
  width: 5rem;
  height: 6.5rem;
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  object-fit: cover;
}

.concentration-product-card__placeholder {
  display: grid;
  place-items: center;
  color: var(--text-muted);
  font-size: 0.78rem;
  text-align: center;
}

.concentration-product-card__body {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.concentration-product-card__body span,
.concentration-product-card__body p {
  color: var(--text-muted);
  font-size: 0.78rem;
}

.concentration-product-card__body strong {
  overflow: hidden;
  color: var(--text-primary);
  font-size: 0.9rem;
  text-overflow: ellipsis;
}

.concentration-product-card__body dl {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-1) var(--space-2);
  margin: 0;
}

.concentration-product-card__body div {
  min-width: 0;
}

.concentration-product-card__body dt {
  color: var(--text-muted);
  font-size: 0.68rem;
  font-weight: 900;
}

.concentration-product-card__body dd {
  margin: 0;
  color: var(--text-primary);
  font-weight: 800;
}

.concentration-product-card__body p {
  margin: 0;
  color: var(--accent-ember-text-strong);
  font-weight: 800;
}

.concentration-products-modal__pager {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: var(--space-3);
  align-items: center;
}

.concentration-products-modal__pager-pages {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.25rem;
}

.concentration-products-modal__pager-page,
.concentration-products-modal__pager-ellipsis {
  display: inline-flex;
  min-width: 2rem;
  height: 2rem;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-sm);
  font-size: 0.8125rem;
}

.concentration-products-modal__pager-page {
  border: 1px solid rgb(249 115 22 / 0.18);
  background: var(--surface-control);
  color: var(--color-text-muted);
  cursor: pointer;
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, box-shadow 120ms ease;
}

.concentration-products-modal__pager-page:hover {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.09), transparent),
    var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.concentration-products-modal__pager-page--active {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.22), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 8px 20px rgb(249 115 22 / 0.08);
}

.concentration-products-modal__pager-page:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.concentration-products-modal__pager-ellipsis {
  color: var(--text-muted);
}

.concentration-products-modal__pager-nav {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.14), rgb(249 115 22 / 0.05)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  font-weight: 720;
}

.concentration-products-modal__pager-nav:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.2), rgb(249 115 22 / 0.08)),
    var(--color-surface-hover);
}

.concentration-products-modal__pager-nav:disabled {
  border-color: var(--color-border);
  background: var(--surface-control);
  color: var(--color-text-muted);
  opacity: 0.58;
}

@media (max-width: 760px) {
  .mi-controls__fields {
    grid-template-columns: 1fr;
  }

  .concentration-products-modal {
    padding: var(--space-2);
  }

  .concentration-products-modal__panel {
    max-height: calc(100vh - 1rem);
  }

  .concentration-products-modal__sort {
    grid-template-columns: 1fr;
  }

  .concentration-products-modal__grid {
    grid-template-columns: 1fr;
  }
}
</style>
