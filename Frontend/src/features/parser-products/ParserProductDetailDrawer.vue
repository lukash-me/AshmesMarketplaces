<script setup lang="ts">
import { ExternalLink, X } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import MarketProductImage from './MarketProductImage.vue';
import ParserProductObservedReviews from './ParserProductObservedReviews.vue';
import { getParserProduct } from './parserProducts.api';
import type {
  ParserProductDetail,
  ParserProductListItem,
  ParserProductReviewEvidence
} from './parserProducts.types';
import { getWildberriesProductUrl } from './wildberriesLinks';

const props = defineProps<{
  open: boolean;
  product: ParserProductListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<ParserProductDetail | null>(null);
const loading = ref(false);
const error = ref('');
let loadVersion = 0;

const emptyReviewEvidence: ParserProductReviewEvidence = {
  rootFetchCount: 0,
  parsedReviewCount: 0,
  parsedReplyCount: 0,
  latestReviewRunId: null,
  attributionMode: 'root_payload',
  isRootScoped: true,
  isFullHistoryUnknown: true,
  hasCappedRootPayload: false
};

const displayProduct = computed(() => detail.value ?? props.product);
const images = computed(() => detail.value?.imageUrls ?? []);
const mainImage = computed(() => images.value[0] ?? null);
const productUrl = computed(() => getWildberriesProductUrl(displayProduct.value?.wbProductId));
const rankSummary = computed(() => displayProduct.value?.rank ?? null);
const reviewEvidence = computed(() => displayProduct.value?.parsedReviewEvidence ?? emptyReviewEvidence);
const hasParsedEvidence = computed(
  () =>
    reviewEvidence.value.rootFetchCount > 0 ||
    reviewEvidence.value.parsedReviewCount > 0 ||
    reviewEvidence.value.parsedReplyCount > 0
);

watch(
  () => [props.open, props.product?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      error.value = '';
      return;
    }

    await loadDetail(id);
  },
  { immediate: true }
);

onMounted(() => window.addEventListener('keydown', onKeydown));
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown));

async function loadDetail(id: string) {
  const version = ++loadVersion;
  loading.value = true;
  error.value = '';
  detail.value = null;

  try {
    const response = await getParserProduct(id);
    if (version === loadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === loadVersion) {
      error.value = getProblemMessage(err, 'Не удалось загрузить товар маркетплейса.');
    }
  } finally {
    if (version === loadVersion) {
      loading.value = false;
    }
  }
}

function close() {
  emit('close');
}

function onKeydown(event: KeyboardEvent) {
  if (props.open && event.key === 'Escape') {
    close();
  }
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

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? '-' : new Intl.NumberFormat('ru-RU').format(value);
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '-';
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
    : '-';
}

function formatBoolean(value: boolean): string {
  return value ? 'Да' : 'Нет';
}

function evidenceScopeLabel(value: ParserProductReviewEvidence): string {
  return value.isRootScoped ? 'root-scoped' : 'product-scoped';
}

