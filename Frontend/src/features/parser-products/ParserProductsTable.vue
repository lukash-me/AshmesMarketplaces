<script setup lang="ts">
import { Columns3, RotateCcw } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable, { type DataTableColumn } from '@/shared/ui/DataTable.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';

import MarketProductImage from './MarketProductImage.vue';
import type {
  ParserProductListItem,
  ParserProductPosition
} from './parserProducts.types';

type MarketProductColumnId =
  | 'thumbnailUrl'
  | 'product'
  | 'position'
  | 'brandSeller'
  | 'price'
  | 'stock'
  | 'reviewRating'
  | 'feedbackCount'
  | 'wbProductId'
  | 'brandName'
  | 'sellerName'
  | 'sourceCategory'
  | 'sourceSubcategory'
  | 'priceRegular'
  | 'priceWbWallet'
  | 'discountPercent';

type MarketProductColumn = DataTableColumn<ParserProductListItem> & {
  key: MarketProductColumnId;
  optionLabel: string;
  group: 'default' | 'optional';
};

const props = defineProps<{
  rows: ParserProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ParserProductListItem];
}>();

const storageKey = 'ashmes.market-products.columns.v3';
const mandatoryColumns: MarketProductColumnId[] = ['product'];
const defaultColumnIds: MarketProductColumnId[] = [
  'thumbnailUrl',
  'product',
  'position',
  'brandSeller',
  'price',
  'reviewRating',
  'feedbackCount'
];
const columns: MarketProductColumn[] = [
  { key: 'thumbnailUrl', label: 'Фото', optionLabel: 'Фото', group: 'default', className: 'table__cell--image' },
  { key: 'product', label: 'Товар', optionLabel: 'Товар', group: 'default', className: 'table__cell--product' },
  { key: 'position', label: 'Позиция', optionLabel: 'Позиция', group: 'default', sortable: true },
  { key: 'brandSeller', label: 'Бренд / продавец', optionLabel: 'Бренд / продавец', group: 'default', className: 'table__cell--brand-seller' },
  { key: 'price', label: 'Цена', optionLabel: 'Цена', group: 'default', sortable: true, align: 'right', className: 'table__cell--price' },
  { key: 'stock', label: 'Остаток WB', optionLabel: 'Остаток WB', group: 'default', align: 'right', className: 'table__cell--stock' },
  { key: 'reviewRating', label: 'Рейтинг WB', optionLabel: 'Рейтинг WB', group: 'default', sortable: true, align: 'right', className: 'table__cell--rating' },
  { key: 'feedbackCount', label: 'Отзывы WB', optionLabel: 'Отзывы WB', group: 'default', sortable: true, align: 'right', className: 'table__cell--feedback' },
  { key: 'wbProductId', label: 'WB id', optionLabel: 'WB id', group: 'optional', sortable: true },
  { key: 'brandName', label: 'Бренд', optionLabel: 'Бренд', group: 'optional' },
  { key: 'sellerName', label: 'Продавец', optionLabel: 'Продавец', group: 'optional' },
  { key: 'sourceCategory', label: 'Категория', optionLabel: 'Категория', group: 'optional' },
  { key: 'sourceSubcategory', label: 'Подкатегория', optionLabel: 'Подкатегория', group: 'optional' },
  { key: 'priceRegular', label: 'Цена без скидки', optionLabel: 'Цена без скидки', group: 'optional', align: 'right' },
  { key: 'priceWbWallet', label: 'WB кошелек', optionLabel: 'Цена с WB кошельком', group: 'optional', align: 'right' },
  { key: 'discountPercent', label: 'Скидка', optionLabel: 'Скидка', group: 'optional', align: 'right' },
];
const columnIdSet = new Set(columns.map((column) => column.key));

const columnMenuOpen = ref(false);
const visibleColumnIds = ref<MarketProductColumnId[]>([...defaultColumnIds]);

const visibleColumns = computed(() =>
  columns
    .filter((column) => visibleColumnIds.value.includes(column.key))
    .map((column) => ({
      ...column,
      label: column.label.toLocaleUpperCase('ru-RU'),
      className: [
        column.className,
        column.sortable ? 'table__cell--sortable' : '',
        sortedColumnKey(props.sort) === column.key ? 'table__cell--sorted' : ''
      ].filter(Boolean).join(' ')
    }))
);
const optionColumns = computed(() => columns);
const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
const paginationItems = computed(() => buildPaginationItems(props.page, pageCount.value));

