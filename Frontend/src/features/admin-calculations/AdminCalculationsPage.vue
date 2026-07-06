<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { Play, RefreshCw, ScrollText, X } from 'lucide-vue-next';

import { getAdminCalculations, runAdminCalculation } from './adminCalculations.api';
import type { AdminCalculation } from './adminCalculations.types';

type CalculationMode = 'scheduled' | 'on_demand';

const calculations = ref<AdminCalculation[]>([]);
const activeMode = ref<CalculationMode>('scheduled');
const loading = ref(false);
const runningKey = ref<string | null>(null);
const error = ref<string | null>(null);
const message = ref<string | null>(null);
const scenarioCalculation = ref<AdminCalculation | null>(null);
let pollingTimer: number | null = null;

const visibleCalculations = computed(() =>
  calculations.value.filter((calculation) => executionMode(calculation) === activeMode.value)
);

const hasActiveManualRun = computed(() =>
  calculations.value.some((calculation) => isActiveStatus(calculation.lastManualRun?.status))
);

onMounted(async () => {
  await loadCalculations();
  syncPolling();
});

onBeforeUnmount(() => {
  stopPolling();
});

async function loadCalculations() {
  loading.value = true;
  error.value = null;

  try {
    calculations.value = await getAdminCalculations();
  } catch (err) {
    error.value = errorMessage(err);
  } finally {
    loading.value = false;
  }
}

async function refresh() {
  await loadCalculations();
  syncPolling();
}

async function runNow(calculation: AdminCalculation) {
  if (!calculation.canRunManually || isCalculationRunning(calculation)) {
    return;
  }

  runningKey.value = calculation.scheduleKey;
  error.value = null;
  message.value = null;

  try {
    await runAdminCalculation(calculation.scheduleKey);
    message.value = 'Расчет поставлен в очередь.';
    await loadCalculations();
    syncPolling();
  } catch (err) {
    error.value = errorMessage(err);
  } finally {
    runningKey.value = null;
  }
}

function setMode(mode: CalculationMode) {
  activeMode.value = mode;
  message.value = null;
  error.value = null;
}

function openScenarios(calculation: AdminCalculation) {
  scenarioCalculation.value = calculation;
}

function closeScenarios() {
  scenarioCalculation.value = null;
}

function syncPolling() {
  if (hasActiveManualRun.value) {
    if (pollingTimer === null) {
      pollingTimer = window.setInterval(() => {
        void loadCalculations().then(syncPolling);
      }, 5000);
    }
    return;
  }

  stopPolling();
}

function stopPolling() {
  if (pollingTimer !== null) {
    window.clearInterval(pollingTimer);
    pollingTimer = null;
  }
}

function executionMode(calculation: AdminCalculation): CalculationMode {
  return calculation.executionMode === 'on_demand' ? 'on_demand' : 'scheduled';
}

function isOnDemand(calculation: AdminCalculation): boolean {
  return executionMode(calculation) === 'on_demand';
}

function isCalculationRunning(calculation: AdminCalculation): boolean {
  return runningKey.value === calculation.scheduleKey || isActiveStatus(calculation.lastManualRun?.status);
}

function isActiveStatus(status?: string | null): boolean {
  return status === 'queued' || status === 'running';
}

