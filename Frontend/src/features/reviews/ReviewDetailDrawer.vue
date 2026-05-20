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
  formatRating,
  getReplyStateLabel,
  getReplyStateTone,
  getReplyStatusLabel,
  getReplyStatusTone,
  getReviewSignal
} from './reviewDisplay';
import { getReview, getReviewReplies } from './reviews.api';
import type { ReviewDetail, ReviewListItem, ReviewReplyListItem } from './reviews.types';

const props = defineProps<{
  open: boolean;
  review: ReviewListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<ReviewDetail | null>(null);
const replies = ref<ReviewReplyListItem[]>([]);
const detailLoading = ref(false);
const repliesLoading = ref(false);
const detailError = ref('');
const repliesError = ref('');
let detailLoadVersion = 0;
let repliesLoadVersion = 0;

const displayReview = computed(() => detail.value ?? props.review);
const displaySignal = computed(() => (displayReview.value ? getReviewSignal(displayReview.value) : null));

watch(
  () => [props.open, props.review?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      replies.value = [];
      detailError.value = '';
      repliesError.value = '';
      return;
    }

    await Promise.all([loadDetail(id), loadReplies(id)]);
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
    const response = await getReview(id);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load review details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadReplies(idReview: string): Promise<void> {
  const version = ++repliesLoadVersion;

  repliesLoading.value = true;
  repliesError.value = '';
  replies.value = [];

  try {
    const response = await getReviewReplies({
      page: 1,
      pageSize: 50,
      sort: '-dateCreate',
      idReview
    });

    if (version === repliesLoadVersion) {
      replies.value = response.items;
    }
  } catch (err) {
    if (version === repliesLoadVersion) {
      repliesError.value = getProblemMessage(err, 'Unable to load linked replies.');
    }
  } finally {
    if (version === repliesLoadVersion) {
      repliesLoading.value = false;
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
      <button class="drawer-shell__backdrop" type="button" aria-label="Close review detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="review-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayReview" class="drawer__title">
            <Badge v-if="displaySignal" :tone="displaySignal.tone">{{ displaySignal.label }}</Badge>
            <h2 id="review-detail-title">Review {{ compactId(displayReview.id) }}</h2>
            <p>{{ displayReview.idOnMp }} / Product {{ compactId(displayReview.idProduct) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close review detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayReview" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Rating</span>
              <strong class="numeric">{{ formatRating(displayReview.rating) }}</strong>
            </div>
            <div>
              <span>Reply state</span>
              <strong>{{ getReplyStateLabel(displayReview.isReplied) }}</strong>
            </div>
            <div>
              <span>Created</span>
              <strong class="numeric">{{ formatDateTime(displayReview.dateCreate) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Review text</h3>
            <p class="drawer__text">{{ fieldValue(displayReview.text) }}</p>
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Review ID</dt><dd>{{ displayReview.id }}</dd></div>
              <div><dt>Product ID</dt><dd>{{ displayReview.idProduct }}</dd></div>
              <div><dt>Marketplace ID</dt><dd>{{ displayReview.idOnMp }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Rating</dt><dd>{{ formatRating(displayReview.rating) }}</dd></div>
              <div>
                <dt>Reply state</dt>
                <dd>
                  <Badge :tone="getReplyStateTone(displayReview.isReplied)">
                    {{ getReplyStateLabel(displayReview.isReplied) }}
                  </Badge>
                </dd>
              </div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayReview.dateCreate) }}</dd></div>
              <div><dt>Reply date</dt><dd>{{ formatDateTime(displayReview.dateReply) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Linked replies</h3>
            <LoadingState v-if="repliesLoading" class="drawer__loading" :rows="2" />

            <div v-else-if="repliesError" class="drawer__notice">
              {{ repliesError }}
            </div>

            <div v-else-if="replies.length === 0" class="drawer__placeholder">
              No linked replies returned by the current API response.
            </div>

            <div v-else class="reply-list">
              <article v-for="reply in replies" :key="reply.id" class="reply">
                <header class="reply__header">
                  <Badge :tone="getReplyStatusTone()">{{ getReplyStatusLabel(reply.status) }}</Badge>
                  <code :title="reply.id">{{ compactId(reply.id) }}</code>
                </header>
                <p>{{ reply.text }}</p>
                <dl class="reply__fields">
                  <div><dt>Marketplace ID</dt><dd>{{ reply.idOnMp }}</dd></div>
                  <div><dt>Created</dt><dd>{{ formatDateTime(reply.dateCreate) }}</dd></div>
                  <div><dt>Updated</dt><dd>{{ formatDateTime(reply.dateUpdate) }}</dd></div>
                </dl>
              </article>
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
  top: var(--space-3);
  right: var(--space-3);
  bottom: var(--space-3);
  display: grid;
  width: min(40rem, calc(100vw - 1.5rem));
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
.drawer__fields dt,
.reply__fields dt {
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

.drawer__fields,
.reply__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields div,
.reply__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__fields dd,
.reply__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__text,
.reply p {
  margin: 0;
  color: var(--color-text);
  line-height: 1.55;
  white-space: pre-wrap;
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

.reply-list {
  display: grid;
  gap: var(--space-2);
}

.reply {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-3);
}

.reply__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.reply__header code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

@media (min-width: 680px) {
  .drawer__section--summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two,
  .reply__fields {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
