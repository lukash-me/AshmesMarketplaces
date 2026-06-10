<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { Eye, History, Save, Trash2 } from 'lucide-vue-next';

import type { PagedResponse } from '@/entities/pagination';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import Input from '@/shared/ui/Input.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import {
  deleteWorkspaceMarketProduct,
  getWorkspaceMarketProductHistory,
  getWorkspaceMarketProducts,
  updateWorkspaceMarketProduct
} from './workspaceMarketProducts.api';
import type {
  WorkspaceMarketProductHistory,
  WorkspaceMarketProductListItem,
  WorkspaceMarketProductTagKey
} from './workspaceMarketProducts.types';
import { useActiveWorkspace } from './useActiveWorkspace';

type TagFilter = WorkspaceMarketProductTagKey | 'all';

type ProductDraft = {
  tagKey: WorkspaceMarketProductTagKey;
  note: string;
};

const pageSize = 20;
const workspace = useActiveWorkspace();
const products = ref<WorkspaceMarketProductListItem[]>([]);
const totalCount = ref(0);
const page = ref(1);
const search = ref('');
const tagFilter = ref<TagFilter>('all');
const filterForm = reactive({
  search: '',
  tagKey: ''
});
const loading = ref(false);
const error = ref('');
const drafts = reactive<Record<string, ProductDraft>>({});
const savingIds = ref<Set<string>>(new Set());
const deletingIds = ref<Set<string>>(new Set());
const selectedProduct = ref<ParserProductListItem | null>(null);
const pendingDeleteProduct = ref<WorkspaceMarketProductListItem | null>(null);
const historyProductId = ref<string | null>(null);
const historyLoading = ref(false);
const historyError = ref('');
const history = ref<WorkspaceMarketProductHistory | null>(null);
let requestVersion = 0;

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)));
const activeWorkspaceId = computed(() => workspace.activeWorkspaceId.value);
const hasRows = computed(() => products.value.length > 0);
const tagOptions = ['Конкурент', 'Идея'];
const chips = computed(() => {
  const items: Array<{ key: 'search' | 'tagKey'; label: string; value: string }> = [];

  if (search.value) {
    items.push({ key: 'search', label: 'Поиск', value: search.value });
  }

  if (tagFilter.value !== 'all') {
    items.push({ key: 'tagKey', label: 'Тег', value: tagLabel(tagFilter.value) });
  }

  return items;
});

watch(
  activeWorkspaceId,
  () => {
    page.value = 1;
    void loadProducts();
  },
  { immediate: true }
);

function tagLabel(tagKey: WorkspaceMarketProductTagKey): string {
  return tagKey === 'competitor' ? 'Конкурент' : 'Идея';
}

function tagTone(tagKey: WorkspaceMarketProductTagKey): 'warning' | 'info' {
  return tagKey === 'competitor' ? 'warning' : 'info';
}

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU').format(value);
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

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function productPrice(product: WorkspaceMarketProductListItem): string {
  return formatMoney(product.priceWbWallet ?? product.priceDiscounted ?? product.priceRegular);
}

function productStock(product: WorkspaceMarketProductListItem): string {
  return product.totalQuantity === null || product.totalQuantity === undefined
    ? 'Нет данных'
    : formatNumber(product.totalQuantity);
}

function applyFilters(): void {
  search.value = filterForm.search.trim();
  tagFilter.value = tagFromLabel(filterForm.tagKey);
  page.value = 1;
  void loadProducts();
}

function resetFilters(): void {
  filterForm.search = '';
  filterForm.tagKey = '';
  search.value = '';
  tagFilter.value = 'all';
  page.value = 1;
  void loadProducts();
}

function removeFilter(key: 'search' | 'tagKey'): void {
  if (key === 'search') {
    filterForm.search = '';
    search.value = '';
  } else {
    filterForm.tagKey = '';
    tagFilter.value = 'all';
  }

  page.value = 1;
  void loadProducts();
}

function isActive(value: string): boolean {
  return value.trim() !== '';
}

function tagFromLabel(value: string): TagFilter {
  if (value === 'Конкурент') {
    return 'competitor';
  }

  if (value === 'Идея') {
    return 'idea';
  }

  return 'all';
}

