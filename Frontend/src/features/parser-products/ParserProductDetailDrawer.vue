<script setup lang="ts">
import { ChevronLeft, ChevronRight, CircleHelp, ExternalLink, X } from 'lucide-vue-next';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import MarketProductImage from './MarketProductImage.vue';
import MarketProductObservedReviews from './ParserProductObservedReviews.vue';
import { getParserProduct } from './parserProducts.api';
import type {
  ParserProductDetail,
  ParserProductListItem,
  ParserProductPosition
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
const activeImagePreview = ref<string | null>(null);
const activeHelpTooltip = ref<{ text: string; style: Record<string, string> } | null>(null);
const helpTooltipBubble = ref<HTMLElement | null>(null);
let loadVersion = 0;
let lastTooltipTarget: HTMLElement | null = null;

const currentPriceHelp = 'Текущая цена карточки.';
const regularPriceHelp = 'Цена до применённых скидок, если она доступна.';
const walletPriceHelp = 'Цена с учётом скидки WB кошелька, если она доступна.';

const displayProduct = computed(() => detail.value ?? props.product);
const images = computed(() => detail.value?.imageUrls ?? []);
const mainImage = computed(() => images.value[0] ?? null);
const previewImages = computed(() => {
  const urls = [mainImage.value, props.product?.thumbnailUrl, ...images.value]
    .filter((value): value is string => Boolean(value));
  return [...new Set(urls)];
});
const activeImageIndex = computed(() =>
  activeImagePreview.value ? previewImages.value.indexOf(activeImagePreview.value) : -1
);
const productUrl = computed(() => getWildberriesProductUrl(displayProduct.value?.wbProductId));
const positionSummary = computed(() => positionFor(displayProduct.value));

watch(
  () => [props.open, props.product?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      error.value = '';
      activeImagePreview.value = null;
      return;
    }

    await loadDetail(id);
  },
  { immediate: true }
);

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', closeHelpTooltip);
  window.addEventListener('scroll', closeHelpTooltip, true);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
  window.removeEventListener('resize', closeHelpTooltip);
  window.removeEventListener('scroll', closeHelpTooltip, true);
});

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
    if (activeImagePreview.value) {
      event.preventDefault();
      event.stopPropagation();
      closeImagePreview();
      return;
    }

    if (activeHelpTooltip.value) {
      event.preventDefault();
      event.stopPropagation();
      closeHelpTooltip();
      return;
    }

    close();
  }
}

function showHelpTooltip(event: MouseEvent | FocusEvent, text: string, align: 'left' | 'right' = 'left') {
  const target = event.currentTarget as HTMLElement | null;
  if (!target) {
    return;
  }

  lastTooltipTarget = target;
  placeHelpTooltip(target, text, align);
}

function closeHelpTooltip() {
  activeHelpTooltip.value = null;
  lastTooltipTarget = null;
}

function placeHelpTooltip(target: HTMLElement, text: string, align: 'left' | 'right') {
  const targetRect = target.getBoundingClientRect();
  const drawerRect = target.closest('.drawer')?.getBoundingClientRect();
  const viewportMargin = 12;
  const drawerMargin = 12;
  const maxWidth = Math.max(
    160,
    Math.min(260, window.innerWidth - viewportMargin * 2, (drawerRect?.width ?? window.innerWidth) - drawerMargin * 2)
  );
  const estimatedWidth = Math.min(maxWidth, text.length > 54 ? 240 : 180);
  const minLeft = Math.max(viewportMargin, drawerRect ? drawerRect.left + drawerMargin : viewportMargin);
  const maxLeft = Math.min(
    window.innerWidth - viewportMargin - estimatedWidth,
    drawerRect ? drawerRect.right - drawerMargin - estimatedWidth : window.innerWidth - viewportMargin - estimatedWidth
  );
  const preferredLeft = align === 'right' ? targetRect.right - estimatedWidth : targetRect.left;
  const left = Math.min(Math.max(preferredLeft, minLeft), Math.max(minLeft, maxLeft));
  const top = Math.min(targetRect.bottom + 8, window.innerHeight - viewportMargin - 48);

  activeHelpTooltip.value = {
    text,
    style: {
      left: `${left}px`,
      top: `${Math.max(viewportMargin, top)}px`,
      maxWidth: `${maxWidth}px`
    }
  };

  void nextTick(() => clampHelpTooltip(target, align));
}

