<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { ArrowLeft, Eye, RefreshCw } from 'lucide-vue-next';

import {
  getWorkspaceOverview,
  recalculateWorkspaceOverview
} from '@/features/overview/workspaceOverview.api';
import {
  formatNumber,
  productMetrics,
  similarGroupItemTags,
  similarGroupTabs,
  similarGroupTitle,
  tagClass,
  toParserProduct,
  toParserSimilarProduct
} from '@/features/overview/workspaceOverview.helpers';
import type {
  WorkspaceOverview,
  WorkspaceOverviewProduct,
  WorkspaceOverviewSimilarProduct,
  WorkspaceOverviewSimilarProductGroup
} from '@/features/overview/workspaceOverview.types';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import Input from '@/shared/ui/Input.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import { useActiveWorkspace } from './useActiveWorkspace';

type TagFilter = 'all' | 'competitor' | 'idea';

const workspace = useActiveWorkspace();
const overview = ref<WorkspaceOverview | null>(null);
const selectedProduct = ref<ParserProductListItem | null>(null);
const loading = ref(false);
const recalculating = ref(false);
const error = ref('');
const filters = reactive({
  search: '',
  tagKey: 'all' as TagFilter,
  groupKey: ''
});
let requestVersion = 0;

const activeWorkspaceId = computed(() => workspace.activeWorkspaceId.value);
const hasProducts = computed(() => allProducts.value.length > 0);
const hasAnalysis = computed(() => Boolean(overview.value?.lastAnalysis));
const allProducts = computed<WorkspaceOverviewProduct[]>(() => {
  if (!overview.value) {
    return [];
  }

  return [
    ...overview.value.competitors.products,
    ...overview.value.ideas.products
  ];
});

const groupOptions = computed(() => {
  const groups = new Map<string, { key: string; label: string; count: number }>();

  for (const product of allProducts.value) {
    for (const group of similarGroupTabs(product)) {
      const existing = groups.get(group.key);
      groups.set(group.key, {
        key: group.key,
        label: similarGroupTitle(group),
        count: (existing?.count ?? 0) + group.items.length
      });
    }
  }

  return [...groups.values()].sort((left, right) => groupPriority(left.key) - groupPriority(right.key));
});

const filteredProducts = computed(() => {
  const search = filters.search.trim().toLocaleLowerCase('ru-RU');
  return allProducts.value.filter((product) => {
    if (filters.tagKey !== 'all' && product.tagKey !== filters.tagKey) {
      return false;
    }

    if (filters.groupKey && !similarGroupTabs(product).some((group) => group.key === filters.groupKey)) {
      return false;
    }

    if (!search) {
      return true;
    }

    return [
      product.name,
      product.brandName,
      product.sellerName,
      product.sourceCategory,
      product.sourceSubcategory,
      product.wbProductId,
      product.note
    ]
      .filter(Boolean)
      .some((value) => String(value).toLocaleLowerCase('ru-RU').includes(search));
  });
});

watch(
  activeWorkspaceId,
  () => {
    void loadOverview();
  },
  { immediate: true }
);

async function loadOverview(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  const version = ++requestVersion;

  if (!workspaceId) {
    overview.value = null;
    error.value = '';
    return;
  }

  loading.value = true;
  error.value = '';

  try {
    const response = await getWorkspaceOverview(workspaceId);
    if (version === requestVersion) {
      overview.value = response;
      if (filters.groupKey && !responseHasGroup(response, filters.groupKey)) {
        filters.groupKey = '';
      }
    }
  } catch (requestError) {
    if (version === requestVersion) {
      error.value = getProblemMessage(requestError, 'Не удалось загрузить кластерный анализ.');
      overview.value = null;
    }
  } finally {
    if (version === requestVersion) {
      loading.value = false;
    }
  }
}