async function loadProducts(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const version = ++requestVersion;

  if (!workspaceId) {
    products.value = [];
    totalCount.value = 0;
    return;
  }

  loading.value = true;
  error.value = '';

  try {
    const response = await getWorkspaceMarketProducts(workspaceId, {
      page: page.value,
      pageSize,
      search: search.value.trim() || undefined,
      tagKey: tagFilter.value === 'all' ? undefined : tagFilter.value
    });

    if (version !== requestVersion) {
      return;
    }

    applyResponse(response);
  } catch (requestError) {
    if (version === requestVersion) {
      error.value = getProblemMessage(requestError, 'Не удалось загрузить товары рабочей области.');
      products.value = [];
      totalCount.value = 0;
    }
  } finally {
    if (version === requestVersion) {
      loading.value = false;
    }
  }
}

function applyResponse(response: PagedResponse<WorkspaceMarketProductListItem>): void {
  products.value = response.items;
  totalCount.value = response.totalCount;

  for (const product of response.items) {
    drafts[product.id] = {
      tagKey: product.tagKey,
      note: product.note ?? ''
    };
  }
}

async function saveProduct(product: WorkspaceMarketProductListItem): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const draft = drafts[product.id];

  if (!workspaceId || !draft) {
    return;
  }

  setSaving(product.id, true);
  try {
    const updated = await updateWorkspaceMarketProduct(workspaceId, product.id, {
      tagKey: draft.tagKey,
      note: draft.note.trim() || null
    });
    const index = products.value.findIndex((item) => item.id === product.id);
    if (index >= 0) {
      products.value[index] = updated;
    }
    drafts[product.id] = {
      tagKey: updated.tagKey,
      note: updated.note ?? ''
    };
  } catch (requestError) {
    window.alert(getProblemMessage(requestError, 'Не удалось сохранить товар.'));
  } finally {
    setSaving(product.id, false);
  }
}

function requestDeleteProduct(product: WorkspaceMarketProductListItem): void {
  pendingDeleteProduct.value = product;
}

function cancelDeleteProduct(): void {
  pendingDeleteProduct.value = null;
}

async function confirmDeleteProduct(): Promise<void> {
  const product = pendingDeleteProduct.value;
  if (!product) {
    return;
  }

  await removeProduct(product);
}

async function removeProduct(product: WorkspaceMarketProductListItem): Promise<void> {
  const workspaceId = activeWorkspaceId.value;

  if (!workspaceId) {
    return;
  }

  setDeleting(product.id, true);
  try {
    await deleteWorkspaceMarketProduct(workspaceId, product.id);
    products.value = products.value.filter((item) => item.id !== product.id);
    totalCount.value = Math.max(0, totalCount.value - 1);
    delete drafts[product.id];
    if (historyProductId.value === product.id) {
      closeHistory();
    }
    if (pendingDeleteProduct.value?.id === product.id) {
      pendingDeleteProduct.value = null;
    }
  } catch (requestError) {
    window.alert(getProblemMessage(requestError, 'Не удалось удалить товар.'));
  } finally {
    setDeleting(product.id, false);
  }
}

async function toggleHistory(product: WorkspaceMarketProductListItem): Promise<void> {
  if (historyProductId.value === product.id) {
    closeHistory();
    return;
  }

  const workspaceId = activeWorkspaceId.value;
  if (!workspaceId) {
    return;
  }

  historyProductId.value = product.id;
  history.value = null;
  historyError.value = '';
  historyLoading.value = true;

  try {
    history.value = await getWorkspaceMarketProductHistory(workspaceId, product.id);
  } catch (requestError) {
    historyError.value = getProblemMessage(requestError, 'Не удалось загрузить историю изменений.');
  } finally {
    historyLoading.value = false;
  }
}

function closeHistory(): void {
  historyProductId.value = null;
  history.value = null;
  historyError.value = '';
}

function setSaving(id: string, saving: boolean): void {
  const next = new Set(savingIds.value);
  if (saving) {
    next.add(id);
  } else {
    next.delete(id);
  }
  savingIds.value = next;
}

