<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import {
  compactId,
  fieldValue,
  formatDateTime,
  formatJson,
  formatScore,
  getRecommendationNeutralTone,
  getRecommendationObjectTypeLabel,
  getRecommendationTypeLabel
} from './recommendationDisplay';
import {
  getRecommendation,
  getRecommendationCategories,
  getRecommendationProducts
} from './recommendations.api';
import type {
  RecommendationCategoryListItem,
  RecommendationDetail,
  RecommendationListItem,
  RecommendationProductListItem
} from './recommendations.types';

const LINKED_ROWS_PAGE_SIZE = 200;

const props = defineProps<{
  open: boolean;
  recommendation: RecommendationListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<RecommendationDetail | null>(null);
const products = ref<RecommendationProductListItem[]>([]);
const categories = ref<RecommendationCategoryListItem[]>([]);
const productsTotalCount = ref(0);
const categoriesTotalCount = ref(0);
const detailLoading = ref(false);
const productsLoading = ref(false);
const categoriesLoading = ref(false);
const detailError = ref('');
const productsError = ref('');
const categoriesError = ref('');
let detailLoadVersion = 0;
let productsLoadVersion = 0;
let categoriesLoadVersion = 0;

const displayRecommendation = computed(() => detail.value ?? props.recommendation);
const formattedExplanation = computed(() => formatJson(detail.value?.explanation));
const formattedSnapshot = computed(() => formatJson(detail.value?.snapshot));
const hasHiddenProducts = computed(() => productsTotalCount.value > LINKED_ROWS_PAGE_SIZE);
const hasHiddenCategories = computed(() => categoriesTotalCount.value > LINKED_ROWS_PAGE_SIZE);

watch(
  () => [props.open, props.recommendation?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      products.value = [];
      categories.value = [];
      productsTotalCount.value = 0;
      categoriesTotalCount.value = 0;
      detailError.value = '';
      productsError.value = '';
      categoriesError.value = '';
      return;
    }

    await Promise.all([loadDetail(id), loadProducts(id), loadCategories(id)]);
  },
  { immediate: true }
);

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});