</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Закрыть карточку товара" @click="close" />
      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="parser-product-title">
        <header class="drawer__header">
          <div v-if="displayProduct" class="drawer__title">
            <Badge tone="info">Товар маркетплейса</Badge>
            <h2 id="parser-product-title">{{ displayProduct.name }}</h2>
            <p>WB {{ displayProduct.wbProductId }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Закрыть карточку товара" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayProduct" class="drawer__body">
          <section class="overview">
            <figure class="overview__media">
              <MarketProductImage
                :src="mainImage"
                :fallback-src="product?.thumbnailUrl"
                :alt="displayProduct.name"
                :priority="true"
              />
            </figure>
            <div class="overview__content">
              <dl class="overview__metrics">
                <div>
                  <dt>Цена</dt>
                  <dd class="overview__price numeric">{{ formatMoney(displayProduct.priceDiscounted) }}</dd>
                </div>
                <div>
                  <dt>Рейтинг WB</dt>
                  <dd class="numeric">{{ fieldValue(displayProduct.reviewRating) }}</dd>
                </div>
                <div>
                  <dt>Отзывы WB</dt>
                  <dd class="numeric">{{ fieldValue(displayProduct.feedbackCount) }}</dd>
                </div>
              </dl>
              <dl class="drawer__fields drawer__fields--two">
                <div><dt>WB id</dt><dd>{{ displayProduct.wbProductId }}</dd></div>
                <div><dt>Бренд</dt><dd>{{ fieldValue(displayProduct.brandName) }}</dd></div>
                <div><dt>Продавец</dt><dd>{{ fieldValue(displayProduct.sellerName) }}</dd></div>
                <div><dt>Категория</dt><dd>{{ fieldValue(displayProduct.sourceSubcategory) }}</dd></div>
              </dl>
              <a v-if="productUrl" class="wb-link" :href="productUrl" target="_blank" rel="noreferrer">
                <ExternalLink :size="16" />
                Открыть карточку на WB
              </a>
            </div>
          </section>

          <LoadingState v-if="loading" class="drawer__loading" :rows="3" />
          <section v-if="error" class="drawer__notice">{{ error }}</section>

          <section class="drawer__section">
            <h3>Позиция</h3>
            <template v-if="rankSummary">
              <dl class="drawer__fields drawer__fields--two">
                <div><dt>Позиция</dt><dd class="numeric">#{{ formatNumber(rankSummary.absolutePosition) }}</dd></div>
                <div><dt>Запрос</dt><dd>{{ rankSummary.query }}</dd></div>
                <div><dt>Категория</dt><dd>{{ fieldValue(rankSummary.sourceCategory) }}</dd></div>
                <div><dt>Подкатегория</dt><dd>{{ fieldValue(rankSummary.sourceSubcategory) }}</dd></div>
                <div><dt>Страница</dt><dd class="numeric">{{ formatNumber(rankSummary.page) }}</dd></div>
                <div><dt>Позиция на странице</dt><dd class="numeric">{{ formatNumber(rankSummary.positionOnPage) }}</dd></div>
                <div><dt>Наблюдение</dt><dd>{{ formatDateTime(rankSummary.observedAtUtc) }}</dd></div>
                <div><dt>Контекстов</dt><dd class="numeric">{{ formatNumber(rankSummary.contextsCount) }}</dd></div>
                <div><dt>Rank context id</dt><dd>{{ rankSummary.rankContextId }}</dd></div>
                <div><dt>Rank parser run</dt><dd>{{ rankSummary.parserRunId }}</dd></div>
              </dl>
              <p class="drawer__hint">Позиция — лучшая наблюдаемая позиция в последнем staged rank run. Это не универсальный рейтинг маркетплейса.</p>
            </template>
            <p v-else class="drawer__empty">Позиция не найдена в последнем staged rank run.</p>
          </section>

          <section class="drawer__section">
            <h3>Спаршенные отзывы</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Рейтинг WB</dt><dd class="numeric">{{ fieldValue(displayProduct.reviewRating) }}</dd></div>
              <div><dt>Отзывы WB</dt><dd class="numeric">{{ fieldValue(displayProduct.feedbackCount) }}</dd></div>
              <div><dt>Root fetch count</dt><dd class="numeric">{{ formatNumber(reviewEvidence.rootFetchCount) }}</dd></div>
              <div><dt>Parsed reviews</dt><dd class="numeric">{{ formatNumber(reviewEvidence.parsedReviewCount) }}</dd></div>
              <div><dt>Parsed replies</dt><dd class="numeric">{{ formatNumber(reviewEvidence.parsedReplyCount) }}</dd></div>
              <div><dt>Latest review run</dt><dd>{{ fieldValue(reviewEvidence.latestReviewRunId) }}</dd></div>
              <div><dt>Attribution</dt><dd>{{ reviewEvidence.attributionMode }} · {{ evidenceScopeLabel(reviewEvidence) }}</dd></div>
              <div><dt>Full history unknown</dt><dd>{{ formatBoolean(reviewEvidence.isFullHistoryUnknown) }}</dd></div>
              <div><dt>Capped payload</dt><dd>{{ formatBoolean(reviewEvidence.hasCappedRootPayload) }}</dd></div>
            </dl>
            <p v-if="hasParsedEvidence" class="drawer__hint">Спаршенные отзывы — staging evidence, root-scoped; это не гарантирует точную variant-level принадлежность.</p>
            <p v-else class="drawer__empty">Для этого root/product нет staged review rows. Число отзывов WB выше — это metadata карточки, а не результат парсинга отзывов.</p>
          </section>

          <ParserProductObservedReviews :product="displayProduct" />

          <section class="drawer__section">
            <h3>Цены и наличие</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Цена без скидки</dt><dd>{{ formatMoney(displayProduct.priceRegular) }}</dd></div>
              <div><dt>Цена с WB кошельком</dt><dd>{{ formatMoney(displayProduct.priceWbWallet) }}</dd></div>
              <div><dt>Скидка</dt><dd>{{ formatPercent(displayProduct.discountPercent) }}</dd></div>
              <div v-if="detail"><dt>Остаток</dt><dd>{{ fieldValue(detail.totalQuantity) }}</dd></div>
            </dl>
          </section>

          <section v-if="images.length > 1" class="drawer__section">
            <h3>Изображения</h3>
            <div class="media">
              <figure v-for="(image, index) in images" :key="image" class="media__item">
                <MarketProductImage :src="image" :alt="`${displayProduct.name} изображение ${index + 1}`" />
              </figure>
            </div>
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
  inset: var(--space-3) var(--space-3) var(--space-3) auto;
  display: grid;
  width: min(54rem, calc(100vw - 1.5rem));
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

.drawer__title,
.drawer__body,
.drawer__section,
.overview__content {
  display: grid;
  gap: var(--space-3);
}

.drawer__title {
  min-width: 0;
  gap: var(--space-2);
}

.drawer__title h2,
.drawer__title p {
  margin: 0;
}

.drawer__title h2 {
  overflow-wrap: anywhere;
  font-size: 1rem;
}

.drawer__title p,
.drawer__fields dd {
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__title p {
  color: var(--color-text-muted);
}

.drawer__body {
  align-content: start;
  overflow-y: auto;
  padding: var(--space-3);
}

.overview,
.drawer__section {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.overview {
  display: grid;
  gap: var(--space-3);
}

.overview__media {
  display: grid;
  min-height: 13rem;
  margin: 0;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
}

.overview__metrics {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.overview__metrics div,
.drawer__fields div {
  display: grid;
  gap: 0.2rem;
}

.overview__metrics div {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-2);
}

.overview__metrics dt,
.drawer__fields dt {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  text-transform: uppercase;
}

.overview__metrics dd,
.drawer__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.overview__metrics dd {
  font-size: 1rem;
  font-weight: 720;
}

.overview__price {
  font-size: 1.2rem;
}

.drawer__section h3 {
  margin: 0;
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__hint,
.drawer__empty {
  margin: 0;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  line-height: 1.45;
}

.drawer__empty {
  border-style: dashed;
  background: var(--surface-control);
}

.drawer__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__loading,
.drawer__notice {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3);
}

.drawer__notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.media {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(6rem, 1fr));
  gap: var(--space-2);
}

.media__item {
  margin: 0;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
}

.media__item :deep(.market-image),
.overview__media :deep(.market-image) {
  aspect-ratio: 3 / 4;
  width: 100%;
}

.wb-link {
  display: inline-flex;
  width: fit-content;
  min-height: 2.125rem;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  color: var(--color-text);
  padding: 0 var(--space-3);
  font-size: 0.8125rem;
  font-weight: 680;
  text-decoration: none;
}

.wb-link:hover {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
}

@media (min-width: 680px) {
  .overview {
    grid-template-columns: 13rem minmax(0, 1fr);
  }

  .overview__metrics,
  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .overview__metrics {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
</style>
