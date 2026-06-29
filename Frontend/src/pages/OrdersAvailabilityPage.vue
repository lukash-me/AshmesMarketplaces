<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Search } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import {
  getParserProduct,
  getParserProductAvailability
} from '@/features/parser-products/parserProducts.api';
import type {
  ParserProductDeliveryDestinationSignal,
  ParserProductDeliveryLocationEstimate,
  ParserProductDetail,
  ParserProductListItem
} from '@/features/parser-products/parserProducts.types';

const PAGE_SIZE = 5;
const MS_PER_DAY = 24 * 60 * 60 * 1000;
const MOSCOW_OFFSET_MS = 3 * 60 * 60 * 1000;
const DELIVERY_CITY_ORDER = [
  'Москва',
  'Санкт-Петербург',
  'Казань',
  'Краснодар',
  'Екатеринбург',
  'Новосибирск',
  'Хабаровск'
];
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

const searchDraft = ref('');
const search = ref('');
const page = ref(1);
const rows = ref<ParserProductListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selected = ref<ParserProductListItem | null>(null);
const detailsById = ref<Record<string, ParserProductDetail>>({});
const detailErrorsById = ref<Record<string, string>>({});
const detailLoadingIds = ref<Set<string>>(new Set());

let loadVersion = 0;
let detailLoadVersion = 0;

const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)));

function destinationsFor(row: ParserProductListItem): ParserProductDeliveryDestinationSignal[] {
  const items = detailFor(row)?.deliveryProfile?.destinations ?? [];
  return items
    .map((destination, index) => ({ destination, index }))
    .sort((left, right) => (
      cityOrderIndex(left.destination) - cityOrderIndex(right.destination)
      || left.index - right.index
    ))
    .map((item) => item.destination);
}

watch(
  () => [page.value, search.value] as const,
  () => void loadRows(),
  { immediate: true }
);

async function loadRows() {
  const version = ++loadVersion;
  loading.value = true;
  error.value = '';

  try {
    const response = await getParserProductAvailability({
      page: page.value,
      pageSize: PAGE_SIZE,
      sort: '-parsedAtUtc',
      search: search.value || undefined
    });

    if (version !== loadVersion) {
      return;
    }

    rows.value = response.items;
    totalCount.value = response.totalCount;
    void loadDetails(response.items);
  } catch (err) {
    if (version === loadVersion) {
      rows.value = [];
      totalCount.value = 0;
      error.value = getProblemMessage(err, 'Доступность товара пока не рассчитана.');
    }
  } finally {
    if (version === loadVersion) {
      loading.value = false;
    }
  }
}

async function loadDetails(items: ParserProductListItem[]) {
  const version = ++detailLoadVersion;
  const ids = items.map((item) => item.id);
  detailErrorsById.value = {};
  detailLoadingIds.value = new Set(ids);

  if (ids.length === 0) {
    detailLoadingIds.value = new Set();
    return;
  }

  await Promise.all(items.map(async (row) => {
    try {
      const response = await getParserProduct(row.id);
      if (version === detailLoadVersion) {
        detailsById.value = { ...detailsById.value, [row.id]: response };
      }
    } catch (err) {
      if (version === detailLoadVersion) {
        detailErrorsById.value = {
          ...detailErrorsById.value,
          [row.id]: getProblemMessage(err, 'Не удалось загрузить профиль доставки товара.')
        };
      }
    } finally {
      if (version === detailLoadVersion) {
        const nextLoading = new Set(detailLoadingIds.value);
        nextLoading.delete(row.id);
        detailLoadingIds.value = nextLoading;
      }
    }
  }));
}

function detailFor(row: ParserProductListItem): ParserProductDetail | null {
  return detailsById.value[row.id] ?? null;
}

function isDetailLoading(row: ParserProductListItem): boolean {
  return detailLoadingIds.value.has(row.id);
}

