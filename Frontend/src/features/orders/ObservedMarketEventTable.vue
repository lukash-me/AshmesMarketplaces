<script setup lang="ts">
import { computed } from 'vue';
import { ExternalLink } from 'lucide-vue-next';

import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import { getWildberriesProductUrl } from '@/features/parser-products/wildberriesLinks';
import Button from '@/shared/ui/Button.vue';
import DataTable, { type DataTableColumn } from '@/shared/ui/DataTable.vue';

import {
  formatObservedDateTime,
  formatObservedMoney,
  formatObservedNumber,
  formatObservedQuantityChange,
  getObservedMarketEventLabel,
  observedFieldValue
} from './orderDisplay';
import type {
  ObservedMarketEventItem
} from './orders.types';

const props = defineProps<{
  rows: ObservedMarketEventItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ObservedMarketEventItem];
}>();

const columns: Array<DataTableColumn<ObservedMarketEventItem>> = [
  { key: 'eventType', label: 'Событие', sortable: true },
  { key: 'product', label: 'Товар', className: 'table__cell--product' },
  { key: 'brandSeller', label: 'Бренд / продавец' },
  { key: 'category', label: 'Категория' },
  { key: 'previousQuantity', label: 'Остаток был', sortable: true, className: 'table__cell--stock' },
  { key: 'currentQuantity', label: 'Остаток стал', sortable: true, className: 'table__cell--stock' },
  { key: 'quantityChange', label: 'Изменение', sortable: true, className: 'table__cell--change' },
  { key: 'observedAtUtc', label: 'Период', sortable: true },
  { key: 'price', label: 'Цена', className: 'table__cell--price' },
  { key: 'rating', label: 'Рейтинг', align: 'right' },
  { key: 'feedbackCount', label: 'Отзывы WB', align: 'right' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
const paginationItems = computed(() => buildPaginationItems(props.page, pageCount.value));

function rowKey(row: ObservedMarketEventItem): string {
  return `${row.eventType}:${row.wbProductId}:${row.destination ?? ''}`;
}

function rowClass(row: ObservedMarketEventItem): string {
  return `event-row--${row.eventType}`;
}

function rowCanOpen(row: ObservedMarketEventItem): boolean {
  return Boolean(row.parserProductRowId);
}

function rowAriaLabel(row: ObservedMarketEventItem): string {
  return `Открыть товар ${productName(row)}`;
}

function openRow(row: ObservedMarketEventItem): void {
  if (rowCanOpen(row)) {
    emit('open', row);
  }
}

function productName(row: ObservedMarketEventItem): string {
  return row.name?.trim() || `WB ${row.wbProductId}`;
}

function quantityValue(value: number | null): string {
  return value === null ? '—' : formatObservedNumber(value);
}

function quantityChangeLabel(row: ObservedMarketEventItem): string {
  if (row.eventType === 'new_product_observed') {
    return 'Новый';
  }

  if (row.eventType === 'product_missing_in_current') {
    return 'Нет в новом наблюдении';
  }

  return formatObservedQuantityChange(row.quantityChange);
}

function quantityChangeClass(row: ObservedMarketEventItem): string {
  if (row.eventType === 'stock_decreased') {
    return 'change-number change-number--decrease';
  }

  if (row.eventType === 'stock_increased') {
    return 'change-number change-number--increase';
  }

  return 'change-number';
}

function eventObservedAt(row: ObservedMarketEventItem): string | null {
  return row.currentObservedAtUtc ?? row.previousObservedAtUtc;
}

function drawerAlignedPrice(row: ObservedMarketEventItem): number | null {
  return row.priceDiscounted;
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
</script>

<template>
  <div class="observed-events-table app-surface">
    <header class="table-toolbar">
      <div class="table-toolbar__heading">
        <h2>Наблюдаемые события WB</h2>
        <p>Изменения между двумя последними наблюдениями по товарам и направлениям WB.</p>
      </div>
    </header>

    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="rowKey"
      :row-class="rowClass"
      :row-interactive="rowCanOpen"
      :row-aria-label="rowAriaLabel"
      @sort="emit('sort', $event)"
      @row-click="openRow"
    >
      <template #cell-eventType="{ row }">
        <span class="event-pill" :class="`event-pill--${row.eventType}`">
          {{ getObservedMarketEventLabel(row.eventType) }}
        </span>
      </template>

      <template #cell-product="{ row }">
        <div class="product-cell">
          <span class="thumb">
            <MarketProductImage :src="row.imageUrl" :alt="productName(row)" />
          </span>
          <span class="product-cell__body">
            <strong>{{ productName(row) }}</strong>
            <span>WB {{ row.wbProductId }}</span>
            <a
              v-if="getWildberriesProductUrl(Number(row.wbProductId))"
              class="wb-link"
              :href="getWildberriesProductUrl(Number(row.wbProductId)) ?? undefined"
              target="_blank"
              rel="noreferrer"
              title="Открыть карточку WB"
              @click.stop
            >
              <ExternalLink :size="13" />
              Карточка WB
            </a>
          </span>
        </div>
      </template>

      <template #cell-brandSeller="{ row }">
        <div class="stack-cell">
          <strong>{{ observedFieldValue(row.brandName) }}</strong>
          <span>{{ observedFieldValue(row.sellerName) }}</span>
        </div>
      </template>

      <template #cell-category="{ row }">
        <div class="stack-cell category-cell">
          <strong>{{ observedFieldValue(row.sourceCategory) }}</strong>
          <span>{{ observedFieldValue(row.sourceSubcategory) }}</span>
        </div>
      </template>

      <template #cell-previousQuantity="{ value }">
        <span class="numeric stock-number">{{ quantityValue(value as number | null) }}</span>
      </template>

      <template #cell-currentQuantity="{ value }">
        <span class="numeric stock-number">{{ quantityValue(value as number | null) }}</span>
      </template>

      <template #cell-quantityChange="{ row }">
        <span class="numeric" :class="quantityChangeClass(row)">{{ quantityChangeLabel(row) }}</span>
      </template>

      <template #cell-observedAtUtc="{ row }">
        <span class="period-cell">
          <template v-if="row.previousObservedAtUtc && row.currentObservedAtUtc">
            <span class="numeric">{{ formatObservedDateTime(row.previousObservedAtUtc) }}</span>
            <span aria-hidden="true">→</span>
            <span class="numeric">{{ formatObservedDateTime(row.currentObservedAtUtc) }}</span>
          </template>
          <span v-else class="numeric">{{ formatObservedDateTime(eventObservedAt(row)) }}</span>
        </span>
      </template>

      <template #cell-price="{ row }">
        <span class="numeric price-number">{{ formatObservedMoney(drawerAlignedPrice(row)) }}</span>
      </template>

      <template #cell-rating="{ value }">
        <span class="numeric">{{ formatObservedNumber(value as number | null) }}</span>
      </template>

      <template #cell-feedbackCount="{ value }">
        <span class="numeric">{{ formatObservedNumber(value as number | null) }}</span>
      </template>
    </DataTable>

    <footer class="table-footer">
      <span class="numeric">Показаны {{ pageStart }}-{{ pageEnd }} из {{ totalCount }}</span>
      <div class="pager">
        <Button class="pager__nav" variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">
          Назад
        </Button>
        <div class="pager__pages" aria-label="Страницы наблюдаемых событий">
          <template v-for="item in paginationItems" :key="item">
            <span v-if="typeof item === 'string'" class="pager__ellipsis" aria-hidden="true">...</span>
            <button
              v-else
              class="pager__page"
              :class="{ 'pager__page--active': item === page }"
              type="button"
              :aria-current="item === page ? 'page' : undefined"
              @click="emit('page', item)"
            >
              {{ item }}
            </button>
          </template>
        </div>
        <Button class="pager__nav" variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">
          Далее
        </Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.observed-events-table {
  position: relative;
  overflow: visible;
  border-color: rgb(249 115 22 / 0.2);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.035), transparent 10rem),
    var(--surface-panel);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.035),
    inset 0 0 0 1px rgb(249 115 22 / 0.035),
    var(--shadow-panel);
}

