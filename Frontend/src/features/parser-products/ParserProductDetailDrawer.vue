<script setup lang="ts">
import { X } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import { getParserProduct } from './parserProducts.api';
import type { ParserProductDetail, ParserProductListItem } from './parserProducts.types';

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

const displayProduct = computed(() => detail.value ?? props.product);
const images = computed(() => detail.value?.imageUrls ?? []);

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
      error.value = getProblemMessage(err, 'Unable to load staged parser product.');
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

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat('en', {
        month: 'short',
        day: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : '-';
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Close parser product detail" @click="close" />
      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="parser-product-title">
        <header class="drawer__header">
          <div v-if="displayProduct" class="drawer__title">
            <Badge tone="info">Staged parser row</Badge>
            <h2 id="parser-product-title">{{ displayProduct.name }}</h2>
            <p>{{ displayProduct.wbProductId }} / {{ displayProduct.parserRunId }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close parser product detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayProduct" class="drawer__body">
          <section class="drawer__section drawer__summary">
            <div><span>Discounted</span><strong class="numeric">{{ fieldValue(displayProduct.priceDiscounted) }}</strong></div>
            <div><span>Review rating</span><strong class="numeric">{{ fieldValue(displayProduct.reviewRating) }}</strong></div>
            <div><span>Parsed</span><strong class="numeric">{{ formatDate(displayProduct.parsedAtUtc) }}</strong></div>
          </section>

          <LoadingState v-if="loading" class="drawer__loading" :rows="3" />
          <section v-if="error" class="drawer__notice">{{ error }}</section>

          <section class="drawer__section">
            <h3>Observed identifiers</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Row id</dt><dd>{{ displayProduct.id }}</dd></div>
              <div><dt>WB product id</dt><dd>{{ displayProduct.wbProductId }}</dd></div>
              <div><dt>WB root id</dt><dd>{{ fieldValue(displayProduct.wbRootId) }}</dd></div>
              <div><dt>Parser run</dt><dd>{{ displayProduct.parserRunId }}</dd></div>
              <div><dt>Brand</dt><dd>{{ fieldValue(displayProduct.brandName) }}</dd></div>
              <div><dt>Seller</dt><dd>{{ fieldValue(displayProduct.sellerName) }}</dd></div>
            </dl>
          </section>

          <section v-if="detail" class="drawer__section">
            <h3>Marketplace fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Marketplace</dt><dd>{{ detail.marketplace }}</dd></div>
              <div><dt>SKU</dt><dd>{{ fieldValue(detail.skuProduct) }}</dd></div>
              <div><dt>Entity</dt><dd>{{ fieldValue(detail.entity) }}</dd></div>
              <div><dt>Quantity</dt><dd>{{ fieldValue(detail.totalQuantity) }}</dd></div>
              <div><dt>Brand id on MP</dt><dd>{{ fieldValue(detail.brandIdOnMp) }}</dd></div>
              <div><dt>Seller id on MP</dt><dd>{{ fieldValue(detail.sellerIdOnMp) }}</dd></div>
              <div><dt>Subject parent id</dt><dd>{{ fieldValue(detail.subjectParentId) }}</dd></div>
              <div><dt>Subject id</dt><dd>{{ fieldValue(detail.subjectId) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Prices and feedback</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Regular price</dt><dd>{{ fieldValue(displayProduct.priceRegular) }}</dd></div>
              <div><dt>Discounted price</dt><dd>{{ fieldValue(displayProduct.priceDiscounted) }}</dd></div>
              <div><dt>WB wallet price</dt><dd>{{ fieldValue(displayProduct.priceWbWallet) }}</dd></div>
              <div><dt>Discount percent</dt><dd>{{ fieldValue(displayProduct.discountPercent) }}</dd></div>
              <div><dt>Rating rounded</dt><dd>{{ fieldValue(displayProduct.ratingRounded) }}</dd></div>
              <div><dt>Feedback count</dt><dd>{{ fieldValue(displayProduct.feedbackCount) }}</dd></div>
              <div v-if="detail"><dt>Feedback source</dt><dd>{{ fieldValue(detail.feedbackCountSource) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Observed media</h3>
            <div v-if="images.length" class="media">
              <figure v-for="(image, index) in images" :key="image" class="media__item">
                <img :src="image" :alt="`${displayProduct.name} parser image ${index + 1}`" />
              </figure>
            </div>
            <div v-else class="drawer__placeholder">No image URLs observed in this staged row.</div>
          </section>

          <section v-if="detail" class="drawer__section">
            <h3>Source lineage</h3>
            <dl class="drawer__fields">
              <div><dt>Source category</dt><dd>{{ fieldValue(detail.sourceCategory) }}</dd></div>
              <div><dt>Source subcategory</dt><dd>{{ fieldValue(detail.sourceSubcategory) }}</dd></div>
              <div><dt>Source query</dt><dd>{{ fieldValue(detail.sourceQuery) }}</dd></div>
              <div><dt>Source region</dt><dd>{{ fieldValue(detail.sourceRegionDest) }}</dd></div>
              <div><dt>File kind</dt><dd>{{ detail.sourceFileKind }}</dd></div>
              <div><dt>File sha256</dt><dd>{{ detail.sourceFileSha256 }}</dd></div>
              <div><dt>Source line</dt><dd>{{ detail.sourceLineNumber }}</dd></div>
              <div><dt>Row hash</dt><dd>{{ detail.rowHash }}</dd></div>
            </dl>
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
  width: min(43rem, calc(100vw - 1.5rem));
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
.drawer__section {
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

.drawer__title p {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__body {
  align-content: start;
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.drawer__summary div,
.drawer__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__summary span,
.drawer__fields dt {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  text-transform: uppercase;
}

.drawer__section h3 {
  margin: 0;
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__loading,
.drawer__notice,
.drawer__placeholder {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3);
}

.drawer__notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.drawer__placeholder {
  border-style: dashed;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
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

.media__item img {
  display: block;
  aspect-ratio: 1;
  width: 100%;
  object-fit: cover;
}

@media (min-width: 680px) {
  .drawer__summary,
  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .drawer__summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
</style>