onMounted(() => {
  const stored = readStoredColumns();
  if (stored) {
    visibleColumnIds.value = stored;
  }
});

function readStoredColumns(): MarketProductColumnId[] | null {
  try {
    const parsed = JSON.parse(localStorage.getItem(storageKey) ?? 'null');
    if (!Array.isArray(parsed)) {
      return null;
    }

    const selected = parsed.filter(
      (value): value is MarketProductColumnId =>
        typeof value === 'string' && columnIdSet.has(value as MarketProductColumnId)
    );

    return ensureMandatoryColumns(selected.length ? selected : defaultColumnIds);
  } catch {
    return null;
  }
}

function toggleColumn(column: MarketProductColumn) {
  if (mandatoryColumns.includes(column.key)) {
    return;
  }

  const selected = visibleColumnIds.value.includes(column.key)
    ? visibleColumnIds.value.filter((key) => key !== column.key)
    : [...visibleColumnIds.value, column.key];
  setColumns(ensureMandatoryColumns(selected));
}

function resetColumns() {
  setColumns([...defaultColumnIds]);
}

function setColumns(selected: MarketProductColumnId[]) {
  visibleColumnIds.value = selected;
  localStorage.setItem(storageKey, JSON.stringify(selected));
}

function ensureMandatoryColumns(selected: MarketProductColumnId[]): MarketProductColumnId[] {
  return [...new Set([...selected, ...mandatoryColumns])];
}

function isMandatory(column: MarketProductColumn): boolean {
  return mandatoryColumns.includes(column.key);
}

function sortedColumnKey(sort: string): string {
  return sort.startsWith('-') ? sort.slice(1) : sort;
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

function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? 'Нет данных' : String(value);
}

