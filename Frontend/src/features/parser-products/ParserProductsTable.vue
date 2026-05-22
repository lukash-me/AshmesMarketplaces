<script setup lang="ts">
import { Columns3, RotateCcw } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable, { type DataTableColumn } from '@/shared/ui/DataTable.vue';

import MarketProductImage from './MarketProductImage.vue';
import type {
  ParserProductListItem,
  ParserProductReviewPresence
} from './parserProducts.types';

type MarketProductColumnId =
  | 'thumbnailUrl'
  | 'product'
  | 'brandSeller'
  | 'price'
  | 'reviewRating'
  | 'reviewPresence'
  | 'wbProductId'
  | 'brandName'
  | 'sellerName'
  | 'sourceCategory'
  | 'sourceSubcategory'
  | 'priceRegular'
  | 'priceWbWallet'
  | 'discountPercent'
  | 'feedbackCount';

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
  reviewPresence: Record<string, ParserProductReviewPresence | undefined>;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: ParserProductListItem];
}>();

const storageKey = 'ashmes.market-products.columns.v1';
const mandatoryColumns: MarketProductColumnId[] = ['product'];
const defaultColumnIds: MarketProductColumnId[] = [
  'thumbnailUrl',
  'product',
  'brandSeller',
  'price',
  'reviewRating',
  'reviewPresence'
];
const columns: MarketProductColumn[] = [
  { key: 'thumbnailUrl', label: 'Фото', optionLabel: 'Фото', group: 'default', className: 'table__cell--image' },
  { key: 'product', label: 'Товар', optionLabel: 'Товар', group: 'default', className: 'table__cell--product' },
  { key: 'brandSeller', label: 'Бренд и продавец', optionLabel: 'Бренд и продавец', group: 'default' },
  { key: 'price', label: 'Цена', optionLabel: 'Цена', group: 'default', align: 'right' },
  { key: 'reviewRating', label: 'Рейтинг', optionLabel: 'Рейтинг', group: 'default', sortable: true, align: 'right' },
  { key: 'reviewPresence', label: 'Найденные отзывы', optionLabel: 'Найденные отзывы', group: 'default' },
  { key: 'wbProductId', label: 'WB id', optionLabel: 'WB id', group: 'optional', sortable: true },
  { key: 'brandName', label: 'Бренд', optionLabel: 'Бренд', group: 'optional' },
  { key: 'sellerName', label: 'Продавец', optionLabel: 'Продавец', group: 'optional' },
  { key: 'sourceCategory', label: 'Категория', optionLabel: 'Категория', group: 'optional' },
  { key: 'sourceSubcategory', label: 'Подкатегория', optionLabel: 'Подкатегория', group: 'optional' },
  { key: 'priceRegular', label: 'Цена без скидки', optionLabel: 'Цена без скидки', group: 'optional', align: 'right' },
  { key: 'priceWbWallet', label: 'WB кошелек', optionLabel: 'Цена с WB кошельком', group: 'optional', align: 'right' },
  { key: 'discountPercent', label: 'Скидка', optionLabel: 'Скидка', group: 'optional', align: 'right' },
  { key: 'feedbackCount', label: 'Отзывы', optionLabel: 'Количество отзывов', group: 'optional', sortable: true, align: 'right' }
];
const columnIdSet = new Set(columns.map((column) => column.key));

const columnMenuOpen = ref(false);
const visibleColumnIds = ref<MarketProductColumnId[]>([...defaultColumnIds]);

const visibleColumns = computed(() =>
  columns.filter((column) => visibleColumnIds.value.includes(column.key))
);
const optionColumns = computed(() => columns);
const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));

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

function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

