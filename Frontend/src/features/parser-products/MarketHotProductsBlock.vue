<script setup lang="ts">
import { computed, ref } from 'vue';

import MarketProductImage from './MarketProductImage.vue';
import type {
  HotProductRecommendationFactor,
  HotProductRecommendationItem,
  HotProductsListResponse
} from './hotProductsRecommendations.types';

const props = defineProps<{
  response: HotProductsListResponse | null;
  loading: boolean;
  error: string;
}>();

const emit = defineEmits<{
  locate: [wbProductId: string];
}>();

const displayLimit = 5;
const expandedId = ref<string | null>(null);
const items = computed(() => pickVisibleItems(props.response?.items ?? []));
const hasItems = computed(() => items.value.length > 0);

function pickVisibleItems(allItems: HotProductRecommendationItem[]): HotProductRecommendationItem[] {
  if (allItems.length <= displayLimit) {
    return allItems;
  }

  const picked: HotProductRecommendationItem[] = [];
  const pickedIds = new Set<string>();
  const seenGroups = new Set<string>();

  for (const item of allItems) {
    const group = `${item.sourceCategory ?? ''}|${item.sourceSubcategory ?? ''}`.toLocaleLowerCase('ru-RU');
    if (group.trim() && !seenGroups.has(group)) {
      picked.push(item);
      pickedIds.add(item.id);
      seenGroups.add(group);
    }

    if (picked.length === displayLimit) {
      return picked;
    }
  }

  for (const item of allItems) {
    if (!pickedIds.has(item.id)) {
      picked.push(item);
    }

    if (picked.length === displayLimit) {
      return picked;
    }
  }

  return picked;
}

function toggleExplanation(item: HotProductRecommendationItem) {
  expandedId.value = expandedId.value === item.id ? null : item.id;
}

function locate(item: HotProductRecommendationItem) {
  if (item.wbProductId) {
    emit('locate', item.wbProductId);
  }
}

function identityValue(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

function textValue(value: string | null | undefined): string {
  return value?.trim() ? value : 'Нет данных';
}

function numberValue(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : formatNumber(value);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU').format(value);
}

function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return 'Нет данных';
  }

  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function displayPrice(item: HotProductRecommendationItem): number | null {
  return item.walletPrice ?? item.price ?? item.priceWithoutDiscount;
}

function formatScore(value: number): string {
  const normalized = Math.max(0, Math.min(100, Math.round(value)));
  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(normalized)}/100`;
}

function scoreTier(value: number): 'priority' | 'strong' | 'steady' {
  if (value >= 90) {
    return 'priority';
  }

  if (value >= 80) {
    return 'strong';
  }

  return 'steady';
}

function formatConfidence(value: number): string {
  const percent = Math.max(0, Math.min(100, Math.round(value * 100)));
  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(percent)}%`;
}

function formatStock(value: number | null): string {
  if (value === null) {
    return 'Нет данных';
  }

  if (value >= 40) {
    return '≥40';
  }

  return formatNumber(value);
}

