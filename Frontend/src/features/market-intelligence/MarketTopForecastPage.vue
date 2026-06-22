<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';

import { getPublicTopForecast } from './marketIntelligence.api';
import {
  buildMarketIntelligenceParams,
  isMarketIntelligenceSubcategory,
  marketIntelligenceContexts,
  marketIntelligenceDefaultRegionDest,
  marketIntelligenceDefaultSort,
  resolveMarketIntelligenceContext
} from './marketIntelligence.contexts';
import type {
  PublicMarketIntelligenceParams,
  PublicTopForecastItem,
  PublicTopForecastResponse
} from './marketIntelligence.types';

const PAGE_SIZE = 14;
const MIN_PROBABILITY = 0.7;

const route = useRoute();
const router = useRouter();

const selectedSubcategory = ref(readInitialSubcategory());
const forecast = ref<PublicTopForecastResponse | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const selectedProduct = ref<ParserProductListItem | null>(null);

const page = computed(() => readNumberQuery('page') ?? 1);
const selectedContext = computed(() => resolveMarketIntelligenceContext(selectedSubcategory.value));
const nicheOptions = computed(() => marketIntelligenceContexts.map((context) => context.sourceSubcategory));
const requestParams = computed<PublicMarketIntelligenceParams>(() => buildMarketIntelligenceParams(
  selectedContext.value,
  {
    sourceRegionDest: readStringQuery('sourceRegionDest') ?? marketIntelligenceDefaultRegionDest,
    sort: readStringQuery('sort') ?? marketIntelligenceDefaultSort
  }
));
const totalPages = computed(() => Math.max(1, forecast.value?.totalPages ?? 1));
const paginationItems = computed(() => buildPaginationItems(page.value, totalPages.value));

onMounted(() => {
  void refresh();
});

watch(selectedSubcategory, () => {
  void applySubcategorySelection();
});

async function refresh(): Promise<void> {
  loading.value = true;
  error.value = null;
  selectedProduct.value = null;

  try {
    forecast.value = await getPublicTopForecast({
      ...requestParams.value,
      page: page.value,
      pageSize: PAGE_SIZE,
      minProbability: MIN_PROBABILITY
    });
  } catch (requestError) {
    forecast.value = null;
    error.value = getProblemMessage(requestError, 'Не удалось загрузить прогноз топа.');
  } finally {
    loading.value = false;
  }
}

async function applySubcategorySelection(): Promise<void> {
  if (!isMarketIntelligenceSubcategory(selectedSubcategory.value)) {
    selectedSubcategory.value = marketIntelligenceContexts[0].sourceSubcategory;
    return;
  }

  await router.replace({
    query: {
      ...route.query,
      sourceSubcategory: selectedSubcategory.value,
      page: '1'
    }
  });
  await refresh();
}

async function setPage(nextPage: number): Promise<void> {
  const normalized = Math.min(Math.max(nextPage, 1), totalPages.value);
  if (normalized === page.value) {
    return;
  }

  await router.replace({
    query: {
      ...route.query,
      page: String(normalized)
    }
  });
  window.scrollTo({ top: 0, behavior: 'smooth' });
  await refresh();
}

function openProduct(item: PublicTopForecastItem): void {
  if (!item.productRowId) {
    return;
  }

  selectedProduct.value = toParserProductListItem(item);
}

function toParserProductListItem(item: PublicTopForecastItem): ParserProductListItem {
  const context = forecast.value?.context;
  const observedAt = forecast.value?.run?.calculatedAtUtc ?? new Date().toISOString();
  const position = item.currentPosition;

  return {
    id: item.productRowId!,
    parserRunId: '',
    parsedAtUtc: observedAt,
    wbProductId: item.wbProductId,
    wbRootId: item.wbRootId,
    name: productTitle(item),
    brandName: item.brandName,
    sellerName: item.sellerName,
    priceRegular: null,
    priceDiscounted: item.price,
    priceWbWallet: null,
    discountPercent: null,
    totalQuantity: item.stock,
    ratingRounded: null,
    reviewRating: item.rating,
    feedbackCount: item.feedbackCount,
    sourceCategory: context?.sourceCategory ?? selectedContext.value.sourceCategory,
    sourceSubcategory: context?.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
    sourceQuery: context?.query ?? selectedContext.value.query,
    thumbnailUrl: item.image,
    rank: position === null ? null : {
      absolutePosition: position,
      page: Math.max(1, Math.ceil(position / 100)),
      positionOnPage: ((position - 1) % 100) + 1,
      query: context?.query ?? selectedContext.value.query,
      sourceCategory: context?.sourceCategory ?? selectedContext.value.sourceCategory,
      sourceSubcategory: context?.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
      sourceRegionDest: context?.sourceRegionDest ?? marketIntelligenceDefaultRegionDest,
      sort: context?.sort ?? marketIntelligenceDefaultSort,
      observedAtUtc: observedAt,
      parserRunId: '',
      rankContextId: '',
      contextsCount: 1
    },
    position: {
      state: item.currentPositionState ?? (position === null ? 'unknown' : 'observed'),
      absolutePosition: position,
      observedRangeLimit: item.observedRangeLimit ?? context?.topN ?? 1000,
      query: context?.query ?? selectedContext.value.query,
      sourceCategory: context?.sourceCategory ?? selectedContext.value.sourceCategory,
      sourceSubcategory: context?.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
      observedAtUtc: position === null ? null : observedAt
    }
  };
}