function clampHelpTooltip(target: HTMLElement, align: 'left' | 'right') {
  if (!activeHelpTooltip.value || lastTooltipTarget !== target || !helpTooltipBubble.value) {
    return;
  }

  const targetRect = target.getBoundingClientRect();
  const drawerRect = target.closest('.drawer')?.getBoundingClientRect();
  const bubbleRect = helpTooltipBubble.value.getBoundingClientRect();
  const viewportMargin = 12;
  const drawerMargin = 12;
  const minLeft = Math.max(viewportMargin, drawerRect ? drawerRect.left + drawerMargin : viewportMargin);
  const maxLeft = Math.min(
    window.innerWidth - viewportMargin - bubbleRect.width,
    drawerRect ? drawerRect.right - drawerMargin - bubbleRect.width : window.innerWidth - viewportMargin - bubbleRect.width
  );
  const preferredLeft = align === 'right' ? targetRect.right - bubbleRect.width : targetRect.left;
  const left = Math.min(Math.max(preferredLeft, minLeft), Math.max(minLeft, maxLeft));
  const belowTop = targetRect.bottom + 8;
  const aboveTop = targetRect.top - bubbleRect.height - 8;
  const top = belowTop + bubbleRect.height <= window.innerHeight - viewportMargin
    ? belowTop
    : Math.max(viewportMargin, aboveTop);

  activeHelpTooltip.value = {
    ...activeHelpTooltip.value,
    style: {
      ...activeHelpTooltip.value.style,
      left: `${left}px`,
      top: `${top}px`
    }
  };
}

function openImagePreview(src: string | null | undefined) {
  if (!src) {
    return;
  }

  activeImagePreview.value = src;
}

function closeImagePreview() {
  activeImagePreview.value = null;
}

function showPreviousImage() {
  const urls = previewImages.value;
  if (urls.length < 2) {
    return;
  }

  const index = activeImageIndex.value <= 0 ? urls.length - 1 : activeImageIndex.value - 1;
  activeImagePreview.value = urls[index];
}

function showNextImage() {
  const urls = previewImages.value;
  if (urls.length < 2) {
    return;
  }

  const index = activeImageIndex.value < 0 || activeImageIndex.value >= urls.length - 1
    ? 0
    : activeImageIndex.value + 1;
  activeImagePreview.value = urls[index];
}

function onLightboxClick(event: MouseEvent) {
  const target = event.target as HTMLElement | null;
  if (!target?.closest('.image-lightbox__content')) {
    closeImagePreview();
  }
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

function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : new Intl.NumberFormat('ru-RU').format(value);
}

function stockLabel(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return 'Нет данных';
  }

  return new Intl.NumberFormat('ru-RU').format(value);
}