function formatMoney(value: number | null): string {
  return value === null
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function formatPercent(value: number | null): string {
  return value === null ? 'Нет данных' : `${new Intl.NumberFormat('ru-RU').format(value)}%`;
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU').format(value);
}

function logisticsStockLabel(row: ParserProductListItem): string {
  return row.logistics?.totalQuantityLabel?.trim() || 'Нет данных';
}

function logisticsWarehouseLabel(row: ParserProductListItem): string | null {
  if (!row.logistics) {
    return null;
  }

  return `Складов WB: ${formatNumber(row.logistics.warehouseCount)}`;
}

function identityValue(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

function positionFor(row: ParserProductListItem): ParserProductPosition {
  return row.position ?? {
    state: 'unknown',
    absolutePosition: null,
    observedRangeLimit: null,
    query: null,
    sourceCategory: row.sourceCategory,
    sourceSubcategory: row.sourceSubcategory,
    observedAtUtc: null
  };
}

function positionLabel(row: ParserProductListItem): string {
  const position = positionFor(row);

  if (position.state === 'observed' && position.absolutePosition !== null) {
    return `#${formatNumber(position.absolutePosition)}`;
  }

  if (position.state === 'beyondObservedRange' && position.observedRangeLimit !== null) {
    return `>${formatNumber(position.observedRangeLimit)}`;
  }

  return 'Нет данных';
}

function positionTone(row: ParserProductListItem): string {
  return positionFor(row).state;
}

function positionTitle(row: ParserProductListItem): string {
  const position = positionFor(row);

  if (position.state === 'unknown') {
    return 'Позиция пока не определена для этой карточки.';
  }

  const observed = formatDateTime(position.observedAtUtc);
  const source = [position.sourceSubcategory, position.sourceCategory].filter(Boolean).join(' · ');
  return [
    position.state === 'observed'
      ? 'Место карточки в этой подкатегории.'
      : 'Карточка не найдена в пределах проверенного диапазона.',
    position.query ? `Запрос: ${position.query}` : null,
    source ? `Источник: ${source}` : null,
    observed !== 'Нет данных' ? `Позиция проверена: ${observed}` : null
  ]
    .filter(Boolean)
    .join('\n');
}

function ratingTone(value: number | null): string {
  if (value === null || value <= 0) {
    return 'neutral';
  }

  if (value >= 4.7) {
    return 'positive';
  }

  if (value >= 4.2) {
    return 'warning';
  }

  return 'negative';
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('ru-RU', {
        month: 'short',
        day: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : 'Нет данных';
}
</script>

<template>
  <div class="market-products-table app-surface app-operator-table">
    <header class="table-toolbar app-operator-toolbar">
      <div class="table-toolbar__heading">
        <h2>
          <span>Аналитика товаров</span>
          <HelpTooltip text="Исследуйте товары конкурентов, цены, рейтинги и отзывы, чтобы находить перспективные ниши." />
        </h2>
      </div>
      <div class="column-picker">
        <Button class="column-picker__button" variant="secondary" @click="columnMenuOpen = !columnMenuOpen">
          <Columns3 :size="16" />
          Столбцы
        </Button>
        <section v-if="columnMenuOpen" class="column-picker__menu" aria-label="Настройка столбцов">
          <header>
            <strong>Столбцы таблицы</strong>
            <button type="button" @click="resetColumns">
              <RotateCcw :size="14" />
              Сбросить
            </button>
          </header>
          <fieldset>
            <legend>Пользовательский вид</legend>
            <label v-for="column in optionColumns" :key="column.key">
              <input
                type="checkbox"
                :checked="visibleColumnIds.includes(column.key)"
                :disabled="isMandatory(column)"
                @change="toggleColumn(column)"
              />
              <span>{{ column.optionLabel }}</span>
            </label>
          </fieldset>
        </section>
      </div>
    </header>

    <DataTable
      :rows="rows"
      :columns="visibleColumns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Открыть товар маркетплейса ${row.wbProductId}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-thumbnailUrl="{ row }">
        <span class="thumb">
          <MarketProductImage :src="row.thumbnailUrl" :alt="row.name" />
        </span>
      </template>

      <template #cell-product="{ row }">
        <strong class="product-name">{{ row.name }}</strong>
        <span class="meta-line">{{ fieldValue(row.sourceCategory) }} · {{ fieldValue(row.sourceSubcategory) }}</span>
        <code class="code-cell">WB {{ row.wbProductId }}</code>
      </template>

      <template #cell-position="{ row }">
        <div class="position-cell" :class="`position-cell--${positionTone(row)}`" :title="positionTitle(row)">
          <strong>{{ positionLabel(row) }}</strong>
        </div>
      </template>

      <template #cell-brandSeller="{ row }">
        <div class="brand-seller">
          <strong>{{ identityValue(row.brandName) }}</strong>
          <span>{{ identityValue(row.sellerName) }}</span>
        </div>
      </template>

      <template #cell-price="{ row }">
        <div class="price-cell numeric">
          <strong>{{ formatMoney(row.priceDiscounted) }}</strong>
          <span v-if="row.priceRegular !== null">без скидки {{ formatMoney(row.priceRegular) }}</span>
          <span v-if="row.priceWbWallet !== null">WB кошелек {{ formatMoney(row.priceWbWallet) }}</span>
          <span v-if="row.discountPercent !== null">скидка {{ formatPercent(row.discountPercent) }}</span>
        </div>
      </template>

      <template #cell-stock="{ row }">
        <div class="stock-cell numeric">
          <strong>{{ logisticsStockLabel(row) }}</strong>
          <span v-if="logisticsWarehouseLabel(row)">{{ logisticsWarehouseLabel(row) }}</span>
        </div>
      </template>

      <template #cell-reviewRating="{ row }">
        <div class="rating-cell numeric" :class="`rating-cell--${ratingTone(row.reviewRating)}`">
          <strong>{{ fieldValue(row.reviewRating) }}</strong>
          <span>Рейтинг карточки WB</span>
        </div>
      </template>

      <template #cell-wbProductId="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-brandName="{ value }">
        {{ identityValue(value as string | null) }}
      </template>

      <template #cell-sellerName="{ value }">
        {{ identityValue(value as string | null) }}
      </template>

      <template #cell-sourceCategory="{ value }">
        <Badge tone="info">{{ fieldValue(value as string | null) }}</Badge>
      </template>

      <template #cell-sourceSubcategory="{ value }">
        {{ fieldValue(value as string | null) }}
      </template>

      <template #cell-priceRegular="{ value }">
        <span class="numeric">{{ formatMoney(value as number | null) }}</span>
      </template>

      <template #cell-priceWbWallet="{ value }">
        <span class="numeric">{{ formatMoney(value as number | null) }}</span>
      </template>

      <template #cell-discountPercent="{ value }">
        <span class="numeric">{{ formatPercent(value as number | null) }}</span>
      </template>

      <template #cell-feedbackCount="{ value }">
        <div class="feedback-cell numeric">
          <strong>{{ fieldValue(value as number | null) }}</strong>
          <span>Отзывы покупателей</span>
        </div>
      </template>

    </DataTable>

    <footer class="table-footer">
      <span class="numeric">Показаны {{ pageStart }}-{{ pageEnd }} из {{ totalCount }}</span>
      <div class="pager">
        <Button class="pager__nav" variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">Назад</Button>
        <div class="pager__pages" aria-label="Страницы товаров маркетплейса">
          <template v-for="item in paginationItems" :key="item">
            <span v-if="typeof item === 'string'" class="pager__ellipsis" aria-hidden="true">…</span>
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
        <Button class="pager__nav" variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">Далее</Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.market-products-table {
  position: relative;
  overflow: visible;
  border-color: var(--operator-border-muted);
  background: var(--operator-panel-bg);
  box-shadow: var(--shadow-panel);
}

.market-products-table::after {
  position: absolute;
  z-index: 10;
  inset: 0;
  border: 1px solid var(--operator-border-muted);
  border-radius: inherit;
  content: '';
  pointer-events: none;
}

.table-toolbar,
.table-footer,
.pager,
.column-picker__menu header {
  display: flex;
  gap: var(--space-3);
}

.table-toolbar {
  position: relative;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--operator-border-muted);
  background: var(--surface-table-header-strong);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: var(--operator-body-size);
}

.table-toolbar__heading {
  display: grid;
  gap: var(--space-1);
}

.table-toolbar__heading h2 {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  letter-spacing: 0;
}

.column-picker {
  position: relative;
}

.column-picker__button {
  min-height: 2.35rem;
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.2), rgb(249 115 22 / 0.07)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 10px 24px rgb(249 115 22 / 0.07);
  font-weight: 760;
}

.column-picker__button:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.24), rgb(249 115 22 / 0.09)),
    var(--color-surface-hover);
}

