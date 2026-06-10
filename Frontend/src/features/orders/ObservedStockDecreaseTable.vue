<script setup lang="ts">
import { computed } from 'vue';
import { ExternalLink } from 'lucide-vue-next';

import Button from '@/shared/ui/Button.vue';
import DataTable, { type DataTableColumn } from '@/shared/ui/DataTable.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import { getWildberriesProductUrl } from '@/features/parser-products/wildberriesLinks';

import {
  formatObservedDateTime,
  formatObservedDecrease,
  formatObservedMoney,
  formatObservedNumber,
  observedFieldValue
} from './orderDisplay';
import type { ObservedStockDecreaseItem, ObservedStockDecreaseSummary } from './orders.types';

const props = defineProps<{
  rows: ObservedStockDecreaseItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  summary: ObservedStockDecreaseSummary | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
}>();

const columns: Array<DataTableColumn<ObservedStockDecreaseItem>> = [
  { key: 'product', label: 'Товар', className: 'table__cell--product' },
  { key: 'brandSeller', label: 'Бренд / продавец' },
  { key: 'category', label: 'Категория' },
  { key: 'previousQuantity', label: 'Остаток был', sortable: true, align: 'right' },
  { key: 'currentQuantity', label: 'Остаток стал', sortable: true, align: 'right' },
  { key: 'decrease', label: 'Изменение', sortable: true, align: 'right' },
  { key: 'observedAtUtc', label: 'Период', sortable: true },
  { key: 'price', label: 'Цена', align: 'right' },
  { key: 'rating', label: 'Рейтинг', align: 'right' },
  { key: 'feedbackCount', label: 'Отзывы WB', align: 'right' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
const paginationItems = computed(() => buildPaginationItems(props.page, pageCount.value));

function rowKey(row: ObservedStockDecreaseItem): string {
  return `${row.wbProductId}:${row.destination ?? ''}`;
}

function productName(row: ObservedStockDecreaseItem): string {
  return row.name?.trim() || `WB ${row.wbProductId}`;
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
  <div class="observed-stock-table app-surface app-operator-table">
    <header class="table-toolbar app-operator-toolbar">
      <div class="table-toolbar__heading">
        <h2>
          <span>Снижение наблюдаемого остатка WB</span>
          <HelpTooltip text="Только товары, где последний наблюдаемый остаток меньше предыдущего." />
        </h2>
      </div>
      <div v-if="summary" class="summary-strip" aria-label="Сводка по наблюдаемым остаткам">
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.comparedProductsCount) }}</strong>
          сравнено
        </span>
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.productsWithDecrease) }}</strong>
          со снижением
        </span>
        <span class="app-operator-metric">
          <strong>{{ formatObservedNumber(summary.totalObservedDecrease) }}</strong>
          суммарное снижение
        </span>
      </div>
    </header>

    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="rowKey"
      @sort="emit('sort', $event)"
    >
      <template #cell-product="{ row }">
        <div class="product-cell">
          <span class="thumb">
            <MarketProductImage :src="row.imageUrl" :alt="productName(row)" />
          </span>
          <span class="product-cell__body">
            <strong>{{ productName(row) }}</strong>
            <span>WB {{ row.wbProductId }}</span>
            <a
              v-if="getWildberriesProductUrl(row.wbProductId)"
              class="wb-link"
              :href="getWildberriesProductUrl(row.wbProductId) ?? undefined"
              target="_blank"
              rel="noreferrer"
              title="Открыть карточку WB"
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
        <div class="stack-cell">
          <strong>{{ observedFieldValue(row.sourceCategory) }}</strong>
          <span>{{ observedFieldValue(row.sourceSubcategory) }}</span>
          <small v-if="row.destination">Направление WB: {{ row.destination }}</small>
        </div>
      </template>

      <template #cell-previousQuantity="{ value }">
        <span class="numeric stock-number">{{ formatObservedNumber(value as number) }}</span>
      </template>

      <template #cell-currentQuantity="{ value }">
        <span class="numeric stock-number">{{ formatObservedNumber(value as number) }}</span>
      </template>

      <template #cell-decrease="{ row }">
        <span class="numeric decrease-number">{{ formatObservedDecrease(row.quantityDecrease) }}</span>
      </template>

      <template #cell-observedAtUtc="{ row }">
        <span class="period-cell">
          <span class="numeric">{{ formatObservedDateTime(row.previousObservedAtUtc) }}</span>
          <span aria-hidden="true">→</span>
          <span class="numeric">{{ formatObservedDateTime(row.currentObservedAtUtc) }}</span>
        </span>
      </template>

      <template #cell-price="{ value }">
        <span class="numeric">{{ formatObservedMoney(value as number | null) }}</span>
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
        <div class="pager__pages" aria-label="Страницы предполагаемых заказов">
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
.observed-stock-table {
  position: relative;
  overflow: visible;
  border-color: var(--operator-border-muted);
  background: var(--operator-panel-bg);
  box-shadow: var(--shadow-panel);
}

.table-toolbar,
.table-footer,
.pager,
.summary-strip {
  display: flex;
  gap: var(--space-3);
}

.table-toolbar {
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--operator-border-muted);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: var(--operator-body-size);
}

.table-toolbar__heading {
  display: grid;
  gap: var(--space-1);
}

.table-toolbar h2 {
  margin: 0;
}

.table-toolbar h2 {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  letter-spacing: 0;
}

.summary-strip {
  flex-wrap: wrap;
  justify-content: flex-end;
}

.summary-strip span {
  display: grid;
  gap: 0.1rem;
  min-width: 7rem;
  padding: var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
}

.summary-strip strong {
  color: var(--color-text);
  font-size: var(--operator-value-size);
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
  font-size: var(--operator-value-size);
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
  font-size: var(--operator-meta-size);
}

.stack-cell strong {
  color: var(--color-text);
  font-weight: 720;
}

.wb-link {
  display: inline-flex;
  width: fit-content;
  align-items: center;
  gap: var(--space-1);
  color: var(--accent-ember-text);
  font-size: var(--operator-meta-size);
  font-weight: 700;
}

.stock-number {
  color: var(--color-text);
  font-weight: 740;
}

.decrease-number {
  color: var(--accent-ember-text-strong);
  font-weight: 820;
}

.period-cell {
  min-width: 9rem;
}

:deep(.table) {
  min-width: 1180px;
}

:deep(th) {
  border-bottom: 1px solid var(--border-table-header);
  background: var(--surface-table-header-strong);
  color: var(--text-table-header);
  font-size: var(--operator-meta-size);
  font-weight: 840;
  letter-spacing: 0.035em;
  line-height: 1.1;
  text-transform: uppercase;
  box-shadow: inset 0 -1px 0 var(--surface-highlight-overlay);
}

:deep(.table__sort) {
  min-height: 1.35rem;
  color: inherit;
}

:deep(.table__sort svg) {
  color: currentColor;
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

.table-footer {
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: var(--operator-body-size);
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
  font-size: var(--operator-body-size);
}

.pager__page {
  border: 1px solid var(--color-border);
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

  .summary-strip {
    justify-content: flex-start;
  }
}
</style>
