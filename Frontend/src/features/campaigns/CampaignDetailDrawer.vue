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
  formatNumber,
  getCampaignNeutralTone,
  getCampaignStatusLabel,
  getCampaignTypeLabel
} from './campaignDisplay';
import { getCampaign, getCampaignMetrics } from './campaigns.api';
import type { CampaignDetail, CampaignListItem, CampaignMetricListItem } from './campaigns.types';

const LINKED_METRICS_PAGE_SIZE = 50;

const props = defineProps<{
  open: boolean;
  campaign: CampaignListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<CampaignDetail | null>(null);
const metrics = ref<CampaignMetricListItem[]>([]);
const metricsTotalCount = ref(0);
const detailLoading = ref(false);
const metricsLoading = ref(false);
const detailError = ref('');
const metricsError = ref('');
let detailLoadVersion = 0;
let metricsLoadVersion = 0;

const displayCampaign = computed(() => detail.value ?? props.campaign);
const formattedTimeToImpression = computed(() => formatJson(detail.value?.timeToImpression));
const hasHiddenMetrics = computed(() => metricsTotalCount.value > LINKED_METRICS_PAGE_SIZE);

watch(
  () => [props.open, props.campaign?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      metrics.value = [];
      metricsTotalCount.value = 0;
      detailError.value = '';
      metricsError.value = '';
      return;
    }

    await Promise.all([loadDetail(id), loadMetrics(id)]);
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
    const response = await getCampaign(id);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load campaign details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadMetrics(idCampaign: string): Promise<void> {
  const version = ++metricsLoadVersion;

  metricsLoading.value = true;
  metricsError.value = '';
  metrics.value = [];
  metricsTotalCount.value = 0;

  try {
    const response = await getCampaignMetrics({
      page: 1,
      pageSize: LINKED_METRICS_PAGE_SIZE,
      sort: '-date',
      idCampaign
    });

    if (version === metricsLoadVersion) {
      metrics.value = response.items;
      metricsTotalCount.value = response.totalCount;
    }
  } catch (err) {
    if (version === metricsLoadVersion) {
      metricsError.value = getProblemMessage(err, 'Unable to load linked campaign metrics.');
    }
  } finally {
    if (version === metricsLoadVersion) {
      metricsLoading.value = false;
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
      <button class="drawer-shell__backdrop" type="button" aria-label="Close campaign detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="campaign-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayCampaign" class="drawer__title">
            <Badge :tone="getCampaignNeutralTone()">
              {{ getCampaignStatusLabel(displayCampaign.status) }}
            </Badge>
            <h2 id="campaign-detail-title">{{ displayCampaign.name }}</h2>
            <p>Campaign {{ compactId(displayCampaign.id) }} / Product {{ compactId(displayCampaign.idProduct) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close campaign detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayCampaign" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Budget</span>
              <strong class="numeric">{{ formatNumber(displayCampaign.budget) }}</strong>
            </div>
            <div>
              <span>Type</span>
              <strong>{{ getCampaignTypeLabel(displayCampaign.type) }}</strong>
            </div>
            <div>
              <span>Updated</span>
              <strong class="numeric">{{ formatDateTime(displayCampaign.dateUpdate) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Campaign ID</dt><dd>{{ displayCampaign.id }}</dd></div>
              <div><dt>Product ID</dt><dd>{{ displayCampaign.idProduct }}</dd></div>
              <div><dt>Rule set ID</dt><dd>{{ fieldValue(displayCampaign.idSetCampaign) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Campaign fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Name</dt><dd>{{ displayCampaign.name }}</dd></div>
              <div><dt>Region</dt><dd>{{ fieldValue(displayCampaign.region) }}</dd></div>
              <div>
                <dt>Status</dt>
                <dd>
                  <Badge :tone="getCampaignNeutralTone()">
                    {{ getCampaignStatusLabel(displayCampaign.status) }}
                  </Badge>
                </dd>
              </div>
              <div>
                <dt>Type</dt>
                <dd>
                  <Badge :tone="getCampaignNeutralTone()">
                    {{ getCampaignTypeLabel(displayCampaign.type) }}
                  </Badge>
                </dd>
              </div>
              <div><dt>Budget</dt><dd>{{ formatNumber(displayCampaign.budget) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Description</h3>
            <p class="drawer__text">{{ fieldValue(detail?.description) }}</p>
          </section>

          <section class="drawer__section">
            <h3>Timeline</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Start</dt><dd>{{ formatDateTime(displayCampaign.dateStart) }}</dd></div>
              <div><dt>End</dt><dd>{{ formatDateTime(displayCampaign.dateEnd) }}</dd></div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayCampaign.dateCreate) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayCampaign.dateUpdate) }}</dd></div>
            </dl>
          </section>

          <section v-if="formattedTimeToImpression" class="drawer__section">
            <h3>Time to impression</h3>
            <pre class="drawer__json">{{ formattedTimeToImpression }}</pre>
          </section>

          <section class="drawer__section">
            <header class="metrics-header">
              <h3>Linked metrics</h3>
              <span class="numeric">{{ metricsTotalCount }} total</span>
            </header>

            <LoadingState v-if="metricsLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="metricsError" class="drawer__notice">
              {{ metricsError }}
            </div>

            <div v-else-if="metrics.length === 0" class="drawer__placeholder">
              No linked campaign metrics returned by the current API response.
            </div>

            <div v-else class="metrics-list">
              <div v-if="hasHiddenMetrics" class="drawer__placeholder">
                Showing latest {{ LINKED_METRICS_PAGE_SIZE }} of {{ metricsTotalCount }} records.
              </div>

              <article v-for="metric in metrics" :key="metric.id" class="metric">
                <header class="metric__header">
                  <strong class="numeric">{{ formatDateTime(metric.date) }}</strong>
                  <code :title="metric.id">{{ compactId(metric.id) }}</code>
                </header>
                <dl class="metric__fields">
                  <div><dt>Impressions</dt><dd>{{ formatNumber(metric.impressionAmount) }}</dd></div>
                  <div><dt>Clicks</dt><dd>{{ formatNumber(metric.clicksAmount) }}</dd></div>
                  <div><dt>Cost day</dt><dd>{{ formatNumber(metric.costDay) }}</dd></div>
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
.drawer__fields dt,
.metric__fields dt {
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
.metric__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields div,
.metric__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__fields dd,
.metric__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__text {
  margin: 0;
  color: var(--color-text);
  line-height: 1.55;
  white-space: pre-wrap;
}

.drawer__json {
  max-height: 18rem;
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

.metrics-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.metrics-header span {
  color: var(--color-text-muted);
  font-size: 0.78rem;
}

.metrics-list {
  display: grid;
  gap: var(--space-2);
}

.metric {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-3);
}

.metric__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.metric__header code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

@media (min-width: 680px) {
  .drawer__section--summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two,
  .metric__fields {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