function identityValue(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
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

function positionFor(product: ParserProductListItem | null): ParserProductPosition {
  return product?.position ?? {
    state: 'unknown',
    absolutePosition: null,
    observedRangeLimit: null,
    query: null,
    sourceCategory: product?.sourceCategory ?? null,
    sourceSubcategory: product?.sourceSubcategory ?? null,
    observedAtUtc: null
  };
}

function positionLabel(position: ParserProductPosition): string {
  if (position.state === 'observed' && position.absolutePosition !== null) {
    return `#${formatNumber(position.absolutePosition)}`;
  }

  if (position.state === 'beyondObservedRange' && position.observedRangeLimit !== null) {
    return `>${formatNumber(position.observedRangeLimit)}`;
  }

  return 'Нет данных';
}

</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Закрыть карточку товара" @click="close" />
      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="market-product-title">
        <header class="drawer__header">
          <div v-if="displayProduct" class="drawer__title">
            <Badge tone="info">Товар маркетплейса</Badge>
            <h2 id="market-product-title">{{ displayProduct.name }}</h2>
            <p>WB {{ displayProduct.wbProductId }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Закрыть карточку товара" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayProduct" class="drawer__body">
          <section class="overview">
            <figure class="overview__media">
              <button
                v-if="mainImage || product?.thumbnailUrl"
                class="image-trigger image-trigger--main"
                type="button"
                :aria-label="`Открыть изображение товара ${displayProduct.name}`"
                @click="openImagePreview(mainImage || product?.thumbnailUrl)"
              >
                <MarketProductImage
                  :src="mainImage"
                  :fallback-src="product?.thumbnailUrl"
                  :alt="displayProduct.name"
                  :priority="true"
                />
              </button>
              <MarketProductImage
                v-else
                :src="mainImage"
                :fallback-src="product?.thumbnailUrl"
                :alt="displayProduct.name"
                :priority="true"
              />
            </figure>
            <div class="overview__content">
              <dl class="overview__metrics">
                <div>
                  <dt>
                    <span>Цена</span>
                    <span class="help-tooltip">
                      <button
                        class="help-tooltip__trigger"
                        type="button"
                        aria-label="Цена: текущая цена карточки."
                        @mouseenter="showHelpTooltip($event, currentPriceHelp)"
                        @mouseleave="closeHelpTooltip"
                        @focus="showHelpTooltip($event, currentPriceHelp)"
                        @blur="closeHelpTooltip"
                        @click="showHelpTooltip($event, currentPriceHelp)"
                      >
                        <CircleHelp :size="13" />
                      </button>
                    </span>
                  </dt>
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
              <div class="overview__details">
                <dl class="drawer__fields drawer__fields--single">
                  <div><dt>WB id</dt><dd>{{ displayProduct.wbProductId }}</dd></div>
                </dl>
                <dl class="drawer__fields drawer__fields--stack">
                  <div><dt>Бренд</dt><dd>{{ identityValue(displayProduct.brandName) }}</dd></div>
                  <div><dt>Продавец</dt><dd>{{ identityValue(displayProduct.sellerName) }}</dd></div>
                </dl>
                <dl class="drawer__fields drawer__fields--stack">
                  <div><dt>Категория</dt><dd>{{ fieldValue(displayProduct.sourceCategory) }}</dd></div>
                  <div><dt>Подкатегория</dt><dd>{{ fieldValue(displayProduct.sourceSubcategory) }}</dd></div>
                </dl>
              </div>
              <a v-if="productUrl" class="wb-link" :href="productUrl" target="_blank" rel="noreferrer">
                <ExternalLink :size="16" />
                Открыть карточку на WB
              </a>
            </div>
          </section>

          <LoadingState v-if="loading" class="drawer__loading" :rows="3" />
          <section v-if="error" class="drawer__notice">{{ error }}</section>

          <section class="drawer__section">
            <div class="drawer__section-title">
              <h3>Поисковая выдача</h3>
            </div>
            <dl class="drawer__fields drawer__fields--two">
              <div>
                <dt>
                  <span>Позиция</span>
                  <span class="help-tooltip help-tooltip--field help-tooltip--left">
                    <button
                      class="help-tooltip__trigger"
                      type="button"
                      aria-label="Пояснение к позиции в поисковой выдаче"
                    >
                      <CircleHelp :size="13" />
                    </button>
                    <span class="help-tooltip__bubble" role="tooltip">
                      Позиция — место карточки в этой подкатегории. По другим запросам и в других категориях позиция может отличаться.
                    </span>
                  </span>
                </dt>
                <dd class="position-value numeric">{{ positionLabel(positionSummary) }}</dd>
              </div>
              <div><dt>Категория</dt><dd>{{ fieldValue(positionSummary.sourceCategory) }}</dd></div>
              <div><dt>Запрос</dt><dd>{{ fieldValue(positionSummary.query) }}</dd></div>
              <div><dt>Подкатегория</dt><dd>{{ fieldValue(positionSummary.sourceSubcategory) }}</dd></div>
              <div><dt>Обновлено</dt><dd>{{ formatDateTime(positionSummary.observedAtUtc) }}</dd></div>
            </dl>
            <p v-if="positionSummary.state === 'unknown'" class="drawer__empty">Позиция пока не определена для этой карточки.</p>
          </section>

          <MarketProductObservedReviews :product="displayProduct" />

          <section class="drawer__section">
            <h3>Цены и наличие</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div>
                <dt>
                  <span>Цена</span>
                  <span class="help-tooltip">
                    <button
                      class="help-tooltip__trigger"
                      type="button"
                      aria-label="Цена: текущая цена карточки."
                      @mouseenter="showHelpTooltip($event, currentPriceHelp)"
                      @mouseleave="closeHelpTooltip"
                      @focus="showHelpTooltip($event, currentPriceHelp)"
                      @blur="closeHelpTooltip"
                      @click="showHelpTooltip($event, currentPriceHelp)"
                    >
                      <CircleHelp :size="13" />
                    </button>
                  </span>
                </dt>
                <dd>{{ formatMoney(displayProduct.priceDiscounted) }}</dd>
              </div>
              <div>
                <dt>
                  <span>Цена без скидки</span>
                  <span class="help-tooltip">
                    <button
                      class="help-tooltip__trigger"
                      type="button"
                      aria-label="Цена без скидки: цена до применённых скидок, если она доступна."
                      @mouseenter="showHelpTooltip($event, regularPriceHelp)"
                      @mouseleave="closeHelpTooltip"
                      @focus="showHelpTooltip($event, regularPriceHelp)"
                      @blur="closeHelpTooltip"
                      @click="showHelpTooltip($event, regularPriceHelp)"
                    >
                      <CircleHelp :size="13" />
                    </button>
                  </span>
                </dt>
                <dd>{{ formatMoney(displayProduct.priceRegular) }}</dd>
              </div>
              <div>
                <dt>
                  <span>Цена с WB кошельком</span>
                  <span class="help-tooltip">
                    <button
                      class="help-tooltip__trigger"
                      type="button"
                      aria-label="Цена с WB кошельком: цена с учётом скидки WB кошелька, если она доступна."
                      @mouseenter="showHelpTooltip($event, walletPriceHelp, 'right')"
                      @mouseleave="closeHelpTooltip"
                      @focus="showHelpTooltip($event, walletPriceHelp, 'right')"
                      @blur="closeHelpTooltip"
                      @click="showHelpTooltip($event, walletPriceHelp, 'right')"
                    >
                      <CircleHelp :size="13" />
                    </button>
                  </span>
                </dt>
                <dd>{{ formatMoney(displayProduct.priceWbWallet) }}</dd>
              </div>
              <div v-if="displayProduct.discountPercent !== null"><dt>Скидка</dt><dd>{{ formatPercent(displayProduct.discountPercent) }}</dd></div>
              <div><dt>Остаток</dt><dd>{{ stockLabel(displayProduct.totalQuantity) }}</dd></div>
            </dl>
          </section>

          <section v-if="images.length > 1" class="drawer__section">
            <h3>Изображения</h3>
            <div class="media">
              <figure v-for="(image, index) in images" :key="image" class="media__item">
                <button
                  class="image-trigger"
                  type="button"
                  :aria-label="`Открыть изображение ${index + 1} товара ${displayProduct.name}`"
                  @click="openImagePreview(image)"
                >
                  <MarketProductImage :src="image" :alt="`${displayProduct.name} изображение ${index + 1}`" />
                </button>
              </figure>
            </div>
          </section>
        </div>
      </aside>

      <div
        v-if="activeHelpTooltip"
        ref="helpTooltipBubble"
        class="help-tooltip-floating"
        :style="activeHelpTooltip.style"
        role="tooltip"
      >
        {{ activeHelpTooltip.text }}
      </div>

      <div
        v-if="activeImagePreview"
        class="image-lightbox"
        role="dialog"
        aria-modal="true"
        aria-label="Просмотр изображения товара"
        @click="onLightboxClick"
      >
        <span class="image-lightbox__backdrop" aria-hidden="true" />
        <div class="image-lightbox__content">
          <button class="image-lightbox__close" type="button" aria-label="Закрыть просмотр изображения" @click="closeImagePreview">
            <X :size="20" />
          </button>
          <button
            v-if="previewImages.length > 1"
            class="image-lightbox__nav image-lightbox__nav--prev"
            type="button"
            aria-label="Предыдущее изображение"
            @click="showPreviousImage"
          >
            <ChevronLeft :size="22" />
          </button>
          <img class="image-lightbox__image" :src="activeImagePreview" :alt="displayProduct?.name ?? 'Изображение товара'" />
          <button
            v-if="previewImages.length > 1"
            class="image-lightbox__nav image-lightbox__nav--next"
            type="button"
            aria-label="Следующее изображение"
            @click="showNextImage"
          >
            <ChevronRight :size="22" />
          </button>
        </div>
      </div>
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
  position: relative;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(135deg, rgb(249 115 22 / 0.035), transparent 18rem),
    var(--surface-panel-muted);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.025);
  padding: var(--space-3);
}