function setDeleting(id: string, deleting: boolean): void {
  const next = new Set(deletingIds.value);
  if (deleting) {
    next.add(id);
  } else {
    next.delete(id);
  }
  deletingIds.value = next;
}

function openProduct(product: WorkspaceMarketProductListItem): void {
  selectedProduct.value = toParserProduct(product);
}

function closeProduct(): void {
  selectedProduct.value = null;
}

function toParserProduct(product: WorkspaceMarketProductListItem): ParserProductListItem {
  return {
    id: product.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: product.latestObservedAtUtc ?? product.dateUpdate,
    wbProductId: product.wbProductId,
    wbRootId: product.wbRootId,
    name: product.name,
    brandName: product.brandName,
    sellerName: product.sellerName,
    priceRegular: product.priceRegular,
    priceDiscounted: product.priceDiscounted,
    priceWbWallet: product.priceWbWallet,
    discountPercent: null,
    totalQuantity: product.totalQuantity,
    ratingRounded: product.reviewRating === null ? null : Math.round(product.reviewRating ?? 0),
    reviewRating: product.reviewRating,
    feedbackCount: product.feedbackCount,
    sourceCategory: product.sourceCategory,
    sourceSubcategory: product.sourceSubcategory,
    sourceQuery: product.sourceQuery,
    thumbnailUrl: product.thumbnailUrl,
    rank: null,
    position: product.positionAbsolute === null
      ? null
      : {
          state: 'observed',
          absolutePosition: product.positionAbsolute,
          observedRangeLimit: null,
          query: product.sourceQuery,
          sourceCategory: product.sourceCategory,
          sourceSubcategory: product.sourceSubcategory,
          observedAtUtc: product.latestObservedAtUtc
        },
    logistics: null
  };
}

function changePage(nextPage: number): void {
  page.value = Math.min(Math.max(1, nextPage), totalPages.value);
  void loadProducts();
}
</script>