function formatMoney(value: number | null): string {
  return value === null
    ? '-'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function formatPercent(value: number | null): string {
  return value === null ? '-' : `${new Intl.NumberFormat('ru-RU').format(value)}%`;
}

function presenceFor(row: ParserProductListItem): ParserProductReviewPresence {
  return props.reviewPresence[row.id] ?? { status: 'loading' };
}
</script>

<template>
  <div class="market-products-table app-surface">
    <header class="table-toolbar">
      <p>Товары из данных сервиса для сравнения ассортимента и карточек маркетплейса.</p>
      <div class="column-picker">
        <Button variant="secondary" @click="columnMenuOpen = !columnMenuOpen">
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

      <template #cell-brandSeller="{ row }">
        <div class="brand-seller">
          <strong>{{ fieldValue(row.brandName) }}</strong>
          <span>{{ fieldValue(row.sellerName) }}</span>
        </div>
      </template>

      <template #cell-price="{ row }">
        <div class="price-cell numeric">
          <strong>{{ formatMoney(row.priceDiscounted) }}</strong>
          <span>без скидки {{ formatMoney(row.priceRegular) }}</span>
          <span>WB кошелек {{ formatMoney(row.priceWbWallet) }} · скидка {{ formatPercent(row.discountPercent) }}</span>
        </div>
      </template>

      <template #cell-reviewRating="{ row }">
        <div class="rating-cell numeric">
          <strong>{{ fieldValue(row.reviewRating) }}</strong>
          <span>{{ fieldValue(row.feedbackCount) }} отзывов</span>
        </div>
      </template>

      <template #cell-reviewPresence="{ row }">
        <div class="presence-cell">
          <Badge v-if="presenceFor(row).status === 'present'" tone="success">Есть отзывы</Badge>
          <Badge v-else-if="presenceFor(row).status === 'absent'" tone="neutral">Не найдены</Badge>
          <Badge v-else-if="presenceFor(row).status === 'error'" tone="warning">Не удалось проверить</Badge>
          <Badge v-else tone="info">Проверяем</Badge>
        </div>
      </template>

      <template #cell-wbProductId="{ value }">
        <code class="code-cell">{{ value }}</code>
      </template>

      <template #cell-brandName="{ value }">
        {{ fieldValue(value as string | null) }}
      </template>

      <template #cell-sellerName="{ value }">
        {{ fieldValue(value as string | null) }}
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
        <span class="numeric">{{ fieldValue(value as number | null) }}</span>
      </template>

    </DataTable>

    <footer class="table-footer">
      <span class="numeric">Показаны {{ pageStart }}-{{ pageEnd }} из {{ totalCount }}</span>
      <div class="pager">
        <Button variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">Назад</Button>
        <span class="numeric">Страница {{ page }} / {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">Далее</Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.market-products-table {
  overflow: visible;
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
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.table-toolbar p {
  margin: 0;
}

.column-picker {
  position: relative;
}

.column-picker__menu {
  position: absolute;
  z-index: 4;
  top: calc(100% + var(--space-2));
  right: 0;
  display: grid;
  width: min(21rem, calc(100vw - 2rem));
  gap: var(--space-3);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--background-panel-highlight);
  box-shadow: var(--shadow-lg);
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

.thumb {
  display: grid;
  width: 5.75rem;
  height: 7.25rem;
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
.rating-cell {
  display: block;
}

.product-name {
  display: -webkit-box;
  max-width: 28rem;
  overflow: hidden;
  line-height: 1.28;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.brand-seller,
.price-cell,
.rating-cell,
.presence-cell {
  display: grid;
  gap: var(--space-1);
}

.brand-seller span,
.meta-line,
.price-cell,
.rating-cell {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.brand-seller strong,
.price-cell strong,
.rating-cell strong {
  color: var(--color-text);
  font-size: 0.9375rem;
}

.price-cell {
  min-width: 11rem;
  justify-items: end;
}

.presence-cell {
  max-width: 12rem;
  justify-items: start;
}

.code-cell {
  display: block;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.74rem;
  white-space: nowrap;
}

:deep(.table) {
  min-width: 1050px;
}

:deep(tbody tr) {
  height: 7.75rem;
}

:deep(td) {
  padding: 0.5rem 0.625rem;
}

:deep(td.table__cell--image) {
  padding: 0.25rem;
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
}
</style>