.overview::before,
.drawer__section::before {
  position: absolute;
  inset: 0 auto 0 0;
  width: 2px;
  border-radius: var(--radius-md) 0 0 var(--radius-md);
  background: linear-gradient(180deg, var(--accent-ember-border), transparent 72%);
  content: '';
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

.image-trigger {
  display: grid;
  height: 100%;
  width: 100%;
  place-items: center;
  border: 0;
  background: transparent;
  color: inherit;
  cursor: zoom-in;
  padding: 0;
}

.image-trigger:focus-visible {
  outline: none;
  box-shadow: inset 0 0 0 2px var(--accent-primary-hover-border), var(--focus-ring);
}

.image-trigger:hover :deep(.market-image__asset) {
  transform: scale(1.025);
}

.image-trigger :deep(.market-image__asset) {
  transition: opacity 180ms ease, transform 180ms ease;
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
  position: relative;
  overflow: visible;
  border: 1px solid rgb(249 115 22 / 0.22);
  border-radius: var(--radius-sm);
  background:
    radial-gradient(circle at 100% 0, rgb(249 115 22 / 0.12), transparent 38%),
    var(--surface-control);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.04),
    inset 0 0 18px rgb(249 115 22 / 0.035),
    0 10px 28px rgb(0 0 0 / 0.18);
  padding: var(--space-2);
}