async function recalculate(): Promise<void> {
  const workspaceId = activeWorkspaceId.value;
  if (!workspaceId || recalculating.value) {
    return;
  }

  recalculating.value = true;
  error.value = '';

  try {
    await recalculateWorkspaceOverview(workspaceId);
    await loadOverview();
  } catch (requestError) {
    error.value = getProblemMessage(requestError, 'Не удалось обновить анализ.');
  } finally {
    recalculating.value = false;
  }
}

function productGroups(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroup[] {
  const groups = similarGroupTabs(product);
  if (!filters.groupKey) {
    return groups;
  }

  return groups.filter((group) => group.key === filters.groupKey);
}

function openProduct(product: WorkspaceOverviewProduct): void {
  selectedProduct.value = toParserProduct(product);
}

function openSimilarProduct(product: WorkspaceOverviewSimilarProduct): void {
  selectedProduct.value = toParserSimilarProduct(product);
}

function tagLabel(tagKey: WorkspaceOverviewProduct['tagKey']): string {
  return tagKey === 'competitor' ? 'Конкурент' : 'Идея';
}

function tagTone(tagKey: WorkspaceOverviewProduct['tagKey']): 'warning' | 'info' {
  return tagKey === 'competitor' ? 'warning' : 'info';
}

function responseHasGroup(response: WorkspaceOverview, groupKey: string): boolean {
  return [...response.competitors.products, ...response.ideas.products]
    .some((product) => similarGroupTabs(product).some((group) => group.key === groupKey));
}

function groupPriority(key: string): number {
  const priority = [
    'price_disadvantage',
    'position_disadvantage',
    'review_count_disadvantage',
    'rating_disadvantage',
    'stock_disadvantage',
    'weak_competitor_cards'
  ];
  const index = priority.indexOf(key);
  return index === -1 ? priority.length : index;
}
</script>

<template>
  <div class="cluster-page">
    <PageHeader
      title="Кластерный анализ"
      description="Сравнение наблюдаемых товаров с похожими карточками в выбранной нише."
    >
      <RouterLink class="cluster-page__back app-operator-link" to="/workspace/market-products">
        <ArrowLeft :size="15" />
        Наблюдаемые товары
      </RouterLink>
      <Button variant="primary" :loading="recalculating" :disabled="!activeWorkspaceId" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить анализ
      </Button>
    </PageHeader>

    <p v-if="error" class="cluster-error">{{ error }}</p>

    <LoadingState v-if="loading" :rows="6" />

    <EmptyState
      v-else-if="!activeWorkspaceId"
      title="Рабочая область не выбрана"
      description="Выберите рабочую область, чтобы увидеть кластерный анализ наблюдаемых товаров."
    />

    <EmptyState
      v-else-if="!hasProducts"
      title="В рабочей области пока нет товаров"
      description="Добавьте рыночные карточки в наблюдаемые, чтобы сравнивать их с похожими товарами."
    >
      <RouterLink class="app-operator-link" to="/market/intelligence">
        Перейти в маркетинговую разведку
      </RouterLink>
    </EmptyState>

    <EmptyState
      v-else-if="!hasAnalysis"
      title="Анализ еще не рассчитан"
      description="Запустите обновление, чтобы увидеть похожие карточки и сравнения по наблюдаемым товарам."
    >
      <Button variant="primary" :loading="recalculating" @click="recalculate">
        <RefreshCw :size="16" />
        Обновить анализ
      </Button>
    </EmptyState>

    <template v-else>
      <section class="cluster-filters app-surface">
        <Input
          v-model="filters.search"
          label="Поиск"
          placeholder="Название, WB id, бренд, продавец или заметка"
        />

        <label class="cluster-field app-select-field">
          <span>Тег</span>
          <select v-model="filters.tagKey" class="app-select">
            <option value="all">Все</option>
            <option value="competitor">Конкуренты</option>
            <option value="idea">Идеи</option>
          </select>
        </label>

        <label class="cluster-field app-select-field">
          <span>Подборка сравнения</span>
          <select v-model="filters.groupKey" class="app-select">
            <option value="">Все подборки</option>
            <option v-for="group in groupOptions" :key="group.key" :value="group.key">
              {{ group.label }} · {{ group.count }}
            </option>
          </select>
        </label>
      </section>

      <section class="cluster-summary">
        <span>{{ filteredProducts.length }} из {{ allProducts.length }} товаров</span>
        <span>{{ groupOptions.length }} подборок</span>
      </section>

      <EmptyState
        v-if="filteredProducts.length === 0"
        title="Поиск не дал результатов"
        description="Измените поиск, тег или подборку сравнения."
      />

      <section v-else class="cluster-list">
        <article
          v-for="product in filteredProducts"
          :key="product.id"
          class="cluster-card app-operator-card"
        >
          <header class="cluster-card__head">
            <button class="cluster-card__image" type="button" @click="openProduct(product)">
              <MarketProductImage :src="product.thumbnailUrl" :alt="product.name" />
            </button>

            <div class="cluster-card__identity">
              <span>{{ product.sourceSubcategory || product.sourceCategory || 'Ниша не указана' }}</span>
              <h2>{{ product.name }}</h2>
              <p>{{ product.brandName || 'Бренд не указан' }} · {{ product.sellerName || 'Продавец не указан' }}</p>
              <p
                class="cluster-card__note"
                :class="{ 'cluster-card__note--empty': !product.note }"
              >
                {{ product.note || 'Заметка не добавлена' }}
              </p>
            </div>

            <Badge :tone="tagTone(product.tagKey)">{{ tagLabel(product.tagKey) }}</Badge>
          </header>

          <div class="cluster-card__metrics">
            <div v-for="metric in productMetrics(product)" :key="metric.label" class="app-operator-metric cluster-metric">
              <span>{{ metric.label }}</span>
              <strong>{{ metric.value }}</strong>
            </div>
          </div>

          <div v-if="productGroups(product).length" class="cluster-groups">
            <section
              v-for="group in productGroups(product)"
              :key="group.key"
              class="cluster-group"
            >
              <header>
                <div>
                  <h3>{{ similarGroupTitle(group) }}</h3>
                  <p>{{ group.description }}</p>
                </div>
                <span>{{ group.items.length }} товаров</span>
              </header>

              <div class="cluster-group__items">
                <button
                  v-for="item in group.items"
                  :key="`${group.key}:${item.product.productKey}`"
                  class="cluster-similar app-operator-card"
                  :class="{ 'cluster-similar--disabled': !item.product.parserProductRowId }"
                  type="button"
                  :disabled="!item.product.parserProductRowId"
                  @click="openSimilarProduct(item.product)"
                >
                  <MarketProductImage :src="item.product.thumbnailUrl" :alt="item.product.name" />
                  <div class="cluster-similar__body">
                    <strong>{{ item.product.name }}</strong>
                    <span>{{ item.product.sourceSubcategory || product.sourceSubcategory || 'Ниша не указана' }}</span>
                    <div class="cluster-tags">
                      <span
                        v-for="tag in similarGroupItemTags(product, item, group.key)"
                        :key="tag.key"
                        class="cluster-tag"
                        :class="tagClass(tag, 'cluster-tag')"
                      >
                        {{ tag.label }}
                      </span>
                    </div>
                  </div>
                </button>
              </div>
            </section>
          </div>

          <EmptyState
            v-else
            title="Кластеры еще не рассчитаны"
            description="Обновите анализ, чтобы увидеть похожие карточки по этому товару."
          />

          <footer class="cluster-card__actions">
            <button class="app-operator-link" type="button" @click="openProduct(product)">
              <Eye :size="15" />
              Карточка
            </button>
          </footer>
        </article>
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
.cluster-page {
  display: grid;
  gap: var(--space-4);
}

.cluster-page__back {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
}

.cluster-error {
  margin: 0;
  border: 1px solid var(--state-danger-border-strong);
  border-radius: var(--radius-lg);
  background: var(--state-danger-soft);
  padding: var(--space-3);
  color: var(--state-danger);
  font-weight: 650;
}

.cluster-filters {
  display: grid;
  gap: var(--space-3);
  border-color: var(--color-border-strong);
  padding: var(--space-3);
}

.cluster-field {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.cluster-summary {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--operator-body-size);
  font-weight: 650;
}

.cluster-list,
.cluster-groups {
  display: grid;
  gap: var(--space-4);
}

.cluster-card {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
}

.cluster-card__head {
  display: grid;
  grid-template-columns: 5.5rem minmax(0, 1fr) auto;
  gap: var(--space-3);
  align-items: start;
}

.cluster-card__image {
  overflow: hidden;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  padding: 0;
  aspect-ratio: 3 / 4;
  cursor: pointer;
}

.cluster-card__identity {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
}

.cluster-card__identity h2,
.cluster-card__identity p,
.cluster-group h3,
.cluster-group p {
  margin: 0;
}

.cluster-card__identity h2 {
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 820;
  line-height: 1.25;
}

.cluster-card__identity > span,
.cluster-card__identity p,
.cluster-group p,
.cluster-group header > span,
.cluster-similar__body > span {
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
}

.cluster-card__note {
  margin-top: var(--space-1);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-metric-bg);
  padding: 0.45rem 0.6rem;
  line-height: 1.4;
}