.column-picker__button:focus-visible {
  box-shadow: var(--focus-ring), 0 10px 24px rgb(249 115 22 / 0.1);
}

.column-picker__menu {
  position: absolute;
  z-index: 35;
  top: calc(100% + var(--space-2));
  right: 0;
  display: grid;
  width: min(21rem, calc(100vw - 2rem));
  max-height: min(32rem, calc(100vh - 8rem));
  overflow-y: auto;
  gap: var(--space-3);
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, var(--accent-ember-soft), transparent 40%),
    var(--surface-panel-raised);
  box-shadow: var(--shadow-panel);
  backdrop-filter: blur(18px);
  padding: var(--space-3);
}

.column-picker__menu header {
  align-items: center;
  justify-content: space-between;
}

.column-picker__menu button {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 680;
}

.column-picker__menu fieldset {
  display: grid;
  gap: var(--space-2);
  margin: 0;
  border: 0;
  padding: 0;
}

.column-picker__menu legend {
  color: var(--color-text-muted);
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
}

.column-picker__menu label {
  display: grid;
  grid-template-columns: 1rem minmax(0, 1fr);
  align-items: center;
  gap: var(--space-2);
  font-size: 0.8125rem;
}

.column-picker__menu input[type='checkbox'] {
  width: 1rem;
  height: 1rem;
  margin: 0;
  accent-color: var(--accent-ember);
}

.column-picker__menu input[type='checkbox']:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.thumb {
  display: grid;
  width: 8rem;
  height: 10rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
  font-size: 0.6875rem;
  text-align: center;
}

.product-name,
.meta-line,
.brand-seller,
.price-cell,
.rating-cell,
.position-cell,
.feedback-cell,
.stock-cell {
  display: block;
}