.overview__metrics div::after {
  position: absolute;
  inset: 0 0 auto auto;
  width: 0.55rem;
  height: 0.55rem;
  border-top: 1px solid rgb(251 146 60 / 0.52);
  border-right: 1px solid rgb(251 146 60 / 0.52);
  content: '';
}

.overview__metrics dt,
.drawer__fields dt {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
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
  color: var(--color-text);
  font-size: 1.05rem;
  font-weight: 760;
}

.overview__price {
  font-size: 1.22rem;
}

.drawer__section h3 {
  margin: 0;
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__section-title {
  display: inline-flex;
  width: fit-content;
  align-items: center;
  gap: var(--space-2);
}

.position-value {
  color: var(--accent-ember-text-strong);
  font-size: 1.05rem;
  font-weight: 780;
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

.overview__details {
  display: grid;
  gap: var(--space-2);
}

.drawer__fields--stack,
.drawer__fields--single {
  position: relative;
  overflow: hidden;
  border: 1px solid rgb(249 115 22 / 0.26);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.018), transparent),
    rgb(7 10 16 / 0.42);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.025), 0 8px 22px rgb(249 115 22 / 0.035);
  padding: var(--space-2);
}

.drawer__fields--stack {
  gap: var(--space-3);
}

.help-tooltip {
  position: relative;
  display: inline-flex;
  align-items: center;
  isolation: isolate;
}

.help-tooltip__trigger {
  display: inline-grid;
  width: 1rem;
  height: 1rem;
  place-items: center;
  border: 0;
  background: transparent;
  color: var(--accent-ember-text);
  cursor: help;
  padding: 0;
}

.help-tooltip__trigger:focus-visible {
  outline: none;
  color: var(--accent-ember-text-strong);
  filter: drop-shadow(0 0 6px rgb(249 115 22 / 0.35));
}

.help-tooltip__bubble {
  position: absolute;
  z-index: 30;
  top: calc(100% + 0.45rem);
  left: 0;
  width: max-content;
  max-width: min(15rem, calc(100vw - 4rem));
  transform: translateY(0.15rem);
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    rgb(8 11 18 / 0.98);
  box-shadow: 0 16px 42px rgb(0 0 0 / 0.36), 0 0 0 1px rgb(255 255 255 / 0.025);
  color: var(--color-text);
  opacity: 0;
  padding: var(--space-2);
  pointer-events: none;
  text-transform: none;
  transition: opacity 120ms ease, transform 120ms ease;
  white-space: normal;
  overflow-wrap: break-word;
  line-height: 1.35;
}