.cluster-card__note--empty {
  color: var(--color-text-subtle);
}

.cluster-card__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--space-2);
}

.cluster-metric {
  display: grid;
  gap: 0.25rem;
  padding: 0.55rem 0.65rem;
}

.cluster-metric span {
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
  font-weight: 760;
  text-transform: uppercase;
}

.cluster-metric strong {
  color: var(--color-text);
  font-size: var(--operator-value-size);
  font-weight: 820;
}

.cluster-group {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-lg);
  background:
    linear-gradient(90deg, rgb(127 29 29 / 0.03), transparent 42%),
    var(--operator-card-bg);
  padding: var(--space-3);
}

.cluster-group header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
}

.cluster-group h3 {
  color: var(--color-text);
  font-size: var(--operator-title-size);
  font-weight: 820;
}

.cluster-group__items {
  display: grid;
  grid-template-columns: repeat(auto-fill, 16rem);
  justify-content: start;
  gap: var(--space-2);
}

.cluster-similar {
  display: grid;
  grid-template-columns: 4rem minmax(0, 1fr);
  gap: var(--space-2);
  align-items: start;
  border-color: var(--operator-border-muted);
  color: var(--color-text);
  cursor: pointer;
  font: inherit;
  padding: var(--space-2);
  text-align: left;
}

