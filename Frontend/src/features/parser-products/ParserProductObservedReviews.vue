<script setup lang="ts">
import { ExternalLink } from 'lucide-vue-next';
import { computed, ref, watch } from 'vue';

import { getParserReview, getParserReviewReplies, getParserReviews } from '@/features/parser-reviews/parserReviews.api';
import type {
  ParserReviewDetail,
  ParserReviewListItem,
  ParserReviewReply
} from '@/features/parser-reviews/parserReviews.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import type { ParserProductListItem } from './parserProducts.types';
import { getWildberriesProductUrl } from './wildberriesLinks';

const props = defineProps<{
  product: ParserProductListItem;
}>();

const pageSize = 10;
const rows = ref<ParserReviewListItem[]>([]);
const totalCount = ref(0);
const page = ref(1);
const selected = ref<ParserReviewListItem | null>(null);
const detail = ref<ParserReviewDetail | null>(null);
const replies = ref<ParserReviewReply[]>([]);
const listLoading = ref(false);
const detailLoading = ref(false);
const repliesLoading = ref(false);
const listError = ref('');
const detailError = ref('');
const repliesError = ref('');
let listVersion = 0;
let detailVersion = 0;
let repliesVersion = 0;

const rootScoped = computed(() => Boolean(props.product.wbRootId));
const pageCount = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)));
const pageStart = computed(() => (totalCount.value === 0 ? 0 : (page.value - 1) * pageSize + 1));
const pageEnd = computed(() => Math.min(totalCount.value, page.value * pageSize));
const displayReview = computed(() => detail.value ?? selected.value);
const detailProductUrl = computed(() => getWildberriesProductUrl(displayReview.value?.wbProductId));

watch(
  () => [props.product.id, props.product.wbRootId, props.product.wbProductId] as const,
  async () => {
    page.value = 1;
    selected.value = null;
    clearDetail();
    await loadRows();
  },
  { immediate: true }
);

watch(
  () => page.value,
  async () => {
    await loadRows();
  }
);

watch(
  () => selected.value?.id,
  async (id) => {
    if (!id) {
      clearDetail();
      return;
    }

    await Promise.all([loadDetail(id), loadReplies(id)]);
  }
);

async function loadRows() {
  const version = ++listVersion;
  listLoading.value = true;
  listError.value = '';

  try {
    const response = await getParserReviews({
      page: page.value,
      pageSize,
      sort: '-createdAtOnMp',
      ...(rootScoped.value
        ? { sourceWbRootId: props.product.wbRootId ?? undefined }
        : { wbProductId: props.product.wbProductId })
    });

    if (version === listVersion) {
      rows.value = response.items;
      totalCount.value = response.totalCount;
    }
  } catch (err) {
    if (version === listVersion) {
      rows.value = [];
      totalCount.value = 0;
      listError.value = getProblemMessage(err, 'Не удалось загрузить отзывы.');
    }
  } finally {
    if (version === listVersion) {
      listLoading.value = false;
    }
  }
}