.help-tooltip--right .help-tooltip__bubble {
  right: 0;
  left: auto;
}

.help-tooltip--left .help-tooltip__bubble {
  right: auto;
  left: 0;
}

.help-tooltip--field .help-tooltip__bubble {
  max-width: min(18rem, calc(100vw - 4rem));
}

.help-tooltip--drawer-edge .help-tooltip__bubble {
  right: 0;
  left: auto;
  max-width: min(13rem, calc(100vw - 4rem));
}

.help-tooltip:hover .help-tooltip__bubble,
.help-tooltip:focus-within .help-tooltip__bubble {
  opacity: 1;
  transform: translateY(0);
}

.help-tooltip-floating {
  position: fixed;
  z-index: 60;
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    rgb(8 11 18 / 0.98);
  box-shadow: 0 16px 42px rgb(0 0 0 / 0.36), 0 0 0 1px rgb(255 255 255 / 0.025);
  color: var(--color-text);
  padding: var(--space-2);
  pointer-events: none;
  text-transform: none;
  white-space: normal;
  overflow-wrap: anywhere;
  line-height: 1.35;
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

.media__item :deep(.market-image__asset),
.media__item :deep(.market-image__preview),
.overview__media :deep(.market-image__asset),
.overview__media :deep(.market-image__preview) {
  object-fit: contain;
}

.image-lightbox {
  position: fixed;
  inset: 0;
  z-index: 70;
  display: grid;
  width: 100vw;
  height: 100dvh;
  box-sizing: border-box;
  place-items: center;
  overflow: hidden;
  padding: clamp(0.75rem, 2vw, 1.5rem);
}

.image-lightbox__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background:
    radial-gradient(circle at 50% 12%, rgb(249 115 22 / 0.12), transparent 28rem),
    rgb(0 0 0 / 0.82);
  pointer-events: none;
}

.image-lightbox__content {
  position: relative;
  z-index: 1;
  display: grid;
  box-sizing: border-box;
  max-height: calc(100dvh - 2rem);
  max-width: calc(100vw - 2rem);
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-md);
  background: rgb(5 8 13 / 0.86);
  box-shadow: 0 28px 84px rgb(0 0 0 / 0.56), inset 0 1px 0 rgb(255 255 255 / 0.04);
  padding: var(--space-3);
}

.image-lightbox__image {
  display: block;
  max-height: calc(100dvh - 4rem);
  max-width: calc(100vw - 4rem);
  object-fit: contain;
}

.image-lightbox__close,
.image-lightbox__nav {
  position: absolute;
  z-index: 2;
  display: inline-grid;
  place-items: center;
  border: 1px solid var(--accent-ember-border);
  border-radius: var(--radius-sm);
  background: rgb(8 11 18 / 0.82);
  color: var(--accent-ember-text-strong);
  cursor: pointer;
}

.image-lightbox__close:hover,
.image-lightbox__nav:hover {
  border-color: var(--accent-primary-hover-border);
  background: rgb(249 115 22 / 0.12);
}

.image-lightbox__close:focus-visible,
.image-lightbox__nav:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.image-lightbox__close {
  top: var(--space-2);
  right: var(--space-2);
  width: 2.25rem;
  height: 2.25rem;
}

.image-lightbox__nav {
  top: 50%;
  width: 2.5rem;
  height: 2.5rem;
  transform: translateY(-50%);
}

.image-lightbox__nav--prev {
  left: var(--space-2);
}

.image-lightbox__nav--next {
  right: var(--space-2);
}

.wb-link {
  display: inline-flex;
  width: fit-content;
  min-height: 2.125rem;
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--accent-primary-border);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.18), rgb(249 115 22 / 0.06)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.04), 0 10px 26px rgb(249 115 22 / 0.08);
  padding: 0 var(--space-3);
  font-size: 0.8125rem;
  font-weight: 680;
  text-decoration: none;
}

.wb-link:hover {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.22), rgb(249 115 22 / 0.08)),
    var(--color-surface-hover);
}

.wb-link:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring), 0 10px 26px rgb(249 115 22 / 0.1);
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

  .overview__details {
    grid-template-columns: minmax(8rem, 0.7fr) repeat(2, minmax(0, 1fr));
  }
}
</style>
