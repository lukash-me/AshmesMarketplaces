<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { ChevronLeft, ChevronRight, RefreshCw, Search } from 'lucide-vue-next';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import { recalculateWorkspaceOverview } from '@/features/overview/workspaceOverview.api';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import { recalculateHotProductsRecommendations } from '@/features/parser-products/hotProductsRecommendations.api';
import { useActiveWorkspace } from '@/features/workspace-market-products/useActiveWorkspace';

import {
  getParserTestingLogisticsSummary,
  getParserTestingProduct,
  getParserTestingProducts
} from './parserTesting.api';
import type {
  ParserTestingDeliveryDestination,
  ParserTestingDeliveryLocationEstimate,
  ParserTestingLogisticsSummary,
  ParserTestingProduct,
  ParserTestingProductDetail
} from './parserTesting.types';

const TEST_LABEL = 'delivery-profile-100';
const PAGE_SIZE = 20;
const DELIVERY_CITY_ORDER = [
  'Москва',
  'Санкт-Петербург',
  'Казань',
  'Краснодар',
  'Екатеринбург',
  'Новосибирск',
  'Хабаровск'
];
const MS_PER_DAY = 24 * 60 * 60 * 1000;
const MOSCOW_OFFSET_MS = 3 * 60 * 60 * 1000;
const MONTH_NAMES = [
  'января',
  'февраля',
  'марта',
  'апреля',
  'мая',
  'июня',
  'июля',
  'августа',
  'сентября',
  'октября',
  'ноября',
  'декабря'
];

const props = withDefaults(defineProps<{
  title?: string;
  description?: string;
  emptyTitle?: string;
  emptyDescription?: string;
  errorTitle?: string;
  errorMessage?: string;
  testRunsOnly?: boolean;
  testLabel?: string;
  showAnalysisActions?: boolean;
}>(), {
  title: 'Тестирование',
  description: 'Изолированная проверка логистики WB по контрольным ПВЗ.',
  emptyTitle: 'Товары с логистическим профилем не найдены',
  emptyDescription: 'Проверьте, что тестовый parser pipeline запущен с delivery profile и меткой delivery-profile-100.',
  errorTitle: 'Не удалось загрузить тестовую логистику',
  errorMessage: 'Не удалось загрузить тестовую логистику.',
  testRunsOnly: true,
  testLabel: TEST_LABEL,
  showAnalysisActions: true
});

const auth = useAuthStore();
const workspace = useActiveWorkspace();
const searchDraft = ref('');
const search = ref('');
const page = ref(1);
const rows = ref<ParserTestingProduct[]>([]);
const detailsById = ref<Record<string, ParserTestingProductDetail>>({});
const totalCount = ref(0);
const summary = ref<ParserTestingLogisticsSummary | null>(null);
const loading = ref(false);
const detailsLoading = ref(false);
const error = ref('');
const analysisActionError = ref('');
const hotProductsRefreshing = ref(false);
const overviewRefreshing = ref(false);
const selected = ref<ParserTestingProduct | null>(null);
let loadVersion = 0;

const isGuestTestingMode = computed(() => props.showAnalysisActions && !auth.isAuthenticated);
const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)));

watch(
  () => [page.value, search.value, isGuestTestingMode.value] as const,
  () => {
    if (isGuestTestingMode.value) {
      rows.value = [];
      detailsById.value = {};
      totalCount.value = 0;
      summary.value = null;
      loading.value = false;
      return;
    }

    void loadRows();
  },
  { immediate: true }
);