async function loadDetail(id: string): Promise<void> {
  const version = ++detailLoadVersion;

  detailLoading.value = true;
  detailError.value = '';
  detail.value = null;

  try {
    const response = await getRecommendation(id);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load recommendation details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadProducts(idRecommendation: string): Promise<void> {
  const version = ++productsLoadVersion;

  productsLoading.value = true;
  productsError.value = '';
  products.value = [];
  productsTotalCount.value = 0;

  try {
    const response = await getRecommendationProducts({
      page: 1,
      pageSize: LINKED_ROWS_PAGE_SIZE,
      sort: 'idProduct',
      idRecommendation
    });

    if (version === productsLoadVersion) {
      products.value = response.items;
      productsTotalCount.value = response.totalCount;
    }
  } catch (err) {
    if (version === productsLoadVersion) {
      productsError.value = getProblemMessage(err, 'Unable to load linked product IDs.');
    }
  } finally {
    if (version === productsLoadVersion) {
      productsLoading.value = false;
    }
  }
}

async function loadCategories(idRecommendation: string): Promise<void> {
  const version = ++categoriesLoadVersion;

  categoriesLoading.value = true;
  categoriesError.value = '';
  categories.value = [];
  categoriesTotalCount.value = 0;

  try {
    const response = await getRecommendationCategories({
      page: 1,
      pageSize: LINKED_ROWS_PAGE_SIZE,
      sort: 'idCategory',
      idRecommendation
    });

    if (version === categoriesLoadVersion) {
      categories.value = response.items;
      categoriesTotalCount.value = response.totalCount;
    }
  } catch (err) {
    if (version === categoriesLoadVersion) {
      categoriesError.value = getProblemMessage(err, 'Unable to load linked category IDs.');
    }
  } finally {
    if (version === categoriesLoadVersion) {
      categoriesLoading.value = false;
    }
  }
}

function close(): void {
  emit('close');
}

function onKeydown(event: KeyboardEvent): void {
  if (props.open && event.key === 'Escape') {
    close();
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button
        class="drawer-shell__backdrop"
        type="button"
        aria-label="Close recommendation detail"
        @click="close"
      />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="recommendation-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayRecommendation" class="drawer__title">
            <Badge :tone="getRecommendationNeutralTone()">
              {{ getRecommendationTypeLabel(displayRecommendation.type) }}
            </Badge>
            <h2 id="recommendation-detail-title">
              Recommendation {{ compactId(displayRecommendation.id) }}
            </h2>
            <p>Model {{ compactId(displayRecommendation.idModel) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close recommendation detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayRecommendation" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Score</span>
              <strong class="numeric">{{ formatScore(displayRecommendation.score) }}</strong>
            </div>
            <div>
              <span>Type</span>
              <strong>{{ getRecommendationTypeLabel(displayRecommendation.type) }}</strong>
            </div>
            <div>
              <span>Created</span>
              <strong class="numeric">{{ formatDateTime(displayRecommendation.dateCreate) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Recommendation ID</dt><dd>{{ displayRecommendation.id }}</dd></div>
              <div><dt>Model ID</dt><dd>{{ displayRecommendation.idModel }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Recommendation fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Score</dt><dd>{{ formatScore(displayRecommendation.score) }}</dd></div>
              <div>
                <dt>Type</dt>
                <dd>
                  <Badge :tone="getRecommendationNeutralTone()">
                    {{ getRecommendationTypeLabel(displayRecommendation.type) }}
                  </Badge>
                </dd>
              </div>
              <div>
                <dt>Object type</dt>
                <dd>
                  <Badge :tone="getRecommendationNeutralTone()">
                    {{ getRecommendationObjectTypeLabel(displayRecommendation.typeObject) }}
                  </Badge>
                </dd>
              </div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayRecommendation.dateCreate) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayRecommendation.dateUpdate) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Linked products</h3>
              <span class="numeric">{{ productsTotalCount }} total</span>
            </header>

            <LoadingState v-if="productsLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="productsError" class="drawer__notice">
              {{ productsError }}
            </div>

            <div v-else-if="products.length === 0" class="drawer__placeholder">
              No linked product IDs returned by the current API response.
            </div>

            <div v-else class="linked-list">
              <div v-if="hasHiddenProducts" class="drawer__placeholder">
                Showing first {{ LINKED_ROWS_PAGE_SIZE }} of {{ productsTotalCount }} linked product rows.
              </div>

              <code v-for="product in products" :key="product.idProduct" :title="product.idProduct">
                {{ product.idProduct }}
              </code>
            </div>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Linked categories</h3>
              <span class="numeric">{{ categoriesTotalCount }} total</span>
            </header>

            <LoadingState v-if="categoriesLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="categoriesError" class="drawer__notice">
              {{ categoriesError }}
            </div>

            <div v-else-if="categories.length === 0" class="drawer__placeholder">
              No linked category IDs returned by the current API response.
            </div>

            <div v-else class="linked-list">
              <div v-if="hasHiddenCategories" class="drawer__placeholder">
                Showing first {{ LINKED_ROWS_PAGE_SIZE }} of {{ categoriesTotalCount }} linked category rows.
              </div>

              <code v-for="category in categories" :key="category.idCategory" :title="category.idCategory">
                {{ category.idCategory }}
              </code>
            </div>
          </section>

          <section class="drawer__section">
            <h3>Explanation JSON</h3>
            <pre v-if="formattedExplanation" class="drawer__json">{{ formattedExplanation }}</pre>
            <div v-else class="drawer__placeholder">No explanation JSON returned by the current API response.</div>
          </section>

          <section class="drawer__section">
            <h3>Snapshot JSON</h3>
            <pre v-if="formattedSnapshot" class="drawer__json">{{ formattedSnapshot }}</pre>
            <div v-else class="drawer__placeholder">No snapshot JSON returned by the current API response.</div>
          </section>
        </div>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.drawer-shell {
  position: fixed;
  inset: 0;
  z-index: 50;
}

.drawer-shell__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: var(--theme-backdrop);
}

.drawer {
  position: absolute;
  top: var(--space-3);
  right: var(--space-3);
  bottom: var(--space-3);
  display: grid;
  width: min(44rem, calc(100vw - 1.5rem));
  grid-template-rows: auto 1fr;
  overflow: hidden;
}

.drawer__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
  border-bottom: 1px solid var(--color-border);
  background: var(--background-panel-highlight);
  padding: var(--space-4);
}

.drawer__title {
  display: grid;
  min-width: 0;
  gap: var(--space-2);
}

.drawer__title h2 {
  margin: 0;
  overflow-wrap: anywhere;
  font-size: 1rem;
  font-weight: 760;
}

.drawer__title p {
  margin: 0;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__body {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.drawer__section--summary {
  grid-template-columns: repeat(1, minmax(0, 1fr));
}

.drawer__section--summary div {
  display: grid;
  gap: var(--space-1);
}

.drawer__section--summary span,
.drawer__fields dt {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.drawer__section--summary strong {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: 0.9rem;
}

.drawer__section h3 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__notice,
.drawer__placeholder {
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.drawer__loading {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
}

.drawer__json {
  max-height: 20rem;
  overflow: auto;
  margin: 0;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text);
  padding: var(--space-3);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  line-height: 1.45;
  white-space: pre-wrap;
}

.linked-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.linked-header span {
  color: var(--color-text-muted);
  font-size: 0.78rem;
}

.linked-list {
  display: grid;
  gap: var(--space-2);
}

.linked-list code {
  overflow-wrap: anywhere;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0.45rem var(--space-2);
  font-family: var(--font-mono);
  font-size: 0.76rem;
}

@media (min-width: 680px) {
  .drawer__section--summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