.observed-events-table::before {
  position: absolute;
  inset: 0 0 auto;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(249 115 22 / 0.42), transparent);
  content: '';
  pointer-events: none;
}

.table-toolbar,
.table-footer,
.pager {
  display: flex;
  gap: var(--space-3);
}

.table-toolbar {
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid rgb(249 115 22 / 0.18);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.table-toolbar__heading {
  display: grid;
  gap: var(--space-1);
}

.table-toolbar h2,
.table-toolbar p {
  margin: 0;
}

.table-toolbar h2 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  letter-spacing: 0;
}

.product-cell {
  display: grid;
  grid-template-columns: 4rem minmax(12rem, 1fr);
  align-items: center;
  gap: var(--space-3);
  min-width: 20rem;
}

.thumb {
  display: grid;
  width: 4rem;
  height: 5rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
}

.product-cell__body,
.stack-cell,
.period-cell {
  display: grid;
  gap: var(--space-1);
}

.product-cell__body strong {
  display: -webkit-box;
  max-width: 28rem;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.95rem;
  font-weight: 740;
  line-height: 1.28;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.product-cell__body span,
.stack-cell span,
.stack-cell small,
.period-cell {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.stack-cell strong {
  color: var(--color-text);
  font-weight: 720;
}

.category-cell strong,
.category-cell span {
  font-size: 0.75rem;
  letter-spacing: 0;
  text-transform: uppercase;
}

.wb-link {
  display: inline-flex;
  width: fit-content;
  align-items: center;
  gap: var(--space-1);
  color: var(--accent-ember-text);
  font-size: 0.75rem;
  font-weight: 700;
}

.event-pill {
  display: inline-flex;
  align-items: center;
  border: 1px solid rgb(249 115 22 / 0.22);
  border-radius: var(--radius-sm);
  background: rgb(249 115 22 / 0.08);
  color: var(--accent-ember-text-strong);
  padding: 0.25rem 0.45rem;
  font-size: 0.72rem;
  font-weight: 760;
  white-space: nowrap;
}

.event-pill--stock_increased {
  border-color: rgb(56 189 248 / 0.24);
  background: rgb(56 189 248 / 0.08);
  color: rgb(125 211 252);
}

.event-pill--new_product_observed {
  border-color: rgb(34 197 94 / 0.24);
  background: rgb(34 197 94 / 0.08);
  color: rgb(134 239 172);
}

.event-pill--product_missing_in_current {
  border-color: rgb(148 163 184 / 0.24);
  background: rgb(148 163 184 / 0.08);
  color: var(--color-text-muted);
}

.stock-number {
  color: var(--color-text);
  font-weight: 740;
}

.change-number {
  color: var(--color-text-muted);
  font-weight: 780;
}

.change-number--decrease {
  color: var(--accent-ember-text-strong);
  font-weight: 820;
}

.change-number--increase {
  color: rgb(125 211 252);
  font-weight: 820;
}

.period-cell {
  min-width: 9rem;
}

.price-number {
  white-space: nowrap;
}

:deep(.table) {
  min-width: 1260px;
}

:deep(th) {
  border-bottom: 1px solid rgb(249 115 22 / 0.2);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.055), rgb(12 15 22 / 0.98)),
    var(--surface-table-header);
  color: var(--color-text);
  font-size: 0.75rem;
  font-weight: 840;
  letter-spacing: 0.035em;
  line-height: 1.1;
  text-transform: uppercase;
  box-shadow: inset 0 -1px 0 rgb(249 115 22 / 0.08);
}