async function loadRows() {
  if (isGuestTestingMode.value) {
    return;
  }

  const version = ++loadVersion;
  loading.value = true;
  error.value = '';

  try {
    const params = {
      page: page.value,
      pageSize: PAGE_SIZE,
      sort: '-parsedAtUtc',
      search: search.value || undefined,
      requireDeliveryProfile: true as const
    };
    const scopedParams = props.testRunsOnly
      ? {
          ...params,
          testRunsOnly: true,
          testLabel: props.testLabel
        }
      : params;
    const summaryParams = props.testRunsOnly
      ? {
          search: search.value || undefined,
          testRunsOnly: true,
          testLabel: props.testLabel
        }
      : {
          search: search.value || undefined
        };

    const [listResponse, summaryResponse] = await Promise.all([
      getParserTestingProducts(scopedParams),
      getParserTestingLogisticsSummary(summaryParams)
    ]);

    if (version !== loadVersion) {
      return;
    }

    rows.value = listResponse.items;
    totalCount.value = listResponse.totalCount;
    summary.value = summaryResponse;
    await loadDetails(listResponse.items, version);
  } catch (err) {
    if (version === loadVersion) {
      rows.value = [];
      detailsById.value = {};
      totalCount.value = 0;
      summary.value = null;
      error.value = getProblemMessage(err, props.errorMessage);
    }
  } finally {
    if (version === loadVersion) {
      loading.value = false;
    }
  }
}

async function loadDetails(items: ParserTestingProduct[], version: number) {
  detailsLoading.value = true;
  detailsById.value = {};

  try {
    const pairs = await Promise.all(
      items.map(async (item) => {
        try {
          return [item.id, await getParserTestingProduct(item.id)] as const;
        } catch {
          return [item.id, null] as const;
        }
      })
    );

    if (version !== loadVersion) {
      return;
    }

    detailsById.value = Object.fromEntries(
      pairs.filter((pair): pair is readonly [string, ParserTestingProductDetail] => pair[1] !== null)
    );
  } finally {
    if (version === loadVersion) {
      detailsLoading.value = false;
    }
  }
}

function applySearch() {
  search.value = searchDraft.value.trim();
  page.value = 1;
}

function resetSearch() {
  searchDraft.value = '';
  search.value = '';
  page.value = 1;
}

async function refreshHotProducts(): Promise<void> {
  if (isGuestTestingMode.value || hotProductsRefreshing.value) {
    return;
  }

  hotProductsRefreshing.value = true;
  analysisActionError.value = '';
  try {
    await recalculateHotProductsRecommendations({
      maxProducts: 100000,
      maxRecommendations: 1000,
      minProductsForScoring: 5,
      forceRecalculate: true
    });
  } catch (requestError) {
    analysisActionError.value = getProblemMessage(requestError, 'Не удалось обновить перспективные товары.');
  } finally {
    hotProductsRefreshing.value = false;
  }
}

async function refreshOverview(): Promise<void> {
  const workspaceId = workspace.activeWorkspaceId.value;
  if (isGuestTestingMode.value || !workspaceId || overviewRefreshing.value) {
    return;
  }

  overviewRefreshing.value = true;
  analysisActionError.value = '';
  try {
    await recalculateWorkspaceOverview(workspaceId);
  } catch (requestError) {
    analysisActionError.value = getProblemMessage(requestError, 'Не удалось обновить обзор.');
  } finally {
    overviewRefreshing.value = false;
  }
}

function detailFor(row: ParserTestingProduct): ParserTestingProductDetail | null {
  return detailsById.value[row.id] ?? null;
}

function destinationTitle(destination: ParserTestingDeliveryDestination): string {
  return (
    destination.deliveryDestinationCity?.trim()
    || destination.deliveryDestinationName?.trim()
    || 'Контрольная точка WB'
  );
}

function sortedDestinations(row: ParserTestingProduct): ParserTestingDeliveryDestination[] {
  const destinations = detailFor(row)?.deliveryProfile?.destinations ?? [];
  return destinations
    .map((destination, index) => ({ destination, index }))
    .sort((left, right) => (
      cityOrderIndex(left.destination) - cityOrderIndex(right.destination)
      || left.index - right.index
    ))
    .map((item) => item.destination);
}