<template>
  <div class="workspace-products">
    <PageHeader
      title="Наблюдаемые товары"
      description="Рыночные карточки, которые вы добавили в рабочую область для сравнения и отслеживания изменений."
    />

    <form class="filters app-surface" @submit.prevent="applyFilters">
      <label v-if="workspace.hasMultipleWorkspaces.value" class="workspace-products__field">
        <span>Рабочая область</span>
        <select v-model="workspace.selectedWorkspaceId.value" class="app-select">
          <option
            v-for="option in workspace.workspaceOptions.value"
            :key="option.idWorkspace"
            :value="option.idWorkspace"
          >
            {{ option.idWorkspace }}
          </option>
        </select>
      </label>

      <div class="filters__search">
        <Input
          v-model="filterForm.search"
          class="filter-control"
          :class="{ 'filter-control--active': isActive(filterForm.search) }"
          label="Поиск"
          placeholder="Название, WB id, бренд, продавец или заметка"
        />
        <div class="filters__actions">
          <Button class="filters__apply" type="submit">Применить</Button>
          <Button v-if="chips.length" type="button" variant="ghost" @click="resetFilters">Сбросить</Button>
        </div>
      </div>

      <div class="filters__selects">
        <MarketFilterSelect
          v-model="filterForm.tagKey"
          class="filter-control"
          :class="{ 'filter-control--active': isActive(filterForm.tagKey) }"
          label="Тег"
          placeholder="Все теги"
          search-placeholder="Найти тег"
          :options="tagOptions"
        />
      </div>

      <div v-if="chips.length" class="filters__chips" aria-label="Активные фильтры наблюдаемых товаров">
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
          <span aria-hidden="true">x</span>
        </button>
      </div>
    </form>

    <LoadingState v-if="loading && !hasRows" title="Загружаем товары" />

    <EmptyState
      v-else-if="!activeWorkspaceId"
      title="Рабочая область недоступна"
      description="У пользователя нет доступной рабочей области."
    />

    <EmptyState
      v-else-if="error"
      title="Данные временно недоступны"
      :description="error"
    >
      <Button variant="secondary" @click="loadProducts">Повторить</Button>
    </EmptyState>

    <EmptyState
      v-else-if="!hasRows"
      title="В рабочей области пока нет товаров"
      description="Добавляйте рыночные карточки из маркетинговой разведки, чтобы следить за ними здесь."
    />

    <section v-else class="workspace-products__list" :class="{ 'workspace-products__list--loading': loading }">
      <article
        v-for="product in products"
        :key="product.id"
        class="workspace-product app-operator-card"
      >
        <div class="workspace-product__media">
          <MarketProductImage :src="product.thumbnailUrl" :alt="product.name" />
        </div>

        <div class="workspace-product__main">
          <div class="workspace-product__head">
            <div>
              <span class="workspace-product__context">
                {{ product.sourceSubcategory || product.sourceCategory || 'Ниша не указана' }}
              </span>
              <h2>{{ product.name }}</h2>
              <p>{{ product.brandName || 'Бренд не указан' }} · {{ product.sellerName || 'Продавец не указан' }}</p>
            </div>
            <Badge :tone="tagTone(product.tagKey)">{{ tagLabel(product.tagKey) }}</Badge>
          </div>

          <div class="workspace-product__metrics">
            <div class="app-operator-metric">
              <span>Цена</span>
              <strong>{{ productPrice(product) }}</strong>
            </div>
            <div class="app-operator-metric">
              <span>Позиция</span>
              <strong>{{ product.positionAbsolute ? `#${formatNumber(product.positionAbsolute)}` : 'Нет данных' }}</strong>
            </div>
            <div class="app-operator-metric">
              <span>Рейтинг WB</span>
              <strong>{{ product.reviewRating ?? 'Нет данных' }}</strong>
            </div>
            <div class="app-operator-metric">
              <span>Отзывы WB</span>
              <strong>{{ formatNumber(product.feedbackCount) }}</strong>
            </div>
            <div class="app-operator-metric">
              <span>Остаток</span>
              <strong>{{ productStock(product) }}</strong>
            </div>
          </div>

          <div class="workspace-product__edit">
            <label class="workspace-products__field app-select-field">
              <span>Тег</span>
              <select v-model="drafts[product.id].tagKey" class="app-select">
                <option value="competitor">Конкурент</option>
                <option value="idea">Идея</option>
              </select>
            </label>

            <label class="workspace-products__field workspace-products__field--note">
              <span>Заметка</span>
              <textarea
                v-model="drafts[product.id].note"
                class="workspace-product__note"
                rows="2"
                placeholder="Что важно проверить по этой карточке"
              />
            </label>
          </div>

          <div v-if="historyProductId === product.id" class="workspace-product__history app-operator-panel">
            <LoadingState v-if="historyLoading" title="Загружаем историю" />
            <p v-else-if="historyError" class="workspace-product__history-error">{{ historyError }}</p>
            <div v-else-if="history" class="workspace-product__history-grid">
              <article
                v-for="group in history.groups"
                :key="group.key"
                class="workspace-product__history-group"
              >
                <h3>{{ group.label }}</h3>
                <p v-if="group.items.length === 0">Истории пока недостаточно.</p>
                <ol v-else>
                  <li v-for="item in group.items" :key="`${group.key}-${item.observedAtUtc}-${item.displayValue}`">
                    <span>{{ formatDate(item.observedAtUtc) }}</span>
                    <strong>{{ item.displayValue }}</strong>
                  </li>
                </ol>
              </article>
            </div>
          </div>

          <div class="workspace-product__actions">
            <Button
              variant="primary"
              :loading="savingIds.has(product.id)"
              @click="saveProduct(product)"
            >
              <Save :size="15" />
              Сохранить
            </Button>
            <Button variant="secondary" @click="toggleHistory(product)">
              <History :size="15" />
              {{ historyProductId === product.id ? 'Скрыть историю' : 'История' }}
            </Button>
            <Button variant="secondary" @click="openProduct(product)">
              <Eye :size="15" />
              Карточка
            </Button>
            <Button
              variant="danger"
              :loading="deletingIds.has(product.id)"
              @click="requestDeleteProduct(product)"
            >
              <Trash2 :size="15" />
              Удалить
            </Button>
          </div>
        </div>
      </article>

      <footer class="workspace-products__pager">
        <Button variant="secondary" :disabled="page <= 1" @click="changePage(page - 1)">Назад</Button>
        <span>{{ page }} / {{ totalPages }}</span>
        <Button variant="secondary" :disabled="page >= totalPages" @click="changePage(page + 1)">Дальше</Button>
      </footer>
    </section>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="closeProduct"
    />

    <Teleport to="body">
      <div v-if="pendingDeleteProduct" class="confirm-modal" role="presentation">
        <button
          class="confirm-modal__backdrop"
          type="button"
          aria-label="Отменить удаление"
          @click="cancelDeleteProduct"
        />
        <section
          class="confirm-modal__panel app-surface app-operator-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="workspace-delete-title"
        >
          <div class="confirm-modal__icon">
            <Trash2 :size="20" />
          </div>
          <div class="confirm-modal__content">
            <h2 id="workspace-delete-title">Удалить товар из наблюдаемых?</h2>
            <p>
              {{ pendingDeleteProduct.name }}
            </p>
          </div>
          <div class="confirm-modal__actions">
            <Button variant="secondary" @click="cancelDeleteProduct">Отмена</Button>
            <Button
              variant="danger"
              :loading="deletingIds.has(pendingDeleteProduct.id)"
              @click="confirmDeleteProduct"
            >
              <Trash2 :size="15" />
              Удалить
            </Button>
          </div>
        </section>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.workspace-products {
  display: grid;
  gap: var(--space-4);
}