function formatPosition(item: HotProductRecommendationItem): string {
  if (item.positionState === 'observed' && item.position !== null) {
    return `#${formatNumber(item.position)}`;
  }

  if (item.positionState === 'beyondObservedRange' && item.observedRangeLimit !== null) {
    return `>${formatNumber(item.observedRangeLimit)}`;
  }

  return 'Нет данных';
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

function factorValue(factor: HotProductRecommendationFactor): string {
  if (factor.value === null || factor.value === undefined || factor.value === '') {
    return 'Нет данных';
  }

  if (typeof factor.value === 'number') {
    return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(factor.value);
  }

  if (typeof factor.value === 'boolean') {
    return factor.value ? 'Да' : 'Нет';
  }

  if (typeof factor.value === 'string') {
    return factor.value;
  }

  return 'Нет данных';
}

function factorsByDirection(
  item: HotProductRecommendationItem,
  direction: 'positive' | 'negative' | 'neutral'
): HotProductRecommendationFactor[] {
  return item.factors.filter((factor) => factor.direction === direction);
}
</script>

<template>
  <section class="hot-products app-surface" aria-labelledby="hot-products-title">
    <header class="hot-products__header">
      <div class="hot-products__heading">
        <div class="hot-products__title-row">
          <h2 id="hot-products-title">Перспективные товары</h2>
          <span class="hot-products__badge">Рекомендуем обратить внимание</span>
        </div>
        <p>
          Товары, которые стоит изучить в первую очередь. Оценка учитывает рыночные признаки, но перед запуском всё равно
          проверьте маржинальность, поставщика и конкуренцию.
        </p>
      </div>
    </header>

    <div v-if="loading" class="hot-products__state">
      <span class="hot-products__pulse" />
      Загружаем перспективные товары...
    </div>

    <div v-else-if="error" class="hot-products__state hot-products__state--error">
      Не удалось загрузить рекомендации. Попробуйте обновить страницу.
    </div>

    <div v-else-if="!hasItems" class="hot-products__state hot-products__state--empty">
      <strong>Рекомендации ещё не рассчитаны.</strong>
      <span>Запустите пересчёт после обновления данных рынка.</span>
    </div>

    <div v-else class="hot-products__showcase" role="list">
      <article
        v-for="item in items"
        :key="item.id"
        class="hot-card"
        :class="[`hot-card--${scoreTier(item.score)}`, { 'hot-card--open': expandedId === item.id }]"
        role="listitem"
      >
        <svg class="hot-card__flame" viewBox="0 0 96 150" aria-hidden="true" focusable="false">
          <path
            class="hot-card__flame-outer"
            d="M47 145C25 131 10 111 11 86c1-21 13-33 18-48 4-12 1-23-4-34 19 11 31 29 30 48 11-12 17-29 13-48 21 19 29 43 23 66 8-7 12-17 11-29 13 17 17 39 10 60-8 25-31 40-65 44Z"
          />
          <path
            class="hot-card__flame-middle"
            d="M49 132c-18-11-28-26-27-45 1-16 11-25 20-36 7-9 9-20 6-32 17 13 23 30 17 49 10-7 16-18 17-33 13 15 17 32 11 49 7-4 12-11 15-21 5 19 0 39-13 52-10 10-24 16-46 17Z"
          />
          <path
            class="hot-card__flame-inner"
            d="M50 126c-13-9-20-21-18-35 2-12 11-20 20-30 8-9 11-17 10-27 13 13 15 27 8 42 7-3 12-9 16-18 5 17 1 34-10 47-7 9-15 16-26 21Z"
          />
          <path
            class="hot-card__flame-core"
            d="M52 116c-8-7-11-15-8-25 2-8 9-14 15-21 4-5 7-11 7-18 8 10 8 21 2 32 5-2 9-6 12-12 1 16-9 34-28 44Z"
          />
        </svg>
        <div class="hot-card__content">
          <div
            class="hot-card__main"
            role="button"
            tabindex="0"
            :aria-expanded="expandedId === item.id"
            @click="toggleExplanation(item)"
            @keydown.enter.prevent="toggleExplanation(item)"
            @keydown.space.prevent="toggleExplanation(item)"
          >
            <div class="hot-card__image-wrap">
              <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
            </div>

            <div class="hot-card__body">
              <div class="hot-card__topline">
                <span>{{ textValue(item.sourceCategory) }}</span>
                <span>{{ textValue(item.sourceSubcategory) }}</span>
              </div>

              <h3 class="hot-card__name">{{ item.productName }}</h3>

              <div class="hot-card__identity">
                <span>{{ identityValue(item.brandName) }}</span>
                <span>{{ identityValue(item.sellerName) }}</span>
              </div>

              <div class="hot-card__metrics" aria-label="Показатели рекомендации">
                <div class="hot-card__metric hot-card__metric--price">
                  <strong>{{ formatMoney(displayPrice(item)) }}</strong>
                  <span>Цена</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ formatPosition(item) }}</strong>
                  <span>Позиция</span>
                </div>
                <div class="hot-card__metric">
                  <strong :class="`hot-card__rating--${ratingTone(item.rating)}`">{{ numberValue(item.rating) }}</strong>
                  <span>Рейтинг WB</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ numberValue(item.feedbackCount) }}</strong>
                  <span>Отзывы WB</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ formatStock(item.totalQuantity) }}</strong>
                  <span>Остаток</span>
                </div>
              </div>
            </div>

            <aside class="hot-card__side" aria-label="Оценка рекомендации">
              <div class="hot-card__score">
                <strong>{{ formatScore(item.score) }}</strong>
                <span>Индекс перспективности</span>
              </div>

              <button
                class="hot-card__why-link"
                type="button"
                :aria-expanded="expandedId === item.id"
                @click.stop="toggleExplanation(item)"
              >
                Почему
              </button>
            </aside>
          </div>

          <div v-if="expandedId === item.id" class="hot-card__details">
            <section class="hot-card__details-section hot-card__details-section--wide">
              <h4>Почему товар перспективен</h4>
              <p>{{ item.reason }}</p>
              <div class="hot-card__details-actions">
                <div class="hot-card__confidence">
                  <span>Уверенность расчёта</span>
                  <strong>{{ formatConfidence(item.confidence) }}</strong>
                </div>
              </div>
            </section>

            <section v-if="factorsByDirection(item, 'positive').length" class="hot-card__details-section">
              <h4>Удачные параметры</h4>
              <div class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'positive')"
                  :key="`${item.id}-positive-${factor.code}`"
                  class="hot-card__factor hot-card__factor--positive"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
            </section>

            <section class="hot-card__details-section">
              <h4>На что обратить внимание</h4>
              <div v-if="factorsByDirection(item, 'negative').length" class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'negative')"
                  :key="`${item.id}-negative-${factor.code}`"
                  class="hot-card__factor hot-card__factor--negative"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
              <p v-else class="hot-card__calm-note">
                Явные рискованные параметры в расчёте не выделены, но товар всё равно требует проверки маржинальности,
                поставщика и конкуренции.
              </p>
            </section>

            <section v-if="factorsByDirection(item, 'neutral').length" class="hot-card__details-section">
              <h4>Дополнительные признаки</h4>
              <div class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'neutral')"
                  :key="`${item.id}-neutral-${factor.code}`"
                  class="hot-card__factor"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
            </section>

            <div v-if="item.wbProductId" class="hot-card__details-footer">
              <button
                class="hot-card__locate"
                type="button"
                @click="locate(item)"
              >
                Показать в таблице
              </button>
            </div>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.hot-products {
  position: relative;
  overflow: visible;
  border: 0;
  background: transparent;
  box-shadow: none;
}