function statusText(status?: string | null): string {
  switch (status) {
    case 'queued':
      return 'В очереди';
    case 'running':
      return 'Выполняется';
    case 'completed':
      return 'Завершен';
    case 'failed':
      return 'Ошибка';
    case 'pending':
      return 'Ожидает';
    default:
      return '-';
  }
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

function scenariosFor(calculation: AdminCalculation | null) {
  return calculation?.scenarios?.length
    ? calculation.scenarios
    : [{
        id: 'standard_calculation',
        title: 'Стандартный расчет',
        description: 'Для этого расчета специальные сценарии пока не описаны.'
      }];
}

function errorMessage(err: unknown): string {
  if (
    typeof err === 'object' &&
    err !== null &&
    'response' in err &&
    typeof (err as { response?: { data?: { detail?: unknown } } }).response?.data?.detail === 'string'
  ) {
    return (err as { response: { data: { detail: string } } }).response.data.detail;
  }

  if (err instanceof Error) {
    return err.message;
  }

  return 'Не удалось выполнить действие.';
}
</script>

<template>
  <section class="admin-calculations">
    <header class="admin-calculations__header">
      <div>
        <h1>Расчеты</h1>
        <p>Администрирование публичных расчетов витрин и ручной запуск без изменения расписания.</p>
      </div>
      <button class="admin-calculations__button admin-calculations__button--ghost" type="button" :disabled="loading" @click="refresh">
        <RefreshCw :size="16" />
        Обновить
      </button>
    </header>

    <div class="admin-calculations__mode-tabs" role="tablist" aria-label="Режимы расчетов">
      <button
        class="admin-calculations__mode-tab"
        :class="{ 'admin-calculations__mode-tab--active': activeMode === 'scheduled' }"
        type="button"
        role="tab"
        :aria-selected="activeMode === 'scheduled'"
        @click="setMode('scheduled')"
      >
        По расписанию
      </button>
      <button
        class="admin-calculations__mode-tab"
        :class="{ 'admin-calculations__mode-tab--active': activeMode === 'on_demand' }"
        type="button"
        role="tab"
        :aria-selected="activeMode === 'on_demand'"
        @click="setMode('on_demand')"
      >
        По запросу
      </button>
    </div>

    <div v-if="message" class="admin-calculations__notice admin-calculations__notice--success">
      {{ message }}
    </div>
    <div v-if="error" class="admin-calculations__notice admin-calculations__notice--error">
      {{ error }}
    </div>

    <div class="admin-calculations__list">
      <article v-for="calculation in visibleCalculations" :key="calculation.scheduleKey" class="admin-calculations__card">
        <div class="admin-calculations__card-main">
          <div class="admin-calculations__title-block">
            <h2>{{ calculation.name }}</h2>
            <span>{{ calculation.scheduleKey }}</span>
          </div>
          <p>{{ calculation.description }}</p>
          <p class="admin-calculations__details">{{ calculation.details }}</p>
        </div>

        <div class="admin-calculations__metrics">
          <div class="admin-calculations__metric">
            <span>{{ isOnDemand(calculation) ? 'Плановый запуск' : 'Следующий плановый запуск' }}</span>
            <strong>{{ isOnDemand(calculation) ? 'Отключен' : formatDateTime(calculation.nextRunAtUtc) }}</strong>
            <small>{{ isOnDemand(calculation) ? 'Выполняется только по запросу' : `${calculation.localTime} · ${calculation.timezoneId}` }}</small>
          </div>
          <div class="admin-calculations__metric">
            <span>Последний плановый статус</span>
            <strong>{{ isOnDemand(calculation) ? '-' : statusText(calculation.lastScheduledStatus) }}</strong>
            <small>{{ isOnDemand(calculation) ? 'Плановый запуск не выполняется' : formatDateTime(calculation.lastScheduledCompletedAtUtc ?? calculation.lastScheduledStartedAtUtc) }}</small>
          </div>
          <div class="admin-calculations__metric">
            <span>Последний ручной запуск</span>
            <strong>{{ statusText(calculation.lastManualRun?.status) }}</strong>
            <small>{{ formatDateTime(calculation.lastManualRun?.completedAtUtc ?? calculation.lastManualRun?.startedAtUtc ?? calculation.lastManualRun?.requestedAtUtc) }}</small>
          </div>
        </div>

        <div v-if="!isOnDemand(calculation) && calculation.lastScheduledError" class="admin-calculations__inline-error">
          {{ calculation.lastScheduledError }}
        </div>
        <div v-if="calculation.lastManualRun?.error" class="admin-calculations__inline-error">
          {{ calculation.lastManualRun.error }}
        </div>

        <div class="admin-calculations__actions">
          <button
            class="admin-calculations__button"
            type="button"
            :disabled="!calculation.canRunManually || isCalculationRunning(calculation)"
            @click="runNow(calculation)"
          >
            <Play :size="15" />
            Рассчитать сейчас
          </button>
          <button
            class="admin-calculations__button admin-calculations__button--ghost"
            type="button"
            @click="openScenarios(calculation)"
          >
            <ScrollText :size="15" />
            Сценарии
          </button>
          <span v-if="!calculation.canRunManually" class="admin-calculations__disabled-reason">
            {{ calculation.manualRunDisabledReason }}
          </span>
          <span v-else-if="isCalculationRunning(calculation)" class="admin-calculations__disabled-reason">
            Расчет уже ожидает выполнения или выполняется.
          </span>
        </div>
      </article>
    </div>

    <div v-if="!loading && visibleCalculations.length === 0" class="admin-calculations__empty">
      {{ activeMode === 'scheduled' ? 'Расписания расчетов еще не созданы.' : 'Расчеты по запросу еще не созданы.' }}
    </div>

    <Teleport to="body">
      <div v-if="scenarioCalculation" class="admin-calculations__modal" role="presentation">
        <button class="admin-calculations__modal-backdrop" type="button" aria-label="Закрыть сценарии" @click="closeScenarios" />
        <section class="admin-calculations__modal-panel" role="dialog" aria-modal="true" aria-labelledby="calculation-scenarios-title">
          <header class="admin-calculations__modal-header">
            <div>
              <h2 id="calculation-scenarios-title">Сценарии</h2>
              <p>{{ scenarioCalculation.name }}</p>
            </div>
            <button class="admin-calculations__icon-button" type="button" aria-label="Закрыть" @click="closeScenarios">
              <X :size="18" />
            </button>
          </header>

          <div class="admin-calculations__scenario-list">
            <article v-for="scenario in scenariosFor(scenarioCalculation)" :key="scenario.id" class="admin-calculations__scenario">
              <h3>{{ scenario.title }}</h3>
              <p>{{ scenario.description }}</p>
            </article>
          </div>
        </section>
      </div>
    </Teleport>
  </section>
</template>

<style scoped>
.admin-calculations {
  display: grid;
  gap: var(--space-4);
  width: min(82rem, 100%);
  margin: 0 auto;
}

.admin-calculations__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
}