.workspace-product,
.workspace-product__history {
  border-color: var(--operator-border-muted);
}

.filters {
  position: relative;
  z-index: 3;
  display: grid;
  gap: var(--space-3);
  border-color: var(--color-border-strong);
  background:
    linear-gradient(90deg, rgb(249 115 22 / 0.035), transparent 42%),
    var(--surface-panel);
  overflow: visible;
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
  min-height: 2.35rem;
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.24), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 10px 24px rgb(249 115 22 / 0.08);
  font-weight: 760;
}

.filters__apply:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.28), rgb(249 115 22 / 0.1)),
    var(--color-surface-hover);
}

.filters__apply:focus-visible {
  box-shadow: var(--focus-ring), 0 10px 24px rgb(249 115 22 / 0.12);
}

.filter-control--active :deep(.field__control),
.filter-control--active :deep(.filter-select__trigger) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--surface-control-focus);
  box-shadow: inset 0 0 0 1px rgb(249 115 22 / 0.07), 0 0 0 1px rgb(249 115 22 / 0.04);
}

.filter-control--active :deep(.field__label),
.filter-control--active :deep(.filter-select__label),
.filter-control--active :deep(.filter-select__chevrons) {
  color: var(--accent-ember-text);
}

.filters__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filters__chip {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--accent-primary-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  padding: 0.35rem 0.55rem;
  font-size: 0.75rem;
  font-weight: 700;
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--accent-primary-hover-border);
  background: var(--color-surface-hover);
  outline: none;
}

.workspace-products__field {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.workspace-products__field--note {
  min-width: 0;
}

.workspace-products__list {
  display: grid;
  gap: var(--space-3);
}

.workspace-products__list--loading {
  opacity: 0.7;
}

.workspace-product {
  display: grid;
  grid-template-columns: 7.5rem minmax(0, 1fr);
  gap: var(--space-3);
  padding: var(--space-3);
}

.workspace-product__media {
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  aspect-ratio: 3 / 4;
}

.workspace-product__main,
.workspace-product__head,
.workspace-product__edit,
.workspace-product__history-grid {
  display: grid;
  gap: var(--space-3);
}

.workspace-product__head {
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
}

.workspace-product__head h2,
.workspace-product__head p,
.workspace-product__history-group h3,
.workspace-product__history-group p {
  margin: 0;
}

.workspace-product__head h2 {
  font-size: var(--operator-title-size);
}

.workspace-product__head p,
.workspace-product__context,
.workspace-product__history-group p,
.workspace-product__history-group span {
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.workspace-product__context {
  display: block;
  margin-bottom: var(--space-1);
}

.workspace-product__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--space-2);
}

.workspace-product__metrics .app-operator-metric {
  display: grid;
  gap: 0.25rem;
  align-content: start;
  min-height: 3.4rem;
  padding: 0.55rem 0.65rem;
  border-color: var(--operator-border-muted);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.035);
}

