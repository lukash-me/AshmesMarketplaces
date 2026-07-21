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
  formatNumber,
  getOrderStatusLabel,
  getOrderStatusTone
} from './orderDisplay';
import { getOrder } from './orders.api';
import type { OrderDetail, OrderListItem } from './orders.types';

const props = defineProps<{
  open: boolean;
  order: OrderListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<OrderDetail | null>(null);
const loading = ref(false);
const error = ref('');
let loadVersion = 0;

const displayOrder = computed(() => detail.value ?? props.order);

watch(
  () => [props.open, props.order?.id] as const,
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
    const response = await getOrder(id);

    if (version === loadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === loadVersion) {
      error.value = getProblemMessage(err, 'Unable to load order details.');
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
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Close order detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="order-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayOrder" class="drawer__title">
            <Badge :tone="getOrderStatusTone()">{{ getOrderStatusLabel(displayOrder.status) }}</Badge>
            <h2 id="order-detail-title">Order {{ compactId(displayOrder.id) }}</h2>
            <p>Product {{ compactId(displayOrder.idProduct) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close order detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayOrder" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Price</span>
              <strong class="numeric">{{ formatNumber(displayOrder.price) }}</strong>
            </div>
            <div>
              <span>Amount</span>
              <strong class="numeric">{{ formatNumber(displayOrder.amount) }}</strong>
            </div>
            <div>
              <span>Opened</span>
              <strong class="numeric">{{ formatDateTime(displayOrder.dateOpened) }}</strong>
            </div>
          </section>

          <LoadingState v-if="loading" class="drawer__loading" :rows="3" />

          <section v-if="error" class="drawer__notice">
            {{ error }}
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Order ID</dt><dd>{{ displayOrder.id }}</dd></div>
              <div><dt>Product ID</dt><dd>{{ displayOrder.idProduct }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Commercial</h3>
            <dl class="drawer__fields drawer__fields--three">
              <div><dt>Price</dt><dd>{{ formatNumber(displayOrder.price) }}</dd></div>
              <div><dt>Discount</dt><dd>{{ formatNumber(displayOrder.discount) }}</dd></div>
              <div><dt>Amount</dt><dd>{{ formatNumber(displayOrder.amount) }}</dd></div>
              <div><dt>Status</dt><dd>{{ getOrderStatusLabel(displayOrder.status) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Route</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Source</dt><dd>{{ fieldValue(displayOrder.locationSource) }}</dd></div>
              <div><dt>Destination</dt><dd>{{ fieldValue(displayOrder.locationDestination) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Timeline</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Opened</dt><dd>{{ formatDateTime(displayOrder.dateOpened) }}</dd></div>
              <div><dt>Delivered</dt><dd>{{ formatDateTime(displayOrder.dateDelivered) }}</dd></div>
              <div><dt>Closed</dt><dd>{{ formatDateTime(displayOrder.dateClosed) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayOrder.dateUpdate) }}</dd></div>
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
  top: var(--space-3);
  right: var(--space-3);
  bottom: var(--space-3);
  display: grid;
  width: min(38rem, calc(100vw - 1.5rem));
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

.drawer__notice {
  border: 1px dashed var(--state-danger-border);
  border-radius: var(--radius-sm);
  background: var(--state-danger-soft);
  color: var(--state-danger);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__loading {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
}

@media (min-width: 680px) {
  .drawer__section--summary,
  .drawer__fields--three {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
