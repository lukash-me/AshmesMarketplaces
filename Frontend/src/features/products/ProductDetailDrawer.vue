<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import { getProduct } from './products.api';
import ProductSignalBadge from './ProductSignalBadge.vue';
import {
  getProductStatusLabel,
  getProductStatusTone
} from './productSignals';
import type { ProductDetail, ProductListItem } from './products.types';

const props = defineProps<{
  open: boolean;
  product: ProductListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<ProductDetail | null>(null);
const loading = ref(false);
const error = ref('');
let loadVersion = 0;

const displayProduct = computed(() => detail.value ?? props.product);
const images = computed(() => detail.value?.images ?? []);
const videos = computed(() => detail.value?.videos ?? []);
const formattedCharacteristics = computed(() => {
  if (!detail.value?.characteristics) {
    return '';
  }

  try {
    return JSON.stringify(detail.value.characteristics, null, 2);
  } catch {
    return String(detail.value.characteristics);
  }
});

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

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});

async function loadDetail(id: string): Promise<void> {
  const version = ++loadVersion;

  loading.value = true;
  error.value = '';
  detail.value = null;

  try {
    const response = await getProduct(id);

    if (version === loadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === loadVersion) {
      error.value = getProblemMessage(err, 'Unable to load product details.');
    }
  } finally {
    if (version === loadVersion) {
      loading.value = false;
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

function formatDate(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Close product detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="product-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayProduct" class="drawer__title">
            <ProductSignalBadge :product="displayProduct" />
            <h2 id="product-detail-title">{{ displayProduct.name }}</h2>
            <p>{{ displayProduct.skuProduct || displayProduct.idOnMp || displayProduct.skuSeller }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close product detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayProduct" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Status</span>
              <strong>
                <span class="drawer__status" :class="`drawer__status--${getProductStatusTone(displayProduct.status)}`" />
                {{ getProductStatusLabel(displayProduct.status) }}
              </strong>
            </div>
            <div>
              <span>Commission</span>
              <strong class="numeric">{{ displayProduct.commission ?? '-' }}</strong>
            </div>
            <div>
              <span>Updated</span>
              <strong class="numeric">{{ formatDate(displayProduct.dateUpdated) }}</strong>
            </div>
          </section>

          <LoadingState v-if="loading" class="drawer__loading" :rows="3" />

          <section v-if="error" class="drawer__notice">
            {{ error }}
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Product ID</dt><dd>{{ displayProduct.id }}</dd></div>
              <div><dt>Marketplace ID</dt><dd>{{ displayProduct.idMp }}</dd></div>
              <div><dt>Brand ID</dt><dd>{{ fieldValue(displayProduct.idBrand) }}</dd></div>
              <div><dt>Category ID</dt><dd>{{ fieldValue(displayProduct.idCategory) }}</dd></div>
              <div><dt>External ID</dt><dd>{{ fieldValue(displayProduct.idOnMp) }}</dd></div>
              <div><dt>Seller SKU</dt><dd>{{ displayProduct.skuSeller }}</dd></div>
              <div><dt>Product SKU</dt><dd>{{ fieldValue(displayProduct.skuProduct) }}</dd></div>
              <div><dt>Barcode</dt><dd>{{ fieldValue(displayProduct.barcode) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Timeline</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Created</dt><dd>{{ formatDate(displayProduct.dateCreated) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDate(displayProduct.dateUpdated) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Media</h3>
            <div v-if="images.length || videos.length" class="drawer__media">
              <figure v-for="image in images" :key="image.id" class="drawer__media-item">
                <img :src="image.url" :alt="`Product image ${image.sortOrder}`" />
                <figcaption>{{ image.isMain ? 'Main image' : `Image ${image.sortOrder}` }}</figcaption>
              </figure>
              <div v-for="video in videos" :key="video.id" class="drawer__media-item drawer__media-item--video">
                <span>Video</span>
                <small>{{ video.sortOrder }}</small>
              </div>
            </div>
            <div v-else class="drawer__placeholder">No media records returned by the current API response.</div>
          </section>

          <section class="drawer__section">
            <h3>Description</h3>
            <p class="drawer__text">{{ detail?.description || 'No description returned by the current API response.' }}</p>
          </section>

          <section class="drawer__section">
            <h3>Characteristics JSON</h3>
            <pre v-if="formattedCharacteristics" class="drawer__json">{{ formattedCharacteristics }}</pre>
            <div v-else class="drawer__placeholder">No characteristics JSON returned by the current API response.</div>
          </section>

          <section class="drawer__actions">
            <Button variant="secondary" disabled>Open editor</Button>
            <Button variant="ghost" disabled>Media workflow</Button>
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
  width: min(42rem, calc(100vw - 1.5rem));
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

.drawer__status {
  height: 0.5rem;
  width: 0.5rem;
  border-radius: 999px;
  background: var(--color-text-subtle);
}

.drawer__status--success {
  background: var(--state-success);
}

.drawer__status--warning {
  background: var(--state-warning);
}

.drawer__status--danger {
  background: var(--state-danger);
}

.drawer__status--info {
  background: var(--state-info);
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

.drawer__text {
  margin: 0;
  color: var(--color-text-muted);
  line-height: 1.55;
}

.drawer__json {
  max-height: 18rem;
  overflow: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  margin: 0;
  padding: var(--space-3);
}

.drawer__media {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(8rem, 1fr));
  gap: var(--space-2);
}

.drawer__media-item {
  display: grid;
  min-height: 7rem;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
}

.drawer__media-item img {
  height: 6rem;
  width: 100%;
  object-fit: cover;
}

.drawer__media-item figcaption,
.drawer__media-item small,
.drawer__media-item span {
  padding: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.drawer__media-item--video {
  place-items: center;
}

.drawer__placeholder,
.drawer__notice {
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

.drawer__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  padding-bottom: var(--space-2);
}

@media (min-width: 680px) {
  .drawer__section--summary,
  .drawer__fields--two {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