function cityOrderIndex(destination: ParserTestingDeliveryDestination): number {
  const title = destinationTitle(destination).toLocaleLowerCase('ru-RU');
  const index = DELIVERY_CITY_ORDER.findIndex((city) => {
    const normalizedCity = city.toLocaleLowerCase('ru-RU');
    return title === normalizedCity || title.includes(normalizedCity);
  });

  return index === -1 ? Number.MAX_SAFE_INTEGER : index;
}

function deliveryReferenceDate(row: ParserTestingProduct): Date | null {
  const timestamps = sortedDestinations(row)
    .map((destination) => parseDate(destination.visibleDeliveryObservedAtUtc || destination.observedAtUtc))
    .filter((date): date is Date => date !== null);

  if (timestamps.length === 0) {
    return null;
  }

  return new Date(Math.max(...timestamps.map((date) => date.getTime())));
}

function visibleDeliveryText(row: ParserTestingProduct, destination: ParserTestingDeliveryDestination): string {
  if (destination.totalQuantityObserved === 0) {
    return 'Нет в наличии';
  }

  const referenceDate = deliveryReferenceDate(row);
  const deliveryDate = deliveryDateForDestination(destination, referenceDate);
  if (deliveryDate && referenceDate) {
    return formatDeliveryDateFromReference(deliveryDate, referenceDate, deliverySourceSuffix(destination));
  }

  if (deliveryDate) {
    return [formatDate(deliveryDate.toISOString()), deliverySourceSuffix(destination)].filter(Boolean).join(', ');
  }

  if (
    ['calculated', 'estimated', 'success'].includes(destination.visibleDeliveryStatus || '')
    && destination.visibleDeliveryLabel?.trim()
  ) {
    return destination.visibleDeliveryLabel.trim();
  }

  return 'Дата не рассчитана';
}

function deliveryTone(row: ParserTestingProduct, destination: ParserTestingDeliveryDestination): string {
  if (destination.totalQuantityObserved === 0) {
    return 'neutral';
  }

  const referenceDate = deliveryReferenceDate(row);
  const deliveryDate = deliveryDateForDestination(destination, referenceDate);
  if (!deliveryDate || !referenceDate) {
    return 'neutral';
  }

  const deltaDays = daysBetweenDates(referenceDate, deliveryDate);
  if (deltaDays <= 2) {
    return 'fast';
  }

  if (deltaDays <= 6) {
    return 'medium';
  }

  return 'slow';
}

function deliveryDateForDestination(
  destination: ParserTestingDeliveryDestination,
  referenceDate: Date | null
): Date | null {
  const explicitDate = parseDate(destination.visibleDeliveryDate);
  if (explicitDate) {
    return explicitDate;
  }

  return parseDeliveryDateFromLabel(destination.visibleDeliveryLabel, referenceDate);
}

function parseDeliveryDateFromLabel(value: string | null | undefined, referenceDate: Date | null): Date | null {
  if (!value || !referenceDate) {
    return null;
  }

  const firstPart = value.split(',')[0]?.trim().toLocaleLowerCase('ru-RU');
  if (!firstPart) {
    return null;
  }

  const referenceParts = moscowDateParts(referenceDate);
  if (firstPart === 'сегодня') {
    return dateFromMoscowParts(referenceParts.year, referenceParts.month, referenceParts.day);
  }

  if (firstPart === 'завтра') {
    return addDays(dateFromMoscowParts(referenceParts.year, referenceParts.month, referenceParts.day), 1);
  }

  if (firstPart === 'послезавтра') {
    return addDays(dateFromMoscowParts(referenceParts.year, referenceParts.month, referenceParts.day), 2);
  }

  const dateMatch = firstPart.match(/^(\d{1,2})\s+([а-яё]+)$/i);
  if (!dateMatch) {
    return null;
  }

  const day = Number(dateMatch[1]);
  const month = MONTH_NAMES.indexOf(dateMatch[2].toLocaleLowerCase('ru-RU'));
  if (!Number.isInteger(day) || day < 1 || day > 31 || month < 0) {
    return null;
  }

  let parsedDate = dateFromMoscowParts(referenceParts.year, month, day);
  if (moscowDayNumber(parsedDate) < moscowDayNumber(referenceDate)) {
    parsedDate = dateFromMoscowParts(referenceParts.year + 1, month, day);
  }

  return parsedDate;
}