.hot-products::before {
  display: none;
  content: none;
}

.hot-products__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  border-bottom: 0;
  padding: 0 0 var(--space-3);
}

.hot-products__heading {
  display: grid;
  gap: var(--space-2);
  max-width: 58rem;
}

.hot-products__title-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.hot-products__title-row h2 {
  margin: 0;
  color: var(--color-text);
  font-size: clamp(1.15rem, 1.55vw, 1.5rem);
  font-weight: 860;
  letter-spacing: 0;
}

.hot-products__badge {
  border: 1px solid rgb(249 115 22 / 0.34);
  border-radius: var(--radius-sm);
  background: rgb(249 115 22 / 0.1);
  color: var(--accent-ember-text-strong);
  padding: 0.28rem 0.48rem;
  font-size: 0.74rem;
  font-weight: 760;
  line-height: 1;
}

.hot-products__heading p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
  line-height: 1.5;
}

.hot-products__state {
  display: flex;
  min-height: 6rem;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: var(--space-5);
  color: var(--color-text-muted);
  font-size: 0.875rem;
  text-align: center;
}

.hot-products__state--empty {
  display: grid;
  gap: var(--space-1);
}

.hot-products__state--empty strong {
  color: var(--color-text);
}

.hot-products__state--error {
  color: var(--state-danger);
}

