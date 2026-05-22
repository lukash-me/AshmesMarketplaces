<script setup lang="ts">
import { X } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import { getParserReview, getParserReviewReplies } from './parserReviews.api';
import type {
  ParserReviewDetail,
  ParserReviewListItem,
  ParserReviewReply
} from './parserReviews.types';

const props = defineProps<{
  open: boolean;
  review: ParserReviewListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<ParserReviewDetail | null>(null);
const replies = ref<ParserReviewReply[]>([]);
const detailLoading = ref(false);
const repliesLoading = ref(false);
const detailError = ref('');
const repliesError = ref('');
let detailVersion = 0;
let repliesVersion = 0;

const displayReview = computed(() => detail.value ?? props.review);

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

onMounted(() => window.addEventListener('keydown', onKeydown));
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown));

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
      detailError.value = getProblemMessage(err, 'Unable to load staged parser review.');
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
      repliesError.value = getProblemMessage(err, 'Unable to load observed parser replies.');
    }
  } finally {
    if (version === repliesVersion) {
      repliesLoading.value = false;
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

function fieldValue(value: string | number | boolean | null | undefined): string {
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
      <button class="drawer-shell__backdrop" type="button" aria-label="Close parser review detail" @click="close" />
      <aside class="drawer app-surface" role="dialog" aria-modal="true" aria-labelledby="parser-review-title">
        <header class="drawer__header">
          <div v-if="displayReview" class="drawer__title">
            <div class="badge-row">
              <Badge tone="info">Staged parser row</Badge>
              <Badge v-if="displayReview.reviewAttributionMode === 'root_payload'" tone="info">
                Root payload attribution
              </Badge>
            </div>
            <h2 id="parser-review-title">Review {{ displayReview.reviewIdOnMp }}</h2>
            <p>{{ displayReview.wbProductId }} / root {{ displayReview.sourceWbRootId }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close parser review detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayReview" class="drawer__body">
          <section class="warning-stack">
            <div v-if="displayReview.isPartialSnapshot" class="warning">
              <Badge tone="warning">Partial snapshot</Badge>
              <p>This row comes from a staged parser snapshot that may cover only part of the run scope.</p>
            </div>
            <div v-if="displayReview.isCappedRootPayload" class="warning warning--ember">
              <Badge tone="ember">Root payload capped</Badge>
              <p>Root fetch evidence shows the payload may contain fewer feedback rows than the root count reports.</p>
            </div>
            <div v-if="displayReview.isFullHistoryUnknown" class="warning">
              <Badge tone="neutral">Full history unknown</Badge>
              <p>Full review history is not proven for this root payload.</p>
            </div>
          </section>

          <section class="drawer__section drawer__summary">
            <div><span>Rating</span><strong class="numeric">{{ fieldValue(displayReview.rating) }}</strong></div>
            <div>
              <span>Reply evidence</span>
              <strong>{{ displayReview.hasObservedReply ? 'Observed reply' : 'No reply observed' }}</strong>
            </div>
            <div><span>Created on MP</span><strong class="numeric">{{ formatDate(displayReview.createdAtOnMp) }}</strong></div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />
          <section v-if="detailError" class="drawer__notice">{{ detailError }}</section>

          <section class="drawer__section">
            <h3>Review body</h3>
            <p class="body-text">{{ fieldValue(detail?.text ?? displayReview.textPreview) }}</p>
            <dl v-if="detail" class="drawer__fields drawer__fields--two">
              <div><dt>Pros</dt><dd class="body-text">{{ fieldValue(detail.pros) }}</dd></div>
              <div><dt>Cons</dt><dd class="body-text">{{ fieldValue(detail.cons) }}</dd></div>
            </dl>
          </section>

          <section v-if="detail" class="drawer__section">
            <h3>Reviewer and source</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Reviewer</dt><dd>{{ fieldValue(detail.reviewerName) }}</dd></div>
              <div><dt>Country</dt><dd>{{ fieldValue(detail.reviewerCountry) }}</dd></div>
              <div><dt>Has photo</dt><dd>{{ fieldValue(detail.reviewerHasPhoto) }}</dd></div>
              <div><dt>Helpful plus</dt><dd>{{ fieldValue(detail.helpfulPlus) }}</dd></div>
              <div><dt>Helpful minus</dt><dd>{{ fieldValue(detail.helpfulMinus) }}</dd></div>
              <div><dt>Input products run</dt><dd>{{ fieldValue(detail.inputProductsParserRunId) }}</dd></div>
              <div><dt>Category</dt><dd>{{ fieldValue(detail.sourceCategory) }}</dd></div>
              <div><dt>Subcategory</dt><dd>{{ fieldValue(detail.sourceSubcategory) }}</dd></div>
              <div><dt>Query</dt><dd>{{ fieldValue(detail.sourceQuery) }}</dd></div>
              <div><dt>Region</dt><dd>{{ fieldValue(detail.sourceRegionDest) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Observed replies</h3>
            <LoadingState v-if="repliesLoading" class="drawer__loading" :rows="2" />
            <div v-else-if="repliesError" class="drawer__notice">{{ repliesError }}</div>
            <div v-else-if="replies.length === 0" class="drawer__placeholder">
              No reply observed in this staged snapshot.
            </div>
            <div v-else class="reply-list">
              <article v-for="reply in replies" :key="reply.id" class="reply">
                <header class="reply__header">
                  <div class="badge-row">
                    <Badge tone="success">Observed reply</Badge>
                    <Badge v-if="!reply.hasStableReplyId" tone="ember">Fallback reply identity</Badge>
                  </div>
                  <code>{{ reply.replyIdOnMp || reply.replyFallbackHash }}</code>
                </header>
                <p class="body-text">{{ fieldValue(reply.text) }}</p>
                <p v-if="!reply.hasStableReplyId" class="reply__warning">
                  Marketplace reply id was not observed in this staged payload.
                </p>
                <dl class="drawer__fields drawer__fields--two">
                  <div><dt>Author</dt><dd>{{ fieldValue(reply.replyAuthor) }}</dd></div>
                  <div><dt>State</dt><dd>{{ fieldValue(reply.replyState) }}</dd></div>
                  <div><dt>Created</dt><dd>{{ formatDate(reply.createdAtOnMp) }}</dd></div>
                  <div><dt>Updated</dt><dd>{{ formatDate(reply.updatedAtOnMp) }}</dd></div>
                  <div><dt>Source file kind</dt><dd>{{ reply.sourceFileKind }}</dd></div>
                  <div><dt>Source line</dt><dd>{{ reply.sourceLineNumber }}</dd></div>
                </dl>
              </article>
            </div>
          </section>

          <section v-if="detail?.rootFetch" class="drawer__section">
            <h3>Root fetch evidence</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Status</dt><dd>{{ detail.rootFetch.status }}</dd></div>
              <div><dt>Fetch time</dt><dd>{{ formatDate(detail.rootFetch.timestampUtc) }}</dd></div>
              <div><dt>Payload feedback count</dt><dd>{{ fieldValue(detail.rootFetch.payloadFeedbackCount) }}</dd></div>
              <div><dt>Payload rows seen</dt><dd>{{ detail.rootFetch.payloadFeedbackRowsSeen }}</dd></div>
              <div><dt>Selected rows seen</dt><dd>{{ detail.rootFetch.selectedReviewRowsSeen }}</dd></div>
              <div><dt>Reviews written</dt><dd>{{ detail.rootFetch.reviewsWritten }}</dd></div>
              <div><dt>Replies written</dt><dd>{{ detail.rootFetch.repliesWritten }}</dd></div>
            </dl>
          </section>

          <section v-if="detail" class="drawer__section">
            <h3>Parser lineage</h3>
            <dl class="drawer__fields">
              <div><dt>Parser run</dt><dd>{{ detail.parserRunId }}</dd></div>
              <div><dt>Parsed at</dt><dd>{{ formatDate(detail.parsedAtUtc) }}</dd></div>
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
  width: min(45rem, calc(100vw - 1.5rem));
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
.reply,
.warning-stack {
  display: grid;
  gap: var(--space-3);
}

.drawer__title {
  min-width: 0;
  gap: var(--space-2);
}

.drawer__title h2,
.drawer__title p,
.warning p,
.reply p {
  margin: 0;
}

.drawer__title h2 {
  overflow-wrap: anywhere;
  font-size: 1rem;
}

.drawer__title p,
.reply__header code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__body {
  align-content: start;
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section,
.warning,
.reply {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.warning {
  border-color: var(--state-warning-border);
  background: var(--state-warning-soft);
  color: var(--color-text);
  font-size: 0.8125rem;
}

.warning--ember {
  border-color: var(--heat-rising-border);
  background: var(--heat-rising-soft);
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

.body-text {
  color: var(--color-text);
  line-height: 1.55;
  white-space: pre-wrap;
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

.reply-list,
.badge-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.reply-list {
  display: grid;
}

.reply {
  border-radius: var(--radius-sm);
  background: var(--surface-control);
}

.reply__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.reply__warning {
  color: var(--heat-rising-text);
  font-size: 0.8125rem;
}

@media (min-width: 680px) {
  .drawer__summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