.cluster-similar:hover:not(:disabled) {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
}

.cluster-similar--disabled {
  cursor: default;
  opacity: 0.78;
}

.cluster-similar__body {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.cluster-similar__body strong {
  overflow: hidden;
  color: var(--color-text);
  font-size: var(--operator-body-size);
  font-weight: 800;
  line-height: 1.25;
  text-overflow: ellipsis;
}

.cluster-tags {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: var(--space-1);
}

.cluster-tag {
  display: inline-flex;
  width: fit-content;
  max-width: 100%;
  border: 1px solid var(--operator-border-muted);
  border-radius: 999px;
  background: var(--operator-metric-bg);
  color: var(--color-text);
  font-size: var(--operator-meta-size);
  font-weight: 760;
  line-height: 1.25;
  padding: 0.28rem 0.55rem;
}

.cluster-tag--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.cluster-tag--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger-text);
}

.cluster-tag--warning {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.cluster-tag--neutral {
  border-color: var(--operator-border-muted);
  background: var(--operator-metric-bg);
  color: var(--color-text-muted);
}

.cluster-card__actions {
  border-top: 1px solid var(--color-border);
  padding-top: var(--space-3);
}

@media (min-width: 880px) {
  .cluster-filters {
    grid-template-columns: minmax(18rem, 1fr) minmax(10rem, 14rem) minmax(14rem, 20rem);
    align-items: end;
  }
}

@media (max-width: 980px) {
  .cluster-card__head,
  .cluster-card__metrics {
    grid-template-columns: 1fr;
  }
}
</style>