.hot-products__pulse {
  width: 0.7rem;
  height: 0.7rem;
  border-radius: 999px;
  background: var(--accent-ember);
  box-shadow: 0 0 0 0 rgb(249 115 22 / 0.32);
  animation: hot-pulse 1.2s ease-out infinite;
}

.hot-products__showcase {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--space-4);
  overflow: visible;
  padding: var(--space-3) 0 var(--space-4) 3.2rem;
}

.hot-card {
  --flame-aura: rgb(249 115 22 / 0.2);
  --flame-core: rgb(249 115 22 / 0.42);
  --flame-deep: rgb(127 29 29 / 0.2);
  --flame-edge: rgb(249 115 22 / 0.16);
  --flame-hot: rgb(255 196 87 / 0.42);
  --flame-outline: rgb(249 115 22 / 0.58);
  --flame-shadow: rgb(185 28 28 / 0.18);
  position: relative;
  isolation: isolate;
  overflow: visible;
  border: 1px solid rgb(249 115 22 / 0.18);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.028), transparent),
    rgb(10 13 18 / 0.94);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.035),
    0 16px 34px rgb(0 0 0 / 0.22);
  transition:
    border-color 140ms ease,
    box-shadow 140ms ease,
    transform 140ms ease;
}

.hot-card::after {
  position: absolute;
  content: '';
  pointer-events: none;
}

.hot-card::after {
  z-index: 0;
  inset: -1px -1px auto;
  height: 7.75rem;
  border-radius: inherit;
  border: 1px solid color-mix(in srgb, var(--flame-outline), transparent 42%);
  opacity: var(--edge-opacity, 0.5);
  background: linear-gradient(90deg, var(--flame-outline), transparent 5rem);
  clip-path: polygon(0 0, 100% 0, 100% 100%, 0 100%, 0 82%, 1.1% 75%, 0 66%, 1.3% 56%, 0 45%, 1.1% 34%, 0 24%);
  transition: opacity 140ms ease, border-color 140ms ease;
}

.hot-card__flame {
  position: absolute;
  z-index: 4;
  top: 0.02rem;
  left: -3.2rem;
  width: 5.45rem;
  height: 7.65rem;
  pointer-events: none;
  filter:
    drop-shadow(0.06rem 0 0 var(--flame-outline))
    drop-shadow(0 0 0.5rem var(--flame-shadow));
  opacity: var(--flame-opacity, 0.86);
  transition: filter 140ms ease, opacity 140ms ease;
}

.hot-card__flame-outer {
  fill: var(--flame-outer, rgb(220 38 38 / 0.68));
  stroke: var(--flame-outline);
  stroke-linejoin: round;
  stroke-width: 2;
}

.hot-card__flame-middle {
  fill: var(--flame-middle, rgb(249 115 22 / 0.76));
}

.hot-card__flame-inner {
  fill: var(--flame-inner, rgb(251 146 60 / 0.8));
}

.hot-card__flame-core {
  fill: var(--flame-core-fill, rgb(254 240 138 / 0.82));
}

.hot-card:hover,
.hot-card--open {
  transform: none;
}

.hot-card:hover {
  border-color: color-mix(in srgb, var(--flame-outline), transparent 22%);
}

.hot-card:hover::after {
  opacity: 0.72;
}

.hot-card:hover .hot-card__flame {
  filter:
    drop-shadow(0.06rem 0 0 var(--flame-outline))
    drop-shadow(0 0 0.72rem var(--flame-shadow));
  opacity: 1;
}