function formatDeliveryDateFromReference(deliveryDate: Date, referenceDate: Date, suffix: string): string {
  const deltaDays = daysBetweenDates(referenceDate, deliveryDate);
  const deliveryDateParts = moscowDateParts(deliveryDate);
  const dayLabel = deltaDays === 0
    ? 'Сегодня'
    : deltaDays === 1
      ? 'Завтра'
      : deltaDays === 2
        ? 'Послезавтра'
        : `${deliveryDateParts.day} ${MONTH_NAMES[deliveryDateParts.month]}`;

  return [dayLabel, suffix].filter(Boolean).join(', ');
}

function deliverySourceSuffix(destination: ParserTestingDeliveryDestination): string {
  const label = destination.visibleDeliveryLabel?.trim();
  if (!label) {
    return '';
  }

  return label.split(',').slice(1).join(',').trim();
}

function daysBetweenDates(referenceDate: Date, deliveryDate: Date): number {
  const referenceMidnight = moscowDayNumber(referenceDate);
  const deliveryMidnight = moscowDayNumber(deliveryDate);

  return Math.max(0, deliveryMidnight - referenceMidnight);
}

function moscowDayNumber(date: Date): number {
  return Math.floor((date.getTime() + MOSCOW_OFFSET_MS) / MS_PER_DAY);
}

function moscowDateParts(date: Date): { year: number; day: number; month: number } {
  const moscowDate = new Date(date.getTime() + MOSCOW_OFFSET_MS);
  return {
    year: moscowDate.getUTCFullYear(),
    day: moscowDate.getUTCDate(),
    month: moscowDate.getUTCMonth()
  };
}

function dateFromMoscowParts(year: number, month: number, day: number): Date {
  return new Date(Date.UTC(year, month, day) - MOSCOW_OFFSET_MS);
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getTime() + days * MS_PER_DAY);
}

function parseDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? date : null;
}

function availabilityStockText(destination: ParserTestingDeliveryDestination): string {
  return destination.totalQuantityObserved === 0
    ? 'Нет в наличии'
    : formatNumber(destination.totalQuantityObserved);
}

function locationTitle(estimate: ParserTestingDeliveryLocationEstimate | null | undefined): string {
  if (!estimate || estimate.status !== 'estimated' || !estimate.zoneTitle) {
    return 'Недостаточно данных для оценки';
  }

  return estimate.zoneTitle;
}

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : new Intl.NumberFormat('ru-RU').format(value);
}

function formatMoney(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' }).format(date)
    : 'Нет данных';
}

</script>

