<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { ChevronDown, ChevronUp, RefreshCw } from 'lucide-vue-next';

import PageHeader from '@/widgets/PageHeader.vue';

import { getParserAdminInstances, getParserAdminJournal } from './parserAdminMonitoring.api';
import type { ParserAdminInstance, ParserAdminProxyRunJournal } from './parserAdminMonitoring.types';

type ViewMode = 'instances' | 'journal';

const viewModes: Array<{ key: ViewMode; label: string }> = [
  { key: 'instances', label: 'Парсеры' },
  { key: 'journal', label: 'Журнал' }
];

const activeMode = ref<ViewMode>('instances');
const loading = ref(false);
const error = ref('');
const instances = ref<ParserAdminInstance[]>([]);
const journal = ref<ParserAdminProxyRunJournal[]>([]);
const expandedInstanceIds = ref<Set<string>>(new Set());

const hasInstances = computed(() => instances.value.length > 0);
const hasJournal = computed(() => journal.value.length > 0);

onMounted(() => {
  void loadAll();
});

async function loadAll(): Promise<void> {
  loading.value = true;
  error.value = '';
  try {
    const [instanceItems, journalItems] = await Promise.all([
      getParserAdminInstances(),
      getParserAdminJournal({ page: 1, pageSize: 100 })
    ]);
    instances.value = instanceItems;
    journal.value = journalItems;
  } catch (exception) {
    error.value = getErrorMessage(exception);
  } finally {
    loading.value = false;
  }
}

function toggleInstance(id: string): void {
  const next = new Set(expandedInstanceIds.value);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  expandedInstanceIds.value = next;
}

function isExpanded(id: string): boolean {
  return expandedInstanceIds.value.has(id);
}