.hot-card--steady {
  --flame-aura: rgb(249 115 22 / 0.12);
  --flame-core: rgb(249 115 22 / 0.24);
  --flame-deep: rgb(127 29 29 / 0.12);
  --flame-edge: rgb(249 115 22 / 0.14);
  --flame-hot: rgb(255 196 87 / 0.18);
  --flame-outline: rgb(249 115 22 / 0.34);
  --flame-outer: rgb(194 65 12 / 0.48);
  --flame-middle: rgb(249 115 22 / 0.5);
  --flame-inner: rgb(251 146 60 / 0.56);
  --flame-core-fill: rgb(253 186 116 / 0.5);
  --flame-shadow: rgb(249 115 22 / 0.08);
  --flame-opacity: 0.72;
  --edge-opacity: 0.58;
  border-color: rgb(249 115 22 / 0.24);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.035),
    0 12px 24px rgb(0 0 0 / 0.2);
}

.hot-card--strong {
  --flame-aura: rgb(249 115 22 / 0.18);
  --flame-core: rgb(249 115 22 / 0.4);
  --flame-deep: rgb(127 29 29 / 0.18);
  --flame-edge: rgb(249 115 22 / 0.24);
  --flame-hot: rgb(255 196 87 / 0.34);
  --flame-outline: rgb(249 115 22 / 0.58);
  --flame-outer: rgb(220 38 38 / 0.6);
  --flame-middle: rgb(234 88 12 / 0.76);
  --flame-inner: rgb(249 115 22 / 0.72);
  --flame-core-fill: rgb(254 215 170 / 0.72);
  --flame-shadow: rgb(249 115 22 / 0.13);
  --flame-opacity: 0.9;
  --edge-opacity: 0.72;
  border-color: rgb(249 115 22 / 0.38);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.04),
    0 14px 28px rgb(0 0 0 / 0.22),
    0 0 10px rgb(249 115 22 / 0.035);
}

.hot-card--priority {
  --flame-aura: rgb(185 28 28 / 0.25);
  --flame-core: rgb(239 68 68 / 0.42);
  --flame-deep: rgb(127 29 29 / 0.28);
  --flame-edge: rgb(239 68 68 / 0.28);
  --flame-hot: rgb(255 196 87 / 0.4);
  --flame-outline: rgb(248 113 113 / 0.68);
  --flame-outer: rgb(185 28 28 / 0.72);
  --flame-middle: rgb(234 88 12 / 0.84);
  --flame-inner: rgb(249 115 22 / 0.86);
  --flame-core-fill: rgb(254 240 138 / 0.86);
  --flame-shadow: rgb(185 28 28 / 0.2);
  --flame-opacity: 1;
  --edge-opacity: 0.82;
  border-color: rgb(185 28 28 / 0.46);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.045),
    0 15px 30px rgb(0 0 0 / 0.24),
    0 0 12px rgb(185 28 28 / 0.045);
  animation: none;
}

.hot-card--open {
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.04),
    0 12px 24px rgb(0 0 0 / 0.2);
}

.hot-card__content {
  position: relative;
  z-index: auto;
  overflow: hidden;
  border-radius: inherit;
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.026), transparent 60%),
    rgb(8 11 16 / 0.88);
}

.hot-card__main {
  display: grid;
  position: relative;
  grid-template-columns: 5.75rem minmax(0, 1fr) minmax(9rem, 11rem);
  gap: var(--space-3);
  align-items: stretch;
  height: 7.75rem;
  padding: 0.25rem var(--space-3) 0.25rem 0.25rem;
  cursor: pointer;
}

.hot-card__main::before {
  position: absolute;
  z-index: 0;
  inset: 0;
  background: linear-gradient(90deg, rgb(249 115 22 / 0.07), rgb(255 255 255 / 0.025) 34%, transparent 72%);
  content: '';
  opacity: 0;
  pointer-events: none;
  transition: opacity 140ms ease;
}

.hot-card:hover .hot-card__main::before {
  opacity: 1;
}

.hot-card__main:focus-visible {
  outline: 2px solid var(--accent-primary-border);
  outline-offset: -2px;
}

.hot-card__image-wrap {
  display: grid;
  position: relative;
  z-index: 6;
  width: 5.75rem;
  height: 7.25rem;
  min-height: 7.25rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 0.08);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  transition: border-color 140ms ease, box-shadow 140ms ease;
}