:deep(.table__sort) {
  min-height: 1.35rem;
  color: inherit;
}

:deep(.table__cell--stock),
:deep(.table__cell--change) {
  width: 6.5rem;
  min-width: 6.5rem;
  max-width: 6.5rem;
  text-align: center;
}

:deep(.table__cell--change) {
  width: 7rem;
  min-width: 7rem;
  max-width: 7rem;
}

:deep(.table__cell--stock .table__sort),
:deep(.table__cell--change .table__sort) {
  justify-content: center;
  width: 100%;
}

:deep(.table__cell--price) {
  width: 6rem;
  min-width: 6rem;
  text-align: right;
  white-space: nowrap;
}

:deep(.table__sort svg) {
  color: rgb(253 186 116 / 0.72);
}

:deep(tbody tr) {
  height: 6.25rem;
}

:deep(td) {
  padding: 0.5rem 0.625rem;
}

:deep(tbody tr:hover) {
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.1), transparent 38%),
    var(--color-surface-hover);
  box-shadow: inset 2px 0 0 rgb(249 115 22 / 0.44);
}

:deep(.event-row--stock_increased:hover) {
  background:
    linear-gradient(90deg, rgb(56 189 248 / 0.08), transparent 38%),
    var(--color-surface-hover);
}

.table-footer {
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.pager {
  align-items: center;
  flex-wrap: wrap;
}

.pager__pages {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.25rem;
}

.pager__page,
.pager__ellipsis {
  display: inline-flex;
  min-width: 2rem;
  height: 2rem;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-sm);
  font-size: 0.8125rem;
}

.pager__page {
  border: 1px solid rgb(249 115 22 / 0.18);
  background: var(--surface-control);
  color: var(--color-text-muted);
}

.pager__page:hover {
  border-color: var(--accent-ember-border);
  background: var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.pager__page--active {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.22), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
}

.pager__page:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.pager__ellipsis {
  color: var(--color-text-muted);
}

.pager__nav {
  border-color: var(--accent-primary-border);
  color: var(--accent-ember-text-strong);
  font-weight: 720;
}

@media (max-width: 920px) {
  .table-toolbar {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
