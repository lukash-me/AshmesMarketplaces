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
  getLogisticTypeLabel,
  getNeutralTone,
  getWarehouseActiveLabel,
  getWarehouseActiveTone
} from './logisticDisplay';
import { getLogistic, getWarehouse } from './logistics.api';
import type { LogisticDetail, LogisticListItem, WarehouseDetail } from './logistics.types';

const props = defineProps<{
  open: boolean;
  logistic: LogisticListItem | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<LogisticDetail | null>(null);
const warehouse = ref<WarehouseDetail | null>(null);
const detailLoading = ref(false);
const warehouseLoading = ref(false);
const detailError = ref('');
const warehouseError = ref('');
let detailLoadVersion = 0;
let warehouseLoadVersion = 0;

const displayLogistic = computed(() => detail.value ?? props.logistic);
const linkedWarehouseId = computed(() => displayLogistic.value?.idWarehouse ?? null);

watch(
  () => [props.open, props.logistic?.id] as const,
  async ([open, id]) => {
    if (!open || !id) {
      detail.value = null;
      warehouse.value = null;
      detailError.value = '';
      warehouseError.value = '';
      return;
    }

    await loadDetail(id);
  },
  { immediate: true }
);

watch(
  () => [props.open, displayLogistic.value?.idWarehouse] as const,
  async ([open, idWarehouse]) => {
    if (!open || !idWarehouse) {
      warehouse.value = null;
      warehouseError.value = '';
      warehouseLoading.value = false;
      return;
    }

    await loadWarehouse(idWarehouse);
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
    const response = await getLogistic(id);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load logistic details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadWarehouse(id: string): Promise<void> {
  const version = ++warehouseLoadVersion;

  warehouseLoading.value = true;
  warehouseError.value = '';
  warehouse.value = null;

  try {
    const response = await getWarehouse(id);

    if (version === warehouseLoadVersion) {
      warehouse.value = response;
    }
  } catch (err) {
    if (version === warehouseLoadVersion) {
      warehouseError.value = getProblemMessage(err, 'Unable to load linked warehouse.');
    }
  } finally {
    if (version === warehouseLoadVersion) {
      warehouseLoading.value = false;
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
      <button class="drawer-shell__backdrop" type="button" aria-label="Close logistic detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="logistic-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayLogistic" class="drawer__title">
            <Badge :tone="getNeutralTone()">
              {{ getLogisticTypeLabel(displayLogistic.type) }}
            </Badge>
            <h2 id="logistic-detail-title">Logistic {{ compactId(displayLogistic.id) }}</h2>
            <p>Product {{ compactId(displayLogistic.idProduct) }} / Warehouse {{ compactId(displayLogistic.idWarehouse) }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close logistic detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayLogistic" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>Stock</span>
              <strong class="numeric">{{ formatNumber(displayLogistic.stockAmount) }}</strong>
            </div>
            <div>
              <span>Stock statistic</span>
              <strong class="numeric">{{ formatNumber(displayLogistic.stockAmountStatistic) }}</strong>
            </div>
            <div>
              <span>Date</span>
              <strong class="numeric">{{ formatDateTime(displayLogistic.date) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }}
          </section>

          <section class="drawer__section">
            <h3>Identifiers</h3>
            <dl class="drawer__fields">
              <div><dt>Logistic ID</dt><dd>{{ displayLogistic.id }}</dd></div>
              <div><dt>Product ID</dt><dd>{{ displayLogistic.idProduct }}</dd></div>
              <div><dt>Warehouse ID</dt><dd>{{ fieldValue(displayLogistic.idWarehouse) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Stock fields</h3>
            <dl class="drawer__fields drawer__fields--three">
              <div><dt>Stock</dt><dd>{{ formatNumber(displayLogistic.stockAmount) }}</dd></div>
              <div><dt>Stock statistic</dt><dd>{{ formatNumber(displayLogistic.stockAmountStatistic) }}</dd></div>
              <div><dt>In transit</dt><dd>{{ formatNumber(displayLogistic.stockInTransit) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Cost fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div><dt>Storage cost</dt><dd>{{ formatNumber(displayLogistic.costStorage) }}</dd></div>
              <div><dt>Logistic cost</dt><dd>{{ formatNumber(displayLogistic.costLogistic) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <h3>Record fields</h3>
            <dl class="drawer__fields drawer__fields--two">
              <div>
                <dt>Type</dt>
                <dd>
                  <Badge :tone="getNeutralTone()">{{ getLogisticTypeLabel(displayLogistic.type) }}</Badge>
                </dd>
              </div>
              <div><dt>Date</dt><dd>{{ formatDateTime(displayLogistic.date) }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <header class="warehouse-header">
              <h3>Linked warehouse</h3>
              <code v-if="linkedWarehouseId" :title="linkedWarehouseId">{{ compactId(linkedWarehouseId) }}</code>
            </header>

            <div v-if="!linkedWarehouseId" class="drawer__placeholder">
              No warehouse linked by the current logistics record.
            </div>

            <LoadingState v-else-if="warehouseLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="warehouseError" class="drawer__notice">
              {{ warehouseError }}
            </div>

            <div v-else-if="warehouse" class="warehouse-detail">
              <div class="warehouse-detail__title">
                <strong>{{ warehouse.name }}</strong>
                <Badge :tone="getWarehouseActiveTone(warehouse.isActive)">
                  {{ getWarehouseActiveLabel(warehouse.isActive) }}
                </Badge>
              </div>

              <dl class="drawer__fields drawer__fields--two">
                <div><dt>Warehouse ID</dt><dd>{{ warehouse.id }}</dd></div>
                <div><dt>Marketplace ID</dt><dd>{{ warehouse.idMp }}</dd></div>
                <div><dt>Code</dt><dd>{{ warehouse.code }}</dd></div>
                <div><dt>Type</dt><dd>{{ getLogisticTypeLabel(warehouse.type) }}</dd></div>
                <div><dt>Region</dt><dd>{{ warehouse.region }}</dd></div>
                <div><dt>City</dt><dd>{{ fieldValue(warehouse.city) }}</dd></div>
                <div><dt>Address</dt><dd>{{ fieldValue(warehouse.address) }}</dd></div>
                <div><dt>Latitude</dt><dd>{{ fieldValue(warehouse.latitude) }}</dd></div>
                <div><dt>Longitude</dt><dd>{{ fieldValue(warehouse.longitude) }}</dd></div>
                <div><dt>Created</dt><dd>{{ formatDateTime(warehouse.dateCreate) }}</dd></div>
                <div><dt>Updated</dt><dd>{{ formatDateTime(warehouse.dateUpdate) }}</dd></div>
              </dl>
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

.warehouse-header,
.warehouse-detail__title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.warehouse-header code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

.warehouse-detail {
  display: grid;
  gap: var(--space-3);
}

.warehouse-detail__title strong {
  color: var(--color-text);
  font-size: 0.9rem;
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