.workspace-product__metrics .app-operator-metric span {
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
  font-weight: 760;
  letter-spacing: 0.02em;
  line-height: 1.15;
  text-transform: uppercase;
}

.workspace-product__metrics .app-operator-metric strong {
  min-width: 0;
  color: var(--color-text);
  font-size: var(--operator-value-size);
  font-weight: 820;
  line-height: 1.2;
  overflow-wrap: anywhere;
}

.workspace-product__edit {
  grid-template-columns: 11rem minmax(0, 1fr);
  align-items: start;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background:
    linear-gradient(90deg, rgb(127 29 29 / 0.035), transparent 42%),
    var(--operator-card-bg);
  padding: var(--space-3);
}

.workspace-product__note {
  min-height: 4.5rem;
  width: 100%;
  border: 1px solid var(--select-control-border);
  border-radius: var(--radius-md);
  background: var(--select-control-bg);
  color: var(--select-control-text);
  line-height: 1.45;
  outline: none;
  padding: 0.6rem 0.75rem;
  resize: vertical;
  transition: background 140ms ease, border-color 140ms ease, box-shadow 140ms ease, color 140ms ease;
}

.workspace-product__note::placeholder {
  color: var(--color-text-subtle);
}

.workspace-product__note:hover {
  border-color: var(--select-control-border-hover);
  background: var(--select-control-bg-hover);
}

.workspace-product__note:focus {
  border-color: var(--select-control-border-focus);
  background: var(--select-control-bg-focus);
  box-shadow: var(--focus-ring);
}

.workspace-product__history {
  padding: var(--space-3);
}

.workspace-product__history-grid {
  grid-template-columns: repeat(5, minmax(0, 1fr));
}

.workspace-product__history-group {
  display: grid;
  gap: var(--space-2);
}

.workspace-product__history-group ol {
  display: grid;
  gap: var(--space-1);
  margin: 0;
  padding: 0;
  list-style: none;
}

.workspace-product__history-group li {
  display: flex;
  justify-content: space-between;
  gap: var(--space-2);
  border-bottom: 1px solid var(--color-border);
  padding-bottom: var(--space-1);
}

.workspace-product__history-error {
  margin: 0;
  color: var(--state-danger-text);
}

.workspace-product__actions,
.workspace-products__pager {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.workspace-products__pager {
  justify-content: flex-end;
  color: var(--color-text-muted);
  font-weight: 650;
}

.confirm-modal {
  position: fixed;
  z-index: 80;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--space-4);
}

.confirm-modal__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: rgb(2 6 23 / 0.62);
  backdrop-filter: blur(3px);
}

.confirm-modal__panel {
  position: relative;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  gap: var(--space-3);
  width: min(31rem, 100%);
  border-color: var(--operator-border-muted);
  padding: var(--space-4);
  box-shadow: var(--shadow-panel), 0 24px 60px rgb(0 0 0 / 0.28);
}

.confirm-modal__icon {
  display: grid;
  width: 2.65rem;
  height: 2.65rem;
  place-items: center;
  border: 1px solid var(--state-danger-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, rgb(244 63 94 / 0.16), rgb(127 29 29 / 0.06)),
    var(--operator-metric-bg);
  color: var(--state-danger-text);
}

.confirm-modal__content {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.confirm-modal__content h2,
.confirm-modal__content p {
  margin: 0;
}

.confirm-modal__content h2 {
  color: var(--color-text);
  font-size: 1.08rem;
  font-weight: 820;
}

.confirm-modal__content p {
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
  line-height: 1.45;
}

.confirm-modal__actions {
  grid-column: 1 / -1;
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
  padding-top: var(--space-2);
}

@media (max-width: 1100px) {
  .filters__selects,
  .workspace-product__metrics,
  .workspace-product__history-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (min-width: 860px) {
  .filters__search {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__selects {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .filters__search,
  .filters__selects,
  .workspace-product,
  .workspace-product__edit,
  .workspace-product__head,
  .workspace-product__metrics,
  .workspace-product__history-grid {
    grid-template-columns: 1fr;
  }

  .workspace-product__media {
    width: 7.5rem;
  }
}
</style>