.product-name {
  display: -webkit-box;
  max-width: 28rem;
  overflow: hidden;
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 740;
  line-height: 1.28;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.brand-seller,
.price-cell,
.rating-cell,
.position-cell,
.feedback-cell,
.stock-cell {
  display: grid;
  gap: var(--space-1);
}

.brand-seller span,
.meta-line,
.price-cell,
.rating-cell,
.position-cell,
.feedback-cell,
.stock-cell {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.brand-seller strong,
.price-cell strong,
.rating-cell strong,
.position-cell strong,
.feedback-cell strong,
.stock-cell strong {
  color: var(--color-text);
  font-size: 0.9375rem;
}

.price-cell {
  min-width: 8.75rem;
  justify-items: end;
}

.brand-seller {
  max-width: 15rem;
}

.brand-seller strong,
.brand-seller span {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stock-cell {
  justify-items: end;
  min-width: 5.5rem;
}

.rating-cell--positive strong {
  color: var(--state-success);
}

.rating-cell--warning strong {
  color: var(--state-warning);
}

.rating-cell--negative strong {
  color: var(--state-danger);
}

.rating-cell--neutral strong {
  color: var(--color-text-muted);
}

.position-cell {
  position: relative;
  max-width: 11rem;
  border-left: 2px solid var(--color-border-strong);
  padding-left: var(--space-2);
}

.position-cell--observed {
  border-color: var(--accent-ember-border);
}

.position-cell--beyondObservedRange {
  border-color: var(--state-warning-border);
}

.position-cell--unknown {
  border-color: var(--color-border);
}

.position-cell strong {
  font-size: 1rem;
}

.position-cell span,
.feedback-cell span {
  overflow-wrap: anywhere;
}

.feedback-cell {
  justify-items: end;
}

.code-cell {
  display: block;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.74rem;
  white-space: nowrap;
}

:deep(.table) {
  min-width: 1180px;
}

:deep(.table-wrap) {
  position: relative;
  isolation: isolate;
}

:deep(.table-wrap)::after {
  position: absolute;
  z-index: 3;
  inset: 0;
  border: 1px solid var(--color-border-strong);
  border-radius: inherit;
  content: '';
  pointer-events: none;
}

:deep(th) {
  border-bottom: 1px solid var(--border-table-header);
  background: var(--surface-table-header-strong);
  color: var(--text-table-header);
  font-size: 0.78rem;
  font-weight: 840;
  letter-spacing: 0.035em;
  line-height: 1.1;
  text-transform: uppercase;
  box-shadow: inset 0 -1px 0 var(--surface-highlight-overlay);
}

:deep(th.table__cell--sortable) {
  color: var(--text-table-header);
}

:deep(.table__sort) {
  min-height: 1.35rem;
  align-items: center;
  color: inherit;
  line-height: 1.1;
  transition: color 120ms ease, filter 120ms ease;
}

:deep(.table__sort svg) {
  flex: 0 0 auto;
  color: currentColor;
  opacity: 0.95;
  filter: none;
  transition: color 120ms ease, opacity 120ms ease, filter 120ms ease;
}

:deep(.table__sort:hover) {
  color: var(--text-table-header);
}

:deep(.table__sort:hover svg) {
  color: currentColor;
  filter: none;
}

:deep(th.table__cell--sorted) {
  color: var(--text-table-header);
  box-shadow:
    inset 0 -1px 0 var(--accent-ember-border),
    inset 0 -4px 8px rgb(249 115 22 / 0.08);
}

:deep(th.table__cell--sorted .table__sort svg) {
  color: currentColor;
  opacity: 1;
  filter: none;
}

:deep(tbody tr) {
  height: 10.5rem;
}

:deep(td) {
  padding: 0.5rem 0.625rem;
}

:deep(td.table__cell--image) {
  padding: 0.25rem;
}

:deep(th.table__cell--image),
:deep(td.table__cell--image) {
  width: 8.5rem;
  min-width: 8.5rem;
  max-width: 8.5rem;
}

:deep(th.table__cell--brand-seller),
:deep(td.table__cell--brand-seller) {
  width: 16rem;
  max-width: 16rem;
}

:deep(th.table__cell--price),
:deep(td.table__cell--price) {
  width: 9rem;
  min-width: 8.75rem;
  max-width: 9.5rem;
}

:deep(th.table__cell--rating),
:deep(td.table__cell--rating) {
  width: 7rem;
  min-width: 6.75rem;
  max-width: 7rem;
}

:deep(th.table__cell--feedback),
:deep(td.table__cell--feedback) {
  width: 7.5rem;
  min-width: 7rem;
  max-width: 7.5rem;
}

:deep(tbody tr:hover) {
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.1), transparent 38%),
    var(--color-surface-hover);
  box-shadow: inset 2px 0 0 rgb(249 115 22 / 0.44);
}

:deep(tbody tr.table__row--selected td:first-child) {
  box-shadow: inset 3px 0 0 var(--accent-ember), inset 0 0 0 1px rgb(249 115 22 / 0.08);
}

:deep(tbody tr.table__row--selected) {
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.12), transparent 42%),
    var(--surface-active-overlay);
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
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, box-shadow 120ms ease;
}

.pager__page:hover {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.09), transparent),
    var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.pager__page--active {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.22), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 8px 20px rgb(249 115 22 / 0.08);
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
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.14), rgb(249 115 22 / 0.05)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  font-weight: 720;
}

.pager__nav:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.2), rgb(249 115 22 / 0.08)),
    var(--color-surface-hover);
}

.pager__nav:disabled {
  border-color: var(--color-border);
  background: var(--surface-control);
  color: var(--color-text-muted);
  opacity: 0.58;
}
</style>