function buildPaginationItems(currentPage: number, total: number): Array<number | string> {
  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  if (currentPage <= 4) {
    return [1, 2, 3, 4, 5, 'end-ellipsis', total];
  }

  if (currentPage >= total - 3) {
    return [1, 'start-ellipsis', total - 4, total - 3, total - 2, total - 1, total];
  }

  return [1, 'start-ellipsis', currentPage - 1, currentPage, currentPage + 1, 'end-ellipsis', total];
}

function productTitle(item: PublicTopForecastItem): string {
  return item.name || `WB ${item.wbProductId}`;
}

function formatMoney(value: number | null): string {
  return value === null ? 'Нет данных' : `${Math.round(value).toLocaleString('ru-RU')} ₽`;
}

function formatNumber(value: number | null): string {
  return value === null ? 'Нет данных' : value.toLocaleString('ru-RU');
}

function formatRating(value: number | null): string {
  return value === null || value <= 0 ? 'Нет данных' : value.toLocaleString('ru-RU', { maximumFractionDigits: 1 });
}

function formatPosition(position: number | null, state: string | null, limit: number | null): string {
  if (position !== null) {
    return `#${position.toLocaleString('ru-RU')}`;
  }

  if (state === 'beyondObservedRange' || limit) {
    return `>${(limit ?? 1000).toLocaleString('ru-RU')}`;
  }

  return 'Нет данных';
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`;
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function readInitialSubcategory(): string {
  const queryValue = readStringQuery('sourceSubcategory');
  return isMarketIntelligenceSubcategory(queryValue)
    ? queryValue
    : marketIntelligenceContexts[0].sourceSubcategory;
}

function readStringQuery(key: string): string | null {
  const value = route.query[key];
  return typeof value === 'string' && value.trim() ? value : null;
}

function readNumberQuery(key: string): number | null {
  const value = readStringQuery(key);
  if (!value) {
    return null;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}
</script>

<template>
  <div class="top-forecast-page">
    <PageHeader
      title="Прогноз топа"
      description="Модель оценивает вероятность попадания карточки в профиль top-100 текущей выдачи. Позиция не используется как признак и нужна только для обучения и отображения."
    />

    <section class="forecast-controls">
      <MarketFilterSelect
        v-model="selectedSubcategory"
        class="forecast-controls__select"
        label="Ниша"
        :options="nicheOptions"
        placeholder="Выберите нишу"
        search-placeholder="Найти нишу"
        :clearable="false"
      />
    </section>

    <LoadingState v-if="loading" :rows="6" />

    <EmptyState
      v-else-if="error"
      title="Прогноз топа не загружен"
      :description="error"
    />

    <EmptyState
      v-else-if="forecast && !forecast.items.length"
      title="Карточки для прогноза еще не найдены"
      description="После ближайшего расчета здесь появятся товары с вероятностью входа в топ выше 70%."
    />

    <section v-else-if="forecast" class="forecast-content">
      <header class="forecast-summary">
        <span>Выборка: <strong>{{ forecast.run?.sampleSize?.toLocaleString('ru-RU') ?? 0 }}</strong></span>
        <span>Прогнозов: <strong>{{ forecast.totalCount.toLocaleString('ru-RU') }}</strong></span>
        <span>Рассчитано: <strong>{{ formatDate(forecast.run?.calculatedAtUtc) }}</strong></span>
      </header>

      <div class="forecast-grid">
        <article
          v-for="item in forecast.items"
          :key="item.wbProductId"
          class="forecast-card"
          :class="{ 'forecast-card--clickable': item.productRowId }"
          @click="openProduct(item)"
        >
          <div class="forecast-card__image">
            <img v-if="item.image" :src="item.image" :alt="productTitle(item)" loading="lazy" />
            <span v-else>Нет фото</span>
          </div>

          <div class="forecast-card__body">
            <span class="forecast-card__category">{{ forecast.context.sourceSubcategory }}</span>
            <h2>{{ productTitle(item) }}</h2>
            <p>{{ item.brandName || 'Бренд не указан' }} · {{ item.sellerName || 'Продавец не указан' }}</p>

            <div class="forecast-card__positions">
              <div>
                <span>Текущая позиция</span>
                <strong>{{ formatPosition(item.currentPosition, item.currentPositionState, item.observedRangeLimit) }}</strong>
              </div>
              <div>
                <span>Прогнозная позиция</span>
                <strong>{{ item.predictedPosition ? `#${item.predictedPosition}` : 'Нет данных' }}</strong>
              </div>
              <div>
                <span>Уверенность прогноза</span>
                <strong>{{ formatPercent(item.top100Probability) }}</strong>
              </div>
            </div>

            <dl class="forecast-card__metrics">
              <div>
                <dt>Цена</dt>
                <dd>{{ formatMoney(item.price) }}</dd>
              </div>
              <div>
                <dt>Рейтинг</dt>
                <dd>{{ formatRating(item.rating) }}</dd>
              </div>
              <div>
                <dt>Отзывы</dt>
                <dd>{{ formatNumber(item.feedbackCount) }}</dd>
              </div>
              <div>
                <dt>Остаток</dt>
                <dd>{{ formatNumber(item.stock) }}</dd>
              </div>
            </dl>
          </div>
        </article>
      </div>

      <footer class="forecast-pager">
        <Button variant="secondary" :disabled="page <= 1" @click="setPage(page - 1)">Назад</Button>
        <div class="forecast-pager__pages">
          <template v-for="item in paginationItems" :key="item">
            <span v-if="typeof item === 'string'" class="forecast-pager__ellipsis">…</span>
            <button
              v-else
              class="forecast-pager__page"
              :class="{ 'forecast-pager__page--active': item === page }"
              type="button"
              @click="setPage(item)"
            >
              {{ item }}
            </button>
          </template>
        </div>
        <Button variant="secondary" :disabled="page >= totalPages" @click="setPage(page + 1)">Далее</Button>
      </footer>
    </section>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
  </div>
</template>

<style scoped>
.top-forecast-page {
  display: grid;
  gap: var(--space-4);
}

.forecast-controls,
.forecast-content {
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-lg);
  background: var(--operator-panel-bg);
  box-shadow: var(--shadow-panel);
}