.admin-calculations__header h1 {
  margin: 0;
  font-size: 1.35rem;
}

.admin-calculations__header p {
  margin: 0.35rem 0 0;
  color: var(--color-text-muted);
  font-size: 0.88rem;
}

.admin-calculations__mode-tabs {
  display: inline-flex;
  width: max-content;
  gap: 0.35rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 78%, white 12%);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(135deg, rgb(255 255 255 / 0.24), rgb(255 255 255 / 0.06)),
    color-mix(in srgb, var(--surface-card) 82%, white 8%);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.22),
    0 10px 24px rgb(15 23 42 / 0.07);
  backdrop-filter: blur(18px) saturate(1.12);
  padding: 0.28rem;
}

button.admin-calculations__mode-tab {
  appearance: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 8.4rem;
  min-height: 2.1rem;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text);
  padding: 0 var(--space-3);
  font: inherit;
  font-size: 0.86rem;
  font-weight: 750;
  line-height: 1;
  white-space: nowrap;
  transition:
    background-color 0.16s ease,
    border-color 0.16s ease,
    color 0.16s ease,
    box-shadow 0.16s ease;
}

button.admin-calculations__mode-tab:hover {
  border-color: color-mix(in srgb, var(--color-ember) 52%, transparent);
  background: color-mix(in srgb, var(--accent-ember-soft) 42%, transparent);
}

button.admin-calculations__mode-tab:focus-visible {
  outline: 2px solid color-mix(in srgb, var(--color-ember) 70%, white 10%);
  outline-offset: 2px;
}

button.admin-calculations__mode-tab--active {
  border-color: var(--color-ember);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.18), rgb(255 255 255 / 0.02)),
    var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.28),
    0 8px 18px rgb(249 115 22 / 0.10);
}

.admin-calculations__list {
  display: grid;
  gap: var(--space-3);
}

.admin-calculations__card {
  position: relative;
  overflow: hidden;
  display: grid;
  gap: var(--space-3);
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, white 18%);
  border-radius: var(--radius-md);
  background:
    linear-gradient(135deg, rgb(255 255 255 / 0.18), rgb(255 255 255 / 0.04) 42%, rgb(249 115 22 / 0.035)),
    color-mix(in srgb, var(--surface-card) 78%, white 10%);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.24),
    inset 0 -1px 0 rgb(255 255 255 / 0.08),
    0 18px 46px rgb(15 23 42 / 0.10);
  backdrop-filter: blur(22px) saturate(1.18);
  padding: var(--space-4);
}