.hot-card:hover .hot-card__image-wrap {
  border-color: rgb(249 115 22 / 0.28);
  box-shadow: 0 0 0 1px rgb(249 115 22 / 0.1);
}

.hot-card__image-wrap :deep(.market-image__asset),
.hot-card__image-wrap :deep(.market-image__preview) {
  object-fit: cover;
}

.hot-card__body {
  display: grid;
  position: relative;
  z-index: 1;
  min-width: 0;
  align-content: start;
  gap: 0.3rem;
  padding-block: 0.36rem;
}

.hot-card__topline,
.hot-card__identity {
  display: flex;
  min-width: 0;
  flex-wrap: wrap;
  gap: 0.3rem 0.45rem;
  color: var(--color-text-muted);
  font-size: 0.73rem;
  line-height: 1.25;
}

.hot-card__topline span,
.hot-card__identity span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__topline span + span::before,
.hot-card__identity span + span::before {
  color: var(--color-text-subtle);
  content: '· ';
}

.hot-card__name {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 800;
  letter-spacing: 0;
  line-height: 1.24;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.hot-card__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.4rem;
  margin-top: 0.16rem;
}

.hot-card__metric {
  display: grid;
  min-width: 0;
  gap: 0.11rem;
  border: 1px solid rgb(255 255 255 / 0.07);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.032);
  padding: 0.32rem 0.46rem;
}

.hot-card__metric strong {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.9rem;
  font-weight: 780;
  line-height: 1.15;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__metric span {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: 0.68rem;
  line-height: 1.15;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__metric--price {
  border-color: rgb(249 115 22 / 0.17);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), rgb(249 115 22 / 0.025)),
    rgb(255 255 255 / 0.032);
}

.hot-card__rating--positive {
  color: var(--state-success) !important;
}

.hot-card__rating--warning {
  color: var(--state-warning) !important;
}

.hot-card__rating--negative {
  color: var(--state-danger) !important;
}

.hot-card__rating--neutral {
  color: var(--color-text-muted) !important;
}

.hot-card__side {
  display: grid;
  position: relative;
  z-index: 1;
  align-content: center;
  align-self: center;
  box-sizing: border-box;
  height: calc(100% - 0.5rem);
  min-height: 0;
  gap: 0.42rem;
  border: 1px solid var(--flame-edge);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.11), rgb(249 115 22 / 0.03)),
    rgb(255 255 255 / 0.028);
  padding: 0.52rem 0.72rem;
}

.hot-card__score {
  display: grid;
  justify-items: center;
  gap: 0.16rem;
  text-align: center;
}

.hot-card__score strong {
  color: var(--accent-ember-text-strong);
  font-size: clamp(1.16rem, 1.45vw, 1.38rem);
  font-weight: 880;
  line-height: 1;
}

.hot-card__score span {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.72rem;
  line-height: 1.2;
}

.hot-card__confidence {
  display: inline-flex;
  width: max-content;
  max-width: 100%;
  align-items: center;
  gap: 0.45rem;
  border: 1px solid rgb(255 255 255 / 0.08);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.035);
  color: var(--color-text-muted);
  font-size: 0.74rem;
  line-height: 1.2;
  padding: 0.34rem 0.5rem;
}

.hot-card__confidence strong {
  color: var(--color-text);
  font-size: 0.9rem;
  line-height: 1;
}

.hot-card__confidence span {
  color: var(--color-text-muted);
}

.hot-card__why-link {
  justify-self: center;
  width: max-content;
  max-width: 100%;
  border: 0;
  background: transparent;
  color: rgb(255 167 89);
  cursor: pointer;
  font: inherit;
  font-size: 0.86rem;
  font-weight: 820;
  line-height: 1.2;
  padding: 0.04rem 0;
  transition:
    color 120ms ease;
}