.forecast-controls {
  display: grid;
  grid-template-columns: minmax(14rem, 1fr);
  gap: var(--space-3);
  padding: var(--space-3);
}

.forecast-controls__select {
  max-width: 42rem;
}

.forecast-content {
  padding: var(--space-4);
}

.forecast-summary,
.forecast-pager,
.forecast-pager__pages {
  display: flex;
  align-items: center;
}

.forecast-summary {
  flex-wrap: wrap;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
  color: var(--color-text-muted);
}

.forecast-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}

.forecast-card {
  display: grid;
  grid-template-columns: 7rem minmax(0, 1fr);
  gap: var(--space-3);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--surface-card);
  padding: var(--space-3);
}

.forecast-card--clickable {
  cursor: pointer;
}

.forecast-card--clickable:hover {
  border-color: var(--operator-border-strong);
  background: var(--surface-card-hover);
}

.forecast-card__image {
  display: grid;
  min-height: 9rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-muted);
  color: var(--color-text-muted);
  font-size: 0.8rem;
}

.forecast-card__image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.forecast-card__body {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.forecast-card__category,
.forecast-card__body p,
.forecast-card__positions span,
.forecast-card__metrics dt {
  color: var(--color-text-muted);
  font-size: 0.78rem;
}

.forecast-card h2 {
  margin: 0;
  font-size: 1rem;
  line-height: 1.25;
}

.forecast-card__body p {
  margin: 0;
}

.forecast-card__positions,
.forecast-card__metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  column-gap: var(--space-4);
  row-gap: var(--space-2);
}

.forecast-card__positions div,
.forecast-card__metrics div {
  display: grid;
  gap: 0.15rem;
  align-content: start;
  min-width: 0;
}

.forecast-card__metrics {
  margin: 0;
}

.forecast-card__metrics dd {
  margin: 0;
  font-weight: 700;
}

.forecast-pager {
  justify-content: center;
  gap: var(--space-3);
  margin-top: var(--space-5);
}

.forecast-pager__pages {
  gap: var(--space-2);
}

.forecast-pager__page {
  min-width: 2.1rem;
  height: 2.1rem;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--color-text);
  cursor: pointer;
  font-weight: 700;
}

.forecast-pager__page--active {
  border-color: var(--color-accent);
  background: var(--color-accent-soft);
  color: var(--color-accent-strong);
}

.forecast-pager__ellipsis {
  color: var(--color-text-muted);
}

@media (max-width: 980px) {
  .forecast-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .forecast-card {
    grid-template-columns: 1fr;
  }

  .forecast-card__positions,
  .forecast-card__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