.admin-calculations__card::before {
  content: "";
  position: absolute;
  inset: 0;
  pointer-events: none;
  background:
    radial-gradient(circle at 12% 0%, rgb(255 255 255 / 0.24), transparent 14rem),
    linear-gradient(120deg, transparent 0 42%, rgb(255 255 255 / 0.12) 48%, transparent 56%);
  opacity: 0.82;
}

.admin-calculations__card > * {
  position: relative;
  z-index: 1;
}

.admin-calculations__card-main {
  display: grid;
  gap: var(--space-2);
}

.admin-calculations__title-block {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-2);
}

.admin-calculations__title-block h2 {
  margin: 0;
  font-size: 1rem;
}

.admin-calculations__title-block span,
.admin-calculations__details,
.admin-calculations__metric span,
.admin-calculations__metric small,
.admin-calculations__disabled-reason {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 650;
}

.admin-calculations__card-main p {
  margin: 0;
  font-size: 0.88rem;
}

.admin-calculations__details {
  max-width: 58rem;
  line-height: 1.45;
}

.admin-calculations__metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-3);
}

.admin-calculations__metric {
  display: grid;
  align-content: start;
  gap: 0.18rem;
  min-height: 4.3rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 68%, white 16%);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(145deg, rgb(255 255 255 / 0.16), rgb(255 255 255 / 0.035)),
    color-mix(in srgb, var(--surface-card) 72%, white 12%);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.18),
    0 8px 18px rgb(15 23 42 / 0.06);
  backdrop-filter: blur(16px) saturate(1.12);
  padding: var(--space-3);
}

.admin-calculations__metric strong {
  font-size: 0.92rem;
}

.admin-calculations__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
}

.admin-calculations__button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: 2.2rem;
  border: 1px solid var(--color-ember);
  border-radius: var(--radius-sm);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  padding: 0 var(--space-3);
  font-weight: 750;
}

.admin-calculations__button--ghost {
  background: transparent;
}

.admin-calculations__button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.admin-calculations__notice,
.admin-calculations__inline-error,
.admin-calculations__empty {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(135deg, rgb(255 255 255 / 0.16), rgb(255 255 255 / 0.04)),
    color-mix(in srgb, var(--surface-card) 76%, white 9%);
  backdrop-filter: blur(18px) saturate(1.12);
  padding: var(--space-3);
  font-size: 0.85rem;
  font-weight: 650;
}

.admin-calculations__notice--success {
  border-color: var(--state-success-border);
  background: var(--color-success-soft);
  color: var(--state-success-text);
}

.admin-calculations__notice--error,
.admin-calculations__inline-error {
  border-color: var(--state-danger-border);
  background: var(--color-danger-soft);
  color: var(--state-danger-text);
}

.admin-calculations__modal {
  position: fixed;
  z-index: 1000;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--space-4);
}

.admin-calculations__modal-backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: rgb(15 23 42 / 0.48);
}

.admin-calculations__modal-panel {
  position: relative;
  display: grid;
  width: min(42rem, 100%);
  max-height: min(40rem, calc(100vh - 4rem));
  overflow: auto;
  gap: var(--space-4);
  border: 1px solid color-mix(in srgb, var(--color-border) 75%, white 15%);
  border-radius: var(--radius-md);
  background: var(--surface-panel-raised);
  box-shadow: var(--shadow-panel);
  padding: var(--space-4);
}

.admin-calculations__modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.admin-calculations__modal-header h2,
.admin-calculations__modal-header p,
.admin-calculations__scenario h3,
.admin-calculations__scenario p {
  margin: 0;
}

.admin-calculations__modal-header p {
  margin-top: 0.25rem;
  color: var(--color-text-muted);
  font-size: 0.85rem;
}

.admin-calculations__icon-button {
  display: inline-grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text);
}

.admin-calculations__scenario-list {
  display: grid;
  gap: var(--space-3);
}

.admin-calculations__scenario {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-3);
}

.admin-calculations__scenario h3 {
  font-size: 0.95rem;
}

.admin-calculations__scenario p {
  color: var(--color-text-muted);
  font-size: 0.86rem;
  line-height: 1.5;
}

@media (max-width: 860px) {
  .admin-calculations__header {
    display: grid;
  }

  .admin-calculations__mode-tabs {
    width: 100%;
  }

  button.admin-calculations__mode-tab {
    flex: 1;
    min-width: 0;
  }

  .admin-calculations__metrics {
    grid-template-columns: 1fr;
  }
}
</style>