.hot-card__why-link:hover,
.hot-card__why-link:focus-visible {
  color: rgb(255 214 170);
  text-decoration: underline;
  text-underline-offset: 0.18rem;
  outline: none;
}

.hot-card__details {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-2);
  max-width: 100%;
  border-top: 1px solid rgb(249 115 22 / 0.16);
  background: rgb(4 7 12 / 0.34);
  padding: var(--space-3);
}

.hot-card__details-section {
  display: grid;
  align-content: start;
  min-width: 0;
  gap: var(--space-2);
  border: 1px solid rgb(249 115 22 / 0.15);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.03);
  padding: var(--space-3);
}

.hot-card__details-section--wide {
  grid-column: 1 / -1;
}

.hot-card__details-section h4 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.8rem;
  font-weight: 780;
}

.hot-card__details-section strong {
  display: -webkit-box;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.82rem;
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.hot-card__details-section p {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: 0.78rem;
  line-height: 1.48;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 4;
}

.hot-card__details-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-start;
  gap: var(--space-2);
  margin-top: 0;
}

.hot-card__details-footer {
  display: flex;
  grid-column: 1 / -1;
  justify-content: flex-start;
  border-top: 1px solid rgb(249 115 22 / 0.12);
  padding-top: var(--space-2);
}

.hot-card__locate {
  border: 1px solid rgb(249 115 22 / 0.28);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.12), rgb(249 115 22 / 0.035)),
    rgb(255 255 255 / 0.025);
  color: var(--accent-ember-text-strong);
  cursor: pointer;
  font: inherit;
  font-size: 0.78rem;
  font-weight: 760;
  padding: 0.42rem 0.68rem;
}

.hot-card__locate:hover,
.hot-card__locate:focus-visible {
  border-color: rgb(251 146 60 / 0.46);
  color: rgb(255 214 170);
  outline: none;
}

.hot-card__factor-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.hot-card__factor {
  display: inline-grid;
  max-width: 100%;
  gap: 0.14rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.035);
  color: var(--color-text-muted);
  padding: 0.38rem 0.5rem;
  font-size: 0.74rem;
  line-height: 1.2;
}

.hot-card__factor strong,
.hot-card__factor span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__factor strong {
  color: var(--color-text);
}

.hot-card__factor--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.hot-card__factor--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.hot-card__calm-note {
  border-left: 2px solid var(--state-warning-border);
  padding-left: var(--space-2);
}

@media (max-width: 1180px) {
  .hot-card__main {
    grid-template-columns: 7.5rem minmax(0, 1fr);
  }

  .hot-card__side {
    grid-column: 1 / -1;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    align-items: center;
  }

  .hot-card__metrics {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .hot-products__showcase {
    padding: var(--space-3) 0 var(--space-3) 2.35rem;
  }

  .hot-card__flame {
    left: -2.35rem;
    width: 3.95rem;
  }

  .hot-card__main,
  .hot-card__side,
  .hot-card__details {
    grid-template-columns: 1fr;
  }

  .hot-card__image-wrap {
    width: 5.75rem;
    height: 7.25rem;
    min-height: 7.25rem;
  }

  .hot-card__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (prefers-reduced-motion: reduce) {
  .hot-card,
  .hot-products__pulse {
    animation: none;
  }

  .hot-card,
  .hot-card__why-link,
  .hot-card__locate {
    transition: none;
  }
}

@keyframes hot-pulse {
  to {
    box-shadow: 0 0 0 0.55rem rgb(249 115 22 / 0);
  }
}

@keyframes hot-card-breathe {
  0%,
  100% {
    box-shadow:
      inset 0 1px 0 rgb(255 255 255 / 0.045),
      0 15px 30px rgb(0 0 0 / 0.24),
      0 0 10px rgb(185 28 28 / 0.04);
  }

  50% {
    box-shadow:
      inset 0 1px 0 rgb(255 255 255 / 0.045),
      0 15px 30px rgb(0 0 0 / 0.24),
      0 0 14px rgb(185 28 28 / 0.055);
  }
}
</style>