async function loadDetail(id: string) {
  const version = ++detailVersion;
  detailLoading.value = true;
  detailError.value = '';
  detail.value = null;

  try {
    const response = await getParserReview(id);
    if (version === detailVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailVersion) {
      detailError.value = getProblemMessage(err, 'Не удалось загрузить детали отзыва.');
    }
  } finally {
    if (version === detailVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadReplies(id: string) {
  const version = ++repliesVersion;
  repliesLoading.value = true;
  repliesError.value = '';
  replies.value = [];

  try {
    const response = await getParserReviewReplies(id, {
      page: 1,
      pageSize: 50,
      sort: '-createdAtOnMp'
    });

    if (version === repliesVersion) {
      replies.value = response.items;
    }
  } catch (err) {
    if (version === repliesVersion) {
      repliesError.value = getProblemMessage(err, 'Не удалось загрузить ответы продавца.');
    }
  } finally {
    if (version === repliesVersion) {
      repliesLoading.value = false;
    }
  }
}

function clearDetail() {
  detail.value = null;
  replies.value = [];
  detailError.value = '';
  repliesError.value = '';
  detailLoading.value = false;
  repliesLoading.value = false;
}

function changePage(value: number) {
  page.value = value;
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
    ? new Intl.DateTimeFormat('ru-RU', {
        month: 'short',
        day: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      }).format(date)
    : '-';
}

function ratingTone(value: number | null): string {
  if (value === null) {
    return 'unknown';
  }

  if (value <= 2) {
    return 'negative';
  }

  return value === 3 ? 'neutral' : 'positive';
}
</script>

<template>
  <section class="reviews">
    <header class="reviews__header">
      <div>
        <h3>Отзывы</h3>
        <p>Найденные отзывы по данным сервиса.</p>
      </div>
    </header>

    <LoadingState v-if="listLoading" class="review-state" :rows="3" />
    <EmptyState
      v-else-if="listError"
      class="review-state"
      title="Не удалось загрузить отзывы"
      :description="listError"
    />
    <EmptyState
      v-else-if="rows.length === 0"
      class="review-state"
      title="Отзывы не найдены"
      description="Для этого товара отзывы не найдены в данных сервиса."
    />
    <div v-else class="review-list">
      <button
        v-for="row in rows"
        :key="row.id"
        class="review-card"
        :class="[`review-card--${ratingTone(row.rating)}`, { 'review-card--selected': selected?.id === row.id }]"
        type="button"
        :aria-label="`Открыть детали отзыва с рейтингом ${fieldValue(row.rating)}`"
        @click="selected = row"
      >
        <span class="rating-pill" :class="`rating-pill--${ratingTone(row.rating)}`">
          {{ fieldValue(row.rating) }}
        </span>
        <span class="review-card__body">
          <strong>{{ fieldValue(row.textPreview) }}</strong>
          <small>{{ formatDate(row.createdAtOnMp) }}</small>
        </span>
        <Badge :tone="row.hasObservedReply ? 'success' : 'neutral'">
          {{ row.hasObservedReply ? 'Есть ответ' : 'Без ответа' }}
        </Badge>
      </button>

      <footer class="pager">
        <span class="numeric">Показаны {{ pageStart }}-{{ pageEnd }} из {{ totalCount }}</span>
        <div class="pager__actions">
          <Button variant="secondary" :disabled="page <= 1" @click="changePage(page - 1)">Назад</Button>
          <span class="numeric">Страница {{ page }} / {{ pageCount }}</span>
          <Button variant="secondary" :disabled="page >= pageCount" @click="changePage(page + 1)">Далее</Button>
        </div>
      </footer>
    </div>

    <section v-if="displayReview" class="detail">
      <header class="detail__header">
        <div class="detail__summary">
          <span class="rating-pill rating-pill--large" :class="`rating-pill--${ratingTone(displayReview.rating)}`">
            {{ fieldValue(displayReview.rating) }}
          </span>
          <dl>
            <div><dt>Дата</dt><dd>{{ formatDate(displayReview.createdAtOnMp) }}</dd></div>
            <div><dt>Автор</dt><dd>{{ fieldValue(detail?.reviewerName) }}</dd></div>
            <div>
              <dt>Ответ</dt>
              <dd>{{ displayReview.hasObservedReply ? 'Есть ответ продавца' : 'Ответ не найден' }}</dd>
            </div>
          </dl>
        </div>
        <Button variant="ghost" @click="selected = null">Закрыть</Button>
      </header>

      <LoadingState v-if="detailLoading" class="detail-state" :rows="2" />
      <div v-if="detailError" class="notice">{{ detailError }}</div>

      <section class="detail__section">
        <h4>Отзыв</h4>
        <p class="body-text">{{ fieldValue(detail?.text ?? displayReview.textPreview) }}</p>
        <dl class="review-fields">
          <div><dt>Плюсы</dt><dd class="body-text">{{ fieldValue(detail?.pros) }}</dd></div>
          <div><dt>Минусы</dt><dd class="body-text">{{ fieldValue(detail?.cons) }}</dd></div>
        </dl>
      </section>

      <section class="detail__section">
        <h4>Ответ продавца</h4>
        <LoadingState v-if="repliesLoading" class="detail-state" :rows="2" />
        <div v-else-if="repliesError" class="notice">{{ repliesError }}</div>
        <p v-else-if="replies.length === 0" class="placeholder">Ответ продавца не найден.</p>
        <div v-else class="reply-list">
          <article v-for="reply in replies" :key="reply.id" class="reply">
            <p class="body-text">{{ fieldValue(reply.text) }}</p>
            <small>{{ formatDate(reply.createdAtOnMp) }}</small>
          </article>
        </div>
      </section>

      <section v-if="detailProductUrl" class="detail__actions">
        <a :href="detailProductUrl" target="_blank" rel="noreferrer">
          <ExternalLink :size="16" />
          Открыть карточку на WB
        </a>
      </section>
    </section>
  </section>
</template>

<style scoped>
.reviews,
.review-list,
.detail,
.detail__section,
.reply-list {
  display: grid;
  gap: var(--space-3);
}

.reviews__header,
.review-state,
.review-list,
.detail,
.detail__section {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.reviews__header h3,
.reviews__header p,
.detail h4,
.detail p,
.reply p {
  margin: 0;
}

.reviews__header h3,
.detail h4 {
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.reviews__header p,
.review-card small,
.reply small,
.pager {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.review-card {
  position: relative;
  display: grid;
  min-width: 0;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text);
  padding: var(--space-3);
  text-align: left;
}

.review-card::before {
  position: absolute;
  inset: 0 auto 0 0;
  width: 2px;
  border-radius: var(--radius-sm) 0 0 var(--radius-sm);
  background: var(--color-border-strong);
  content: '';
}

.review-card:hover,
.review-card--selected {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
}

.review-card--negative::before {
  background: var(--state-danger);
}

.review-card--neutral::before {
  background: var(--state-warning);
}

.review-card--positive::before {
  background: var(--state-success);
}

.review-card__body {
  display: grid;
  min-width: 0;
  gap: var(--space-1);
}

.review-card__body strong {
  display: -webkit-box;
  overflow: hidden;
  line-height: 1.38;
  font-size: 0.875rem;
  font-weight: 620;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.rating-pill {
  display: grid;
  min-height: 2rem;
  min-width: 2rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--color-text);
  font-weight: 740;
}

.rating-pill--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
}

.rating-pill--neutral {
  border-color: var(--state-warning-border);
  background: var(--state-warning-soft);
}

.rating-pill--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
}

.rating-pill--large {
  min-height: 2.5rem;
  min-width: 2.5rem;
  font-size: 1rem;
}

.pager,
.pager__actions,
.detail__header,
.detail__summary,
.detail__actions a {
  display: flex;
  gap: var(--space-3);
}

.pager,
.detail__header {
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
}

.pager__actions,
.detail__summary {
  align-items: center;
}

.detail {
  background: var(--surface-control);
}

.detail__summary {
  min-width: 0;
}

.detail__summary dl,
.review-fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.detail__summary dl {
  grid-template-columns: repeat(3, minmax(0, auto));
}

.detail__summary div,
.review-fields div {
  display: grid;
  gap: 0.2rem;
}

.detail dt,
.review-fields dt {
  color: var(--color-text-muted);
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
}

.detail dd,
.review-fields dd {
  margin: 0;
}

.body-text {
  line-height: 1.5;
  white-space: pre-wrap;
}

.reply,
.placeholder,
.notice {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.reply {
  display: grid;
  gap: var(--space-2);
}

.placeholder {
  border-style: dashed;
  color: var(--color-text-muted);
}

.notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.detail__actions a {
  width: fit-content;
  min-height: 2.125rem;
  align-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  color: var(--color-text);
  padding: 0 var(--space-3);
  font-size: 0.8125rem;
  font-weight: 680;
  text-decoration: none;
}

.detail__actions a:hover {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
}

@media (min-width: 680px) {
  .review-fields {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 679px) {
  .review-card {
    grid-template-columns: auto minmax(0, 1fr);
  }

  .review-card :deep(.badge) {
    grid-column: 2;
    width: fit-content;
  }

  .detail__summary,
  .detail__summary dl {
    width: 100%;
  }

  .detail__summary dl {
    grid-template-columns: 1fr;
  }
}
</style>