<template>
  <div class="parser-testing-page">
    <PageHeader
      :title="props.title"
      :description="props.description"
    />

    <AuthRequiredState
      v-if="isGuestTestingMode"
      description="Тестовые действия пересчитывают персональную аналитику и доступны после входа. Авторизуйтесь, чтобы вручную обновить Перспективные товары или Обзор."
    />

    <section v-if="!isGuestTestingMode" class="testing-toolbar app-operator-panel">
      <div class="testing-toolbar__search">
        <Search :size="17" />
        <input
          v-model="searchDraft"
          class="app-input"
          type="search"
          placeholder="WB id, товар, бренд или продавец"
          @keydown.enter="applySearch"
        />
      </div>
      <Button variant="primary" @click="applySearch">Применить</Button>
      <Button variant="secondary" @click="resetSearch">Сбросить</Button>
      <div v-if="props.showAnalysisActions" class="testing-toolbar__analysis-actions">
        <Button variant="secondary" :loading="hotProductsRefreshing" @click="refreshHotProducts">
          <RefreshCw :size="15" />
          Обновить Перспективные товары
        </Button>
        <Button
          variant="secondary"
          :loading="overviewRefreshing"
          :disabled="!workspace.activeWorkspaceId.value"
          @click="refreshOverview"
        >
          <RefreshCw :size="15" />
          Обновить Обзор
        </Button>
      </div>
      <p v-if="analysisActionError" class="testing-toolbar__error">{{ analysisActionError }}</p>
    </section>

    <LoadingState v-if="!isGuestTestingMode && loading" class="app-surface" />
    <EmptyState
      v-else-if="!isGuestTestingMode && error"
      class="app-surface"
      :title="props.errorTitle"
      :description="error"
    />
    <EmptyState
      v-else-if="!isGuestTestingMode && rows.length === 0"
      class="app-surface"
      :title="props.emptyTitle"
      :description="props.emptyDescription"
    />

    <section v-else-if="!isGuestTestingMode" class="testing-list">
      <article v-for="row in rows" :key="row.id" class="testing-card app-operator-card">
        <header class="testing-card__header">
          <button class="testing-card__media" type="button" @click="selected = row">
            <MarketProductImage :src="row.thumbnailUrl" :alt="row.name" />
          </button>

          <div class="testing-card__identity">
            <div class="testing-card__badges">
              <Badge tone="info">{{ row.sourceSubcategory || 'Ниша не указана' }}</Badge>
            </div>
            <h2>{{ row.name }}</h2>
            <p>{{ row.brandName || 'Бренд не указан' }} / {{ row.sellerName || 'Продавец не указан' }}</p>
            <code>WB {{ row.wbProductId }}</code>
          </div>

          <div class="testing-card__metrics">
            <div class="app-operator-metric app-operator-value-metric testing-price-metric">
              <span>Цена</span>
              <strong>{{ formatMoney(row.priceDiscounted) }}</strong>
            </div>
          </div>
        </header>

        <section class="testing-card__section">
          <span v-if="detailsLoading" class="testing-card__muted">Загружаем профиль...</span>

          <div class="location-estimate">
            <div class="location-estimate__main">
              <span>Вероятная зона товара</span>
              <strong>{{ locationTitle(detailFor(row)?.deliveryProfile?.locationEstimate) }}</strong>
            </div>
          </div>

          <div v-if="detailFor(row)?.deliveryProfile?.destinations?.length" class="availability-table-wrap">
            <table class="availability-table">
              <thead>
                <tr>
                  <th>Город</th>
                  <th>Дата доставки</th>
                  <th>Остаток</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="destination in sortedDestinations(row)"
                  :key="`${destination.destination}-${destination.deliveryDestinationName}`"
                >
                  <td>{{ destinationTitle(destination) }}</td>
                  <td>
                    <span
                      class="availability-date"
                      :class="`availability-date--${deliveryTone(row, destination)}`"
                    >
                      {{ visibleDeliveryText(row, destination) }}
                    </span>
                  </td>
                  <td>{{ availabilityStockText(destination) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <p v-else class="testing-card__muted">
            Данные доступности для карточки еще загружаются или не найдены.
          </p>
        </section>

        <footer class="testing-card__footer">
          <Button variant="secondary" @click="selected = row">Открыть карточку</Button>
        </footer>
      </article>

      <footer class="testing-pagination app-operator-toolbar">
        <Button variant="secondary" :disabled="page <= 1" @click="page -= 1">
          <ChevronLeft :size="16" />
          Назад
        </Button>
        <span>Страница {{ page }} из {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="page += 1">
          Далее
          <ChevronRight :size="16" />
        </Button>
      </footer>
    </section>

    <ParserProductDetailDrawer :open="Boolean(selected)" :product="selected" @close="selected = null" />
  </div>
</template>

<style scoped>
.parser-testing-page,
.testing-list {
  display: grid;
  gap: var(--space-4);
}

.testing-toolbar {
  padding: var(--space-4);
}

.testing-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
}

.testing-toolbar__search {
  display: flex;
  flex: 1 1 24rem;
  align-items: center;
  gap: var(--space-2);
  min-width: min(100%, 18rem);
  border: 1px solid var(--accent-primary-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  padding: 0 var(--space-3);
  color: var(--accent-ember-text-strong);
}

.testing-toolbar__search input {
  min-height: 2.45rem;
  width: 100%;
  border: 0;
  background: transparent;
}

.testing-toolbar__analysis-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.testing-toolbar__error {
  flex-basis: 100%;
  margin: 0;
  color: var(--state-danger);
  font-size: 0.9rem;
}

.testing-card {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.testing-card__header {
  display: grid;
  grid-template-columns: 7.5rem minmax(0, 1fr);
  gap: var(--space-4);
}

.testing-card__media {
  display: grid;
  aspect-ratio: 3 / 4;
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: 0;
}

.testing-card__media :deep(.market-image) {
  width: 100%;
  height: 100%;
}

.testing-card__identity {
  display: grid;
  align-content: start;
  gap: var(--space-2);
  min-width: 0;
}

.testing-card__badges,
.testing-card__footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.testing-card__identity h2 {
  margin: 0;
  color: var(--color-text);
  font-size: 1.05rem;
  line-height: 1.3;
}

.testing-card__identity p,
.testing-card__identity code,
.testing-card__muted,
.testing-card__footer,
.testing-pagination {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.testing-card__metrics {
  display: grid;
  grid-column: 1 / -1;
  grid-template-columns: minmax(10.5rem, 14rem);
  gap: var(--space-2);
  align-self: start;
}

.testing-price-metric {
  position: relative;
  isolation: isolate;
  overflow: hidden;
}

.testing-card__section {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-metric-bg);
  padding: var(--space-4);
}

.location-estimate {
  display: grid;
  gap: var(--space-3);
  align-items: start;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  padding: var(--space-3);
}

.location-estimate__main {
  display: grid;
  gap: 0.25rem;
}

.location-estimate__main span {
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.location-estimate__main strong {
  color: var(--color-text);
  font-size: 1rem;
}

.availability-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--surface-card);
}

.availability-table {
  width: 100%;
  min-width: 30rem;
  border-collapse: collapse;
}

.availability-table th,
.availability-table td {
  border-bottom: 1px solid var(--operator-border-muted);
  padding: 0.48rem 0.8rem;
  text-align: left;
  vertical-align: middle;
}

.availability-table th {
  background: var(--operator-metric-bg);
  color: var(--color-text-muted);
  font-size: 0.9rem;
  font-weight: 800;
}

.availability-table td {
  color: var(--color-text);
  font-size: 0.9rem;
}

.availability-table tbody tr:nth-child(odd) td {
  background: color-mix(in srgb, var(--surface-control-raised) 96%, #fff7ed 4%);
}

.availability-table tbody tr:nth-child(even) td {
  background: color-mix(in srgb, var(--surface-control-raised) 88%, #fffaf0 12%);
}

.availability-table tr:last-child td {
  border-bottom: 0;
}

.availability-date {
  font-weight: 780;
}

.availability-date--fast {
  color: var(--state-success-text);
}

.availability-date--medium {
  color: #CC8B08;
}

.availability-date--slow {
  color: #ac0e28;
}

.availability-date--neutral {
  color: var(--color-text-muted);
  font-weight: 680;
}

.testing-pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-3);
}

@media (min-width: 920px) {
  .testing-card__header {
    grid-template-columns: 7.5rem minmax(0, 1fr) minmax(10.5rem, 14rem);
  }

  .testing-card__metrics {
    grid-column: auto;
    grid-template-columns: 1fr;
  }
}

@media (max-width: 760px) {
  .testing-card__header {
    grid-template-columns: 5.75rem minmax(0, 1fr);
  }

  .testing-card__metrics {
    grid-template-columns: 1fr;
  }

}
</style>