function getErrorMessage(exception: unknown): string {
  if (typeof exception === 'object' && exception && 'message' in exception) {
    return String((exception as { message?: unknown }).message ?? 'Не удалось загрузить мониторинг parser-ов.');
  }

  return 'Не удалось загрузить мониторинг parser-ов.';
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function formatDuration(minutes: number | null | undefined): string {
  const safeMinutes = Math.max(0, Math.round(minutes ?? 0));
  const hours = Math.floor(safeMinutes / 60);
  const rest = safeMinutes % 60;
  return `${String(hours).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

function formatProgress(downloaded: number, planned: number): string {
  return `${formatNumber(downloaded)}/${formatNumber(planned)}`;
}

function formatPercent(value: number): string {
  return `${formatNumber(value, 1)}%`;
}

function formatNumber(value: number, maximumFractionDigits = 0): string {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits }).format(value);
}

function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    running: 'Работает',
    completed: 'Закончил',
    failed: 'Ошибка'
  };

  return labels[status] ?? status;
}
</script>

<template>
  <main class="parser-admin">
    <PageHeader title="Parser мониторинг" />

    <section class="parser-admin__panel">
      <div class="parser-admin__toolbar">
        <div class="parser-admin__tabs" role="tablist" aria-label="Разделы мониторинга parser-ов">
          <button
            v-for="mode in viewModes"
            :key="mode.key"
            class="parser-admin__tab"
            :class="{ 'parser-admin__tab--active': activeMode === mode.key }"
            type="button"
            @click="activeMode = mode.key"
          >
            {{ mode.label }}
          </button>
        </div>

        <button class="parser-admin__refresh" type="button" :disabled="loading" @click="loadAll">
          <RefreshCw :size="16" />
          Обновить
        </button>
      </div>

      <p v-if="error" class="parser-admin__error">{{ error }}</p>
      <p v-if="loading" class="parser-admin__loading">Загрузка мониторинга...</p>

      <div v-if="activeMode === 'instances'" class="parser-admin__instances">
        <article v-for="instance in instances" :key="instance.id" class="parser-admin__instance">
          <button class="parser-admin__instance-main" type="button" @click="toggleInstance(instance.id)">
            <span class="parser-admin__instance-title">
              <strong>{{ instance.displayName || instance.parserInstanceId }}</strong>
              <span>{{ instance.parserInstanceId }}</span>
            </span>
            <span class="parser-admin__metric">
              <span>Работающих прокси</span>
              <strong>{{ instance.runningProxiesCount }}/{{ instance.totalProxiesCount }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>Прогресс</span>
              <strong>{{ formatProgress(instance.downloadedProductsCount, instance.plannedProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>Выполнено</span>
              <strong>{{ formatPercent(instance.progressPercent) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>Время работы</span>
              <strong>{{ formatDuration(instance.runtimeMinutes) }}</strong>
            </span>
            <ChevronUp v-if="isExpanded(instance.id)" :size="18" />
            <ChevronDown v-else :size="18" />
          </button>

          <div v-if="isExpanded(instance.id)" class="parser-admin__proxies">
            <div v-if="instance.proxies.length === 0" class="parser-admin__empty-row">
              Proxy-процессы еще не запускались.
            </div>
            <div v-for="proxy in instance.proxies" :key="proxy.id" class="parser-admin__proxy">
              <span class="parser-admin__proxy-title">
                <strong>{{ proxy.proxyKey }}</strong>
                <span>{{ proxy.sourceSubcategory }}</span>
              </span>
              <span class="parser-admin__metric">
                <span>Статус</span>
                <strong :class="`parser-admin__status parser-admin__status--${proxy.status}`">
                  {{ statusLabel(proxy.status) }}
                </strong>
              </span>
              <span class="parser-admin__metric">
                <span>Прогресс</span>
                <strong>{{ formatProgress(proxy.downloadedProductsCount, proxy.plannedProductsCount) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>Выполнено</span>
                <strong>{{ formatPercent(proxy.progressPercent) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>Время работы</span>
                <strong>{{ formatDuration(proxy.runtimeMinutes) }}</strong>
              </span>
            </div>
          </div>
        </article>

        <div v-if="!loading && !hasInstances" class="parser-admin__empty">
          Parser-инстансы еще не зарегистрированы.
        </div>
      </div>

      <div v-else class="parser-admin__table-wrap">
        <table class="parser-admin__table">
          <thead>
            <tr>
              <th>Прокси</th>
              <th>Начало</th>
              <th>Завершение</th>
              <th>Инстанс</th>
              <th>Выгружено</th>
              <th>Запланировано</th>
              <th>Время</th>
              <th>Ошибка</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in journal" :key="item.id">
              <td>
                <strong>{{ item.proxyKey }}</strong>
                <span>{{ item.sourceSubcategory }}</span>
              </td>
              <td>{{ formatDate(item.startedAtUtc) }}</td>
              <td>{{ formatDate(item.finishedAtUtc) }}</td>
              <td>{{ item.parserInstanceId }}</td>
              <td>{{ formatNumber(item.downloadedProductsCount) }}</td>
              <td>{{ formatNumber(item.plannedProductsCount) }}</td>
              <td>{{ formatDuration(item.runtimeMinutes) }}</td>
              <td>{{ item.error || '—' }}</td>
            </tr>
          </tbody>
        </table>

        <div v-if="!loading && !hasJournal" class="parser-admin__empty">
          Завершенных или упавших proxy-процессов пока нет.
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.parser-admin {
  display: grid;
  gap: var(--space-6);
}

.parser-admin__panel {
  display: grid;
  gap: var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel);
  padding: var(--space-4);
}

.parser-admin__toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.parser-admin__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.parser-admin__tab,
.parser-admin__refresh {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--color-text);
  font-weight: 750;
}

.parser-admin__tab {
  min-height: 2.25rem;
  padding: 0 var(--space-4);
}

.parser-admin__tab--active,
.parser-admin__refresh {
  border-color: var(--color-ember);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.parser-admin__refresh {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 2.25rem;
  padding: 0 var(--space-4);
}

.parser-admin__error {
  border: 1px solid var(--color-danger);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  color: var(--color-danger);
}

.parser-admin__loading,
.parser-admin__empty,
.parser-admin__empty-row {
  color: var(--color-text-muted);
}

.parser-admin__instances {
  display: grid;
  gap: var(--space-3);
}

.parser-admin__instance {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-card);
  overflow: visible;
}

.parser-admin__instance-main,
.parser-admin__proxy {
  display: grid;
  grid-template-columns: minmax(220px, 1.3fr) repeat(4, minmax(130px, 1fr)) auto;
  align-items: center;
  gap: var(--space-4);
  width: 100%;
  padding: var(--space-4);
  text-align: left;
}

.parser-admin__instance-main {
  border: 0;
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
}

.parser-admin__instance-main:hover {
  background: var(--surface-active-overlay);
}

.parser-admin__instance-title,
.parser-admin__proxy-title,
.parser-admin__metric,
.parser-admin__table td span {
  display: grid;
  gap: 0.2rem;
}

.parser-admin__instance-title strong,
.parser-admin__proxy-title strong,
.parser-admin__metric strong,
.parser-admin__table td {
  font-weight: 750;
}

.parser-admin__instance-title span,
.parser-admin__proxy-title span,
.parser-admin__metric span,
.parser-admin__table td span {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 650;
}

.parser-admin__proxies {
  display: grid;
  border-top: 1px solid var(--color-border);
}

.parser-admin__proxy {
  grid-template-columns: minmax(220px, 1.3fr) repeat(4, minmax(130px, 1fr));
  border-top: 1px solid var(--color-border-muted);
}

.parser-admin__proxy:first-child {
  border-top: 0;
}

.parser-admin__status {
  display: inline-flex;
  width: fit-content;
  border-radius: 999px;
  padding: 0.15rem 0.55rem;
  background: var(--surface-muted);
  color: var(--color-text);
}

.parser-admin__status--completed {
  background: rgba(5, 150, 105, 0.12);
  color: #047857;
}

.parser-admin__status--failed {
  background: rgba(190, 18, 60, 0.12);
  color: #9f1239;
}

.parser-admin__status--running {
  background: rgba(217, 119, 6, 0.12);
  color: #9a3412;
}

.parser-admin__table-wrap {
  overflow: auto;
}

.parser-admin__table {
  width: 100%;
  min-width: 920px;
  border-collapse: collapse;
}

.parser-admin__table th,
.parser-admin__table td {
  border-bottom: 1px solid var(--color-border);
  padding: var(--space-3);
  text-align: left;
  vertical-align: top;
}

.parser-admin__table th {
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 850;
}

.parser-admin__empty,
.parser-admin__empty-row {
  padding: var(--space-4);
}

@media (max-width: 1100px) {
  .parser-admin__instance-main,
  .parser-admin__proxy {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