function detailErrorFor(row: ParserProductListItem): string {
  return detailErrorsById.value[row.id] ?? '';
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

function openProduct(row: ParserProductListItem) {
  selected.value = row;
}

function currentPrice(row: ParserProductListItem): number | null {
  return row.priceDiscounted ?? row.priceWbWallet ?? row.priceRegular ?? null;
}

function destinationTitle(destination: ParserProductDeliveryDestinationSignal): string {
  return (
    destination.deliveryDestinationCity?.trim()
    || destination.deliveryDestinationName?.trim()
    || 'Контрольная точка WB'
  );
}

function cityOrderIndex(destination: ParserProductDeliveryDestinationSignal): number {
  const title = destinationTitle(destination).toLocaleLowerCase('ru-RU');
  const index = DELIVERY_CITY_ORDER.findIndex((city) => {
    const normalizedCity = city.toLocaleLowerCase('ru-RU');
    return title === normalizedCity || title.includes(normalizedCity);
  });

  return index === -1 ? Number.MAX_SAFE_INTEGER : index;
}

function deliveryReferenceDate(row: ParserProductListItem): Date | null {
  const timestamps = destinationsFor(row)
    .map((destination) => parseDate(destination.visibleDeliveryObservedAtUtc || destination.observedAtUtc))
    .filter((date): date is Date => date !== null);

  if (timestamps.length === 0) {
    return null;
  }

  return new Date(Math.max(...timestamps.map((date) => date.getTime())));
}

function visibleDeliveryText(row: ParserProductListItem, destination: ParserProductDeliveryDestinationSignal): string {
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

function deliveryTone(row: ParserProductListItem, destination: ParserProductDeliveryDestinationSignal): string {
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
  destination: ParserProductDeliveryDestinationSignal,
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

function deliverySourceSuffix(destination: ParserProductDeliveryDestinationSignal): string {
  const label = destination.visibleDeliveryLabel?.trim();
  if (!label) {
    return '';
  }

  return label.split(',').slice(1).join(',').trim();
}

function availabilityStockText(destination: ParserProductDeliveryDestinationSignal): string {
  return destination.totalQuantityObserved === 0
    ? 'Нет в наличии'
    : formatNumber(destination.totalQuantityObserved);
}

function locationTitle(estimate: ParserProductDeliveryLocationEstimate | null | undefined): string {
  if (!estimate || estimate.status !== 'estimated' || !estimate.zoneTitle) {
    return 'Недостаточно данных для оценки';
  }

  return estimate.zoneTitle;
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

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' }).format(date)
    : 'Нет данных';
}

function formatMoney(value: number | null | undefined): string {
  return value == null
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)} ₽`;
}

function formatNumber(value: number | null | undefined): string {
  return value == null ? 'Нет данных' : new Intl.NumberFormat('ru-RU').format(value);
}
</script>

<template>
  <div class="availability-page">
    <PageHeader title="Доступность товара" />

    <section class="availability-search">
      <div class="search-field">
        <Search :size="18" />
        <input
          v-model="searchDraft"
          type="search"
          placeholder="WB id, товар, бренд или продавец"
          @keyup.enter="applySearch"
        />
      </div>
      <Button :loading="loading" @click="applySearch">Применить</Button>
      <Button variant="secondary" @click="resetSearch">Сбросить</Button>
    </section>

    <LoadingState v-if="loading" label="Загружаем доступность товара..." />
    <EmptyState
      v-else-if="error"
      title="Доступность товара не загружена"
      :description="error"
    />
    <EmptyState
      v-else-if="rows.length === 0"
      title="Товары с профилем доставки не найдены"
      description="В сохраненном расчете пока нет карточек с рассчитанной доставкой."
    />

    <section v-else class="availability-list">
      <article v-for="row in rows" :key="row.id" class="availability-detail">
        <header class="availability-detail__header">
          <button class="availability-detail__image-button" type="button" @click="openProduct(row)">
            <MarketProductImage :src="row.thumbnailUrl" :alt="row.name" class="availability-detail__image" />
          </button>

          <div class="availability-detail__identity">
            <p class="availability-detail__category">{{ row.sourceSubcategory ?? row.sourceCategory ?? 'Категория не указана' }}</p>
            <h2>{{ row.name }}</h2>
            <p>{{ row.brandName ?? 'Бренд не указан' }} · {{ row.sellerName ?? 'Продавец не указан' }}</p>
            <code>WB {{ row.wbProductId }}</code>
          </div>

          <div class="availability-price-card">
            <span>Цена</span>
            <strong>{{ formatMoney(currentPrice(row)) }}</strong>
          </div>
        </header>

        <section class="availability-section">
          <span v-if="isDetailLoading(row)" class="availability-muted">Загружаем профиль доставки...</span>

          <div class="location-estimate">
            <span>Вероятная зона товара</span>
            <strong>{{ locationTitle(detailFor(row)?.deliveryProfile?.locationEstimate) }}</strong>
          </div>

          <div v-if="destinationsFor(row).length" class="availability-table-wrap">
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
                  v-for="destination in destinationsFor(row)"
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

          <p v-else class="availability-muted">
            {{ detailErrorFor(row) || 'Данные доступности для карточки еще загружаются или не найдены.' }}
          </p>
        </section>

        <footer class="availability-detail__footer">
          <Button variant="secondary" @click="openProduct(row)">Открыть карточку</Button>
        </footer>
      </article>

      <footer v-if="pageCount > 1" class="pagination">
        <Button variant="secondary" :disabled="page <= 1 || loading" @click="page--">Назад</Button>
        <span>Страница {{ page }} из {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount || loading" @click="page++">Далее</Button>
      </footer>
    </section>

    <ParserProductDetailDrawer
      :open="selected !== null"
      :product="selected"
      @close="selected = null"
    />
  </div>
</template>

<style scoped>
.availability-page {
  display: grid;
  gap: 18px;
}

.availability-search {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  gap: 12px;
  align-items: center;
  padding: 16px;
  border: 1px solid var(--color-border-strong);
  border-radius: 6px;
  background: var(--color-surface);
}

.search-field {
  display: flex;
  gap: 8px;
  align-items: center;
  min-width: 0;
  padding: 0 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
}

.search-field input {
  width: 100%;
  min-width: 0;
  height: 38px;
  border: 0;
  outline: none;
  background: transparent;
  color: var(--color-text);
}

.availability-detail {
  display: grid;
  gap: 14px;
  padding: 14px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
}

.availability-list {
  display: grid;
  gap: 12px;
}

.availability-detail__header {
  display: grid;
  grid-template-columns: 116px minmax(0, 1fr) minmax(180px, 220px);
  gap: 16px;
  align-items: start;
}

.availability-detail__image-button {
  padding: 0;
  border: 0;
  background: transparent;
  cursor: pointer;
}

.availability-detail__image {
  width: 116px;
  height: 116px;
}

.availability-detail__identity {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.availability-detail__identity h2,
.availability-detail__identity p {
  margin: 0;
}

.availability-detail__identity h2 {
  color: var(--color-text);
  font-size: 16px;
  line-height: 1.35;
}

.availability-detail__identity p,
.availability-detail__identity code,
.availability-detail__category {
  color: var(--color-muted);
  font-size: 13px;
}

.availability-price-card {
  display: grid;
  gap: 8px;
  min-height: 64px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
}

.availability-price-card span,
.location-estimate span {
  color: var(--color-muted);
  font-size: 12px;
  font-weight: 700;
}

.availability-price-card strong,
.location-estimate strong {
  color: var(--color-text);
  font-size: 16px;
}

.availability-section {
  display: grid;
  gap: 10px;
}

.location-estimate {
  display: grid;
  gap: 5px;
  padding: 12px;
  border: 1px solid var(--color-border-strong);
  border-radius: 6px;
  background: var(--color-surface);
}

.availability-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border-strong);
  border-radius: 6px;
  background: var(--color-surface);
}

.availability-table {
  width: 100%;
  min-width: 520px;
  border-collapse: collapse;
}

.availability-table th,
.availability-table td {
  border-bottom: 1px solid var(--color-border);
  padding: 9px 12px;
  text-align: left;
  vertical-align: middle;
}

.availability-table th {
  color: var(--color-muted);
  font-size: 12px;
  font-weight: 800;
}

.availability-table td {
  color: var(--color-text);
  font-size: 13px;
}

.availability-table tr:last-child td {
  border-bottom: 0;
}

.availability-date {
  font-weight: 800;
}

.availability-date--fast {
  color: var(--state-success-text);
}

.availability-date--medium {
  color: #cc8b08;
}

.availability-date--slow {
  color: #ac0e28;
}

.availability-date--neutral {
  color: var(--color-muted);
  font-weight: 700;
}

.availability-muted {
  margin: 0;
  color: var(--color-muted);
  font-size: 13px;
}

.availability-detail__footer {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: var(--color-muted);
  font-size: 13px;
  font-weight: 700;
}

@media (max-width: 900px) {
  .availability-search,
  .availability-detail__header {
    grid-template-columns: 1fr;
  }

  .availability-detail__image {
    width: 140px;
    height: 140px;
  }
}
</style>
